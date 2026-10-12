using System;
using System.Collections.Generic;
using CSInfrastructure;
using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Integration.Bootstrapper;
using ModelExchanger.AnalysisDataModel.Libraries;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using ModelExchanger.AnalysisDataModel.Subtypes;
using UnitsNet;

namespace SciaApi.AdmModeling.Roundtrip
{
    /// <summary>
    /// Builds the roundtrip test delta: a 300 x 500 mm rectangular section, four nodes, two beams and an optional point hinge.
    /// </summary>
    public static class RoundtripScenarioBuilder
    {
        /// <summary>
        /// Name prefix used when none is given.
        /// </summary>
        public const string DefaultNamePrefix = "Roundtrip";

        /// <summary>
        /// Builds the test objects on a new scratch model. <paramref name="sourceModel"/> is only read.
        /// </summary>
        /// <param name="sourceModel">Model read from SCIA Engineer.</param>
        /// <param name="withHinge">True to add a point hinge on the beam that uses the existing section.</param>
        /// <param name="namePrefix">Prefix for all new names; null or empty uses <see cref="DefaultNamePrefix"/>.
        /// A name that already exists in <paramref name="sourceModel"/> receives a _NNN suffix.</param>
        /// <exception cref="InvalidOperationException">No reusable pair or free space exists, or the ADM rejects the objects.</exception>
        public static RoundtripPayload Build(AnalysisModel sourceModel, bool withHinge, string namePrefix)
        {
            if (sourceModel == null)
            {
                throw new ArgumentNullException(nameof(sourceModel));
            }

            string prefix = string.IsNullOrWhiteSpace(namePrefix) ? DefaultNamePrefix : namePrefix.Trim();
            ExistingDefinitions existing = ExistingModelCatalog.Find(sourceModel);
            BeamPlacement placement = ExistingModelCatalog.FindPlacement(sourceModel);

            var reservedIds = new HashSet<Guid>();
            var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IAnalysisObject item in sourceModel)
            {
                reservedIds.Add(item.Id);
                if (!string.IsNullOrWhiteSpace(item.Name))
                {
                    reservedNames.Add(item.Name);
                }
            }

            var testSection = new StructuralParametricCrossSection(
                CreateUniqueId(reservedIds),
                CreateUniqueName(reservedNames, prefix + "_Rectangle_300x500"),
                existing.Material,
                ProfileLibraryId.Rectangle,
                new[]
                {
                    // ADM rectangle dimensions are height then width: 500 mm high by 300 mm wide.
                    Length.FromMillimeters(500),
                    Length.FromMillimeters(300)
                });

            var nodes = new List<StructuralPointConnection>
            {
                CreateNode(reservedIds, reservedNames, prefix + "_N1", placement.StartX, placement.FirstBeamY, placement.Z),
                CreateNode(reservedIds, reservedNames, prefix + "_N2", placement.EndX, placement.FirstBeamY, placement.Z),
                CreateNode(reservedIds, reservedNames, prefix + "_N3", placement.StartX, placement.SecondBeamY, placement.Z),
                CreateNode(reservedIds, reservedNames, prefix + "_N4", placement.EndX, placement.SecondBeamY, placement.Z)
            };

            StructuralCurveMember existingSectionBeam = CreateBeam(
                reservedIds,
                CreateUniqueName(reservedNames, prefix + "_Beam_ExistingSection"),
                nodes[0],
                nodes[1],
                existing.Section);

            StructuralCurveMember rectangularSectionBeam = CreateBeam(
                reservedIds,
                CreateUniqueName(reservedNames, prefix + "_Beam_Rectangle"),
                nodes[2],
                nodes[3],
                testSection);

            RelConnectsStructuralMember pointHinge = withHinge
                ? CreatePointHinge(reservedIds, CreateUniqueName(reservedNames, prefix + "_PointHinge"), existingSectionBeam)
                : null;

            var references = new List<IAnalysisObject>();
            if (sourceModel.ProjectInformation != null)
            {
                references.Add(sourceModel.ProjectInformation);
            }

            if (sourceModel.ModelInformation != null)
            {
                references.Add(sourceModel.ModelInformation);
            }

            references.Add(existing.Material);
            references.Add(existing.Section);

            var newObjects = new List<IAnalysisObject> { testSection };
            newObjects.AddRange(nodes);
            newObjects.Add(existingSectionBeam);
            newObjects.Add(rectangularSectionBeam);
            if (pointHinge != null)
            {
                newObjects.Add(pointHinge);
            }

            AnalysisModel scratchModel = CreateScratchModel(references, newObjects);
            ModelValidation.Validate(scratchModel, "building the roundtrip scenario");

            return new RoundtripPayload(
                scratchModel,
                references.AsReadOnly(),
                newObjects.AsReadOnly(),
                existing.Material,
                existing.Section,
                existing.SourceBeam,
                testSection,
                nodes.AsReadOnly(),
                existingSectionBeam,
                rectangularSectionBeam,
                pointHinge);
        }

        private static AnalysisModel CreateScratchModel(List<IAnalysisObject> references, List<IAnalysisObject> newObjects)
        {
            var scratchModel = new AnalysisModel();
            var items = new List<IAnalysisObject>(references);
            items.AddRange(newObjects);

            var bootstrapper = new AnalysisDataModelBootstrapper();
            using (var scope = bootstrapper.CreateThreadedScope())
            {
                IAnalysisModelService modelService = scope.GetService<IAnalysisModelService>();
                if (modelService == null)
                {
                    throw new InvalidOperationException("The ADM bootstrapper did not provide IAnalysisModelService.");
                }

                IDictionary<Guid, bool> additions = modelService.AddItemsToModel(scratchModel, items);
                foreach (IAnalysisObject item in items)
                {
                    if (additions == null || !additions.TryGetValue(item.Id, out bool added) || !added)
                    {
                        throw new InvalidOperationException(
                            $"The ADM builder rejected {item.GetType().Name} '{item.Name}' ({item.Id}).");
                    }
                }
            }

            return scratchModel;
        }

        private static StructuralPointConnection CreateNode(
            HashSet<Guid> reservedIds,
            HashSet<string> reservedNames,
            string name,
            double x,
            double y,
            double z)
        {
            return new StructuralPointConnection(
                CreateUniqueId(reservedIds),
                CreateUniqueName(reservedNames, name),
                Length.FromMeters(x),
                Length.FromMeters(y),
                Length.FromMeters(z));
        }

        private static StructuralCurveMember CreateBeam(
            HashSet<Guid> reservedIds,
            string name,
            StructuralPointConnection startNode,
            StructuralPointConnection endNode,
            StructuralCrossSection section)
        {
            var beamLines = new Curve<StructuralPointConnection>[]
            {
                new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new[] { startNode, endNode })
            };

            return new StructuralCurveMember(CreateUniqueId(reservedIds), name, beamLines, section)
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new FlexibleEnum<Member1DType>(Member1DType.Beam),
                Layer = "Beams"
            };
        }

        private static RelConnectsStructuralMember CreatePointHinge(
            HashSet<Guid> reservedIds,
            string name,
            StructuralCurveMember member)
        {
            var fixedTranslation = new Constraint<ForcePerLength?>(
                ConstraintType.Rigid,
                ForcePerLength.FromKilonewtonsPerMeter(1e+10));
            var fixedRotation = new Constraint<RotationalStiffness?>(
                ConstraintType.Rigid,
                RotationalStiffness.FromKilonewtonMetersPerRadian(1e+10));
            var freeRotation = new Constraint<RotationalStiffness?>(
                ConstraintType.Free,
                RotationalStiffness.FromKilonewtonMetersPerRadian(0));

            return new RelConnectsStructuralMember(CreateUniqueId(reservedIds), name, member)
            {
                Position = Position.Both,
                TranslationX = fixedTranslation,
                TranslationY = fixedTranslation,
                TranslationZ = fixedTranslation,
                RotationX = fixedRotation,
                RotationY = freeRotation,
                RotationZ = fixedRotation
            };
        }

        private static Guid CreateUniqueId(HashSet<Guid> reservedIds)
        {
            Guid id;
            do
            {
                id = Guid.NewGuid();
            }
            while (!reservedIds.Add(id));

            return id;
        }

        private static string CreateUniqueName(HashSet<string> reservedNames, string candidate)
        {
            if (reservedNames.Add(candidate))
            {
                return candidate;
            }

            for (int index = 1; ; index++)
            {
                string numbered = candidate + "_" + index.ToString("D3");
                if (reservedNames.Add(numbered))
                {
                    return numbered;
                }
            }
        }
    }
}
