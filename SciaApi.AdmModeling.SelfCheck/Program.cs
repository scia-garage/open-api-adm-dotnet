using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CSInfrastructure;
using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Integration.Bootstrapper;
using ModelExchanger.AnalysisDataModel.Libraries;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using ModelExchanger.AnalysisDataModel.Subtypes;
using SciaApi.AdmModeling.ModelBuilding;
using SciaApi.AdmModeling.Roundtrip;

namespace SciaApi.AdmModeling.SelfCheck
{
    internal static class Program
    {
        private const string DefaultOpenApiDllDirectory = @"C:\Program Files\SCIA\Engineer2027\OpenAPI_dll";

        private static int Main(string[] args)
        {
            string dllDirectory = args.Length > 0 ? args[0] : DefaultOpenApiDllDirectory;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) => ResolveFromDirectory(dllDirectory, eventArgs.Name);
            try
            {
                MethodInfo run = typeof(Program).Assembly
                    .GetType("SciaApi.AdmModeling.SelfCheck.SelfCheckRunner", true)
                    .GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                return (int)run.Invoke(null, null);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAILED: " + exception);
                return 1;
            }
        }

        private static Assembly ResolveFromDirectory(string directory, string assemblyName)
        {
            string path = Path.Combine(directory, new AssemblyName(assemblyName).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        }
    }

    /// <summary>
    /// Offline checks of the ADM builders and roundtrip helpers. Loaded by reflection from Program so that ADM types are
    /// only loaded after the assembly resolver is installed.
    /// </summary>
    internal static class SelfCheckRunner
    {
        private const string NamePrefix = "SelfCheck";

        public static int Run()
        {
            try
            {
                RunChecks();
                Console.WriteLine("All checks passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAILED: " + exception);
                return 1;
            }
        }

        private static void RunChecks()
        {
            AnalysisModel source = null;
            Check("source model", () =>
            {
                source = BuildSourceModel();
                return $"{source.Count} objects: {CountOf<StructuralPointConnection>(source)} nodes, {CountOf<StructuralCurveMember>(source)} beam, {CountOf<StructuralCrossSection>(source)} sections, {CountOf<StructuralMaterial>(source)} materials, validated";
            });

            Check("catalog pair from curve member", () =>
            {
                ExistingDefinitions found = ExistingModelCatalog.Find(source);
                Require(found.SourceBeam != null, "no curve member was used");
                Require(found.Section.Material.Id == found.Material.Id, "section and material do not match");
                return $"beam '{found.SourceBeam.Name}' -> section '{found.Section.Name}', material '{found.Material.Name}'";
            });

            Check("catalog standalone fallback", () =>
            {
                ExistingDefinitions found = ExistingModelCatalog.Find(BuildMaterialsAndSectionsModel());
                Require(found.SourceBeam == null, "a curve member was unexpectedly used");
                Require(found.Section.Material.Id == found.Material.Id, "section and material do not match");
                return $"standalone section '{found.Section.Name}', material '{found.Material.Name}'";
            });

            Check("catalog rejects empty model", () =>
            {
                ExpectException<InvalidOperationException>(() => ExistingModelCatalog.Find(new AnalysisModel()));
                ExpectException<InvalidOperationException>(() => ExistingModelCatalog.FindPlacement(new AnalysisModel()));
                return "Find and FindPlacement throw InvalidOperationException";
            });

            Check("placement beside existing geometry", () =>
            {
                BeamPlacement placement = ExistingModelCatalog.FindPlacement(source);
                List<StructuralPointConnection> nodes = source.OfType<StructuralPointConnection>().ToList();
                double maxX = nodes.Max(node => node.X.Meters);
                double maxZ = nodes.Max(node => node.Z.Meters);
                double centreY = (nodes.Min(node => node.Y.Meters) + nodes.Max(node => node.Y.Meters)) / 2.0;
                Require(
                    Near(placement.StartX, maxX + 2.0) &&
                    Near(placement.EndX - placement.StartX, 3.0) &&
                    Near(placement.FirstBeamY, centreY) &&
                    Near(placement.SecondBeamY - placement.FirstBeamY, 2.0) &&
                    Near(placement.Z, maxZ),
                    "placement does not match the documented offsets");
                return $"start X {placement.StartX:0.###} m, first Y {placement.FirstBeamY:0.###} m, Z {placement.Z:0.###} m";
            });

            RoundtripPayload withoutHinge = null;
            Check("scenario without hinge", () =>
            {
                int sourceCount = source.Count;
                withoutHinge = RoundtripScenarioBuilder.Build(source, false, NamePrefix);
                Require(source.Count == sourceCount, "the source model was modified");
                Require(withoutHinge.NewObjects.Count == 7, $"expected 7 new objects but found {withoutHinge.NewObjects.Count}");
                Require(withoutHinge.PointHinge == null, "a hinge was created without being requested");
                Require(
                    withoutHinge.ScratchModel.Count == withoutHinge.References.Count + withoutHinge.NewObjects.Count,
                    "scratch model does not hold the references and new objects");
                RequireNewObjectsUnique(source, withoutHinge.NewObjects);
                return $"{withoutHinge.NewObjects.Count} new objects, scratch model {withoutHinge.ScratchModel.Count} objects, no hinge";
            });

            RoundtripPayload withHinge = null;
            Check("scenario with hinge", () =>
            {
                withHinge = RoundtripScenarioBuilder.Build(source, true, NamePrefix);
                Require(withHinge.NewObjects.Count == 8, $"expected 8 new objects but found {withHinge.NewObjects.Count}");
                Require(
                    withHinge.PointHinge != null && withHinge.PointHinge.Member.Id == withHinge.ExistingSectionBeam.Id,
                    "hinge is not on the existing-section beam");
                RequireNewObjectsUnique(source, withHinge.NewObjects);
                return $"{withHinge.NewObjects.Count} new objects, hinge on '{withHinge.ExistingSectionBeam.Name}'";
            });

            AnalysisModel mergedWithoutHinge = null;
            RoundtripVerificationResult verifiedWithoutHinge = null;
            Check("verify merged model without hinge", () =>
            {
                mergedWithoutHinge = MergeCopy(source, withoutHinge.NewObjects);
                verifiedWithoutHinge = RoundtripVerifier.Verify(mergedWithoutHinge, false, NamePrefix);
                Require(verifiedWithoutHinge.HingeCount == 0, $"expected 0 hinges but found {verifiedWithoutHinge.HingeCount}");
                return Describe(verifiedWithoutHinge);
            });

            RoundtripVerificationResult verifiedWithHinge = null;
            Check("verify merged model with hinge", () =>
            {
                verifiedWithHinge = RoundtripVerifier.Verify(MergeCopy(source, withHinge.NewObjects), true, NamePrefix);
                Require(verifiedWithHinge.HingeCount == 1, $"expected 1 hinge but found {verifiedWithHinge.HingeCount}");
                return Describe(verifiedWithHinge);
            });

            Check("verify rejects source without test objects", () =>
            {
                string message = ExpectException<InvalidOperationException>(() => RoundtripVerifier.Verify(source, true, NamePrefix)).Message;
                return "rejected: " + message;
            });

            Check("verify rejects missing hinge", () =>
            {
                string message = ExpectException<InvalidOperationException>(() => RoundtripVerifier.Verify(mergedWithoutHinge, true, NamePrefix)).Message;
                Require(message.Contains("hinge was expected"), "unexpected message: " + message);
                return "rejected: " + message;
            });

            Check("repeated run gets _001 names and verifies the newest", () =>
            {
                AnalysisModel afterFirst = MergeCopy(source, withoutHinge.NewObjects);
                RoundtripPayload second = RoundtripScenarioBuilder.Build(afterFirst, false, NamePrefix);
                RoundtripVerificationResult result = RoundtripVerifier.Verify(MergeCopy(afterFirst, second.NewObjects), false, NamePrefix);
                Require(result.ExistingSectionBeam.Name == NamePrefix + "_Beam_ExistingSection_001", $"verified '{result.ExistingSectionBeam.Name}'");
                Require(result.RectangularSectionBeam.Name == NamePrefix + "_Beam_Rectangle_001", $"verified '{result.RectangularSectionBeam.Name}'");
                return Describe(result);
            });

            Check("legacy OpenApi prototype names", () =>
            {
                RoundtripPayload legacy = RoundtripScenarioBuilder.Build(source, false, NamePrefix);
                List<IAnalysisObject> objects = WithTestBeams(legacy, "OpenApiRoundTrip_ExistingSection_Beam_001", "OpenApiRoundTrip_Rectangle_Beam_001");
                return Describe(RoundtripVerifier.Verify(MergeCopy(source, objects), false));
            });

            Check("legacy ThirdParty prototype names", () =>
            {
                RoundtripPayload legacy = RoundtripScenarioBuilder.Build(source, false, NamePrefix);
                List<IAnalysisObject> objects = WithTestBeams(legacy, "TPTest_0a1b2c3d_BeamExistingSection", "TPTest_0a1b2c3d_BeamRectangularSection");
                return Describe(RoundtripVerifier.Verify(MergeCopy(source, objects), false, "TPTest_0a1b2c3d"));
            });

            Console.WriteLine("Verification report (with hinge):");
            Console.WriteLine(verifiedWithHinge.Summary);
        }

        private static void Check(string name, Func<string> body)
        {
            string detail;
            try
            {
                detail = body();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Check '{name}' failed: {exception.Message}", exception);
            }

            Console.WriteLine($"OK {name}: {detail}");
        }

        private static AnalysisModel BuildSourceModel()
        {
            var model = new AnalysisModel();
            using (var scope = new AnalysisDataModelBootstrapper().CreateThreadedScope())
            {
                IAnalysisModelService modelService = scope.GetService<IAnalysisModelService>();
                AddDefaults(model, modelService);
                new GeometryBuilder(model, modelService)
                    .AddNode("N1", 0, 0, 0)
                    .AddNode("N2", 4, 0, 0)
                    .AddNode("N3", 4, 5, 3)
                    .AddLineMember("B1", "N1", "N2", "HEA260", Member1DType.Beam, "Beams")
                    .Build();
            }

            ModelValidation.Validate(model, "building the self-check source model");
            return model;
        }

        private static AnalysisModel BuildMaterialsAndSectionsModel()
        {
            var model = new AnalysisModel();
            using (var scope = new AnalysisDataModelBootstrapper().CreateThreadedScope())
            {
                AddDefaults(model, scope.GetService<IAnalysisModelService>());
            }

            ModelValidation.Validate(model, "building the self-check materials and sections model");
            return model;
        }

        private static void AddDefaults(AnalysisModel model, IAnalysisModelService modelService)
        {
            new ModelInformationBuilder(model, modelService, "SelfCheckModel").Build();
            new MaterialBuilder(model, modelService).SetupDefaultMaterials().Build();
            new CrossSectionBuilder(model, modelService).SetupDefaultSections().Build();
        }

        private static AnalysisModel MergeCopy(AnalysisModel source, IEnumerable<IAnalysisObject> additions)
        {
            var merged = new AnalysisModel();
            AddItems(merged, source.Cast<IAnalysisObject>().ToList());
            AddItems(merged, additions.ToList());
            return merged;
        }

        private static void AddItems(AnalysisModel model, List<IAnalysisObject> items)
        {
            using (var scope = new AnalysisDataModelBootstrapper().CreateThreadedScope())
            {
                IDictionary<Guid, bool> results = scope.GetService<IAnalysisModelService>().AddItemsToModel(model, items);
                foreach (IAnalysisObject item in items)
                {
                    Require(
                        results != null && results.TryGetValue(item.Id, out bool added) && added,
                        $"ADM did not add {item.GetType().Name} '{item.Name}'");
                }
            }
        }

        private static void RequireNewObjectsUnique(AnalysisModel model, IEnumerable<IAnalysisObject> newObjects)
        {
            var ids = new HashSet<Guid>(model.Cast<IAnalysisObject>().Select(item => item.Id));
            var names = new HashSet<string>(
                model.Cast<IAnalysisObject>().Select(item => item.Name ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);
            foreach (IAnalysisObject item in newObjects)
            {
                Require(!ids.Contains(item.Id), $"id of '{item.Name}' already exists in the source model");
                Require(!names.Contains(item.Name ?? string.Empty), $"name '{item.Name}' already exists in the source model");
            }
        }

        private static int CountOf<T>(AnalysisModel model)
            where T : class, IAnalysisObject
        {
            return model.Cast<IAnalysisObject>().Count(item => item is T);
        }

        private static T ExpectException<T>(Action action)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (Exception exception) when (exception is T)
            {
                return (T)exception;
            }

            throw new InvalidOperationException($"Expected {typeof(T).Name} was not thrown.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static bool Near(double actual, double expected)
        {
            return Math.Abs(actual - expected) < 1e-9;
        }

        private static string Describe(RoundtripVerificationResult result)
        {
            return $"beams '{result.ExistingSectionBeam.Name}' and '{result.RectangularSectionBeam.Name}', section '{result.RectangularSection.Name}', material '{result.ExistingMaterial.Name}', hinges {result.HingeCount}";
        }

        private static List<IAnalysisObject> WithTestBeams(RoundtripPayload payload, string existingSectionBeamName, string rectangleBeamName)
        {
            List<IAnalysisObject> objects = payload.NewObjects
                .Where(item => !ReferenceEquals(item, payload.ExistingSectionBeam) && !ReferenceEquals(item, payload.RectangularSectionBeam))
                .ToList();
            objects.Add(CreateBeam(existingSectionBeamName, payload.Nodes[0], payload.Nodes[1], payload.ExistingSection));
            objects.Add(CreateBeam(rectangleBeamName, payload.Nodes[2], payload.Nodes[3], payload.TestSection));
            return objects;
        }

        private static StructuralCurveMember CreateBeam(string name, StructuralPointConnection start, StructuralPointConnection end, StructuralCrossSection section)
        {
            var curve = new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new[] { start, end });
            return new StructuralCurveMember(Guid.NewGuid(), name, new[] { curve }, section)
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new FlexibleEnum<Member1DType>(Member1DType.Beam),
                Layer = "Beams"
            };
        }
    }
}
