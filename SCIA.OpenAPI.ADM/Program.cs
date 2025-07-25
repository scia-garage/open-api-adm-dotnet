using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Libraries;
using ModelExchanger.AnalysisDataModel.Loads;
using ModelExchanger.AnalysisDataModel.Subtypes;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using ModelExchanger.AnalysisDataModel.StructuralReferences.Curves;
using ModelExchanger.AnalysisDataModel.StructuralReferences.Points;
using SciaTools.Kernel.ModelExchangerExtension.Models.Exchange;
using System;
using SCIA.OpenAPI.Results;
using Results64Enums;
using System.IO;
using System.Reflection;
using SCIA.OpenAPI.Utils;
using SciaTools.AdmToAdm.AdmSignalR.Models.ModelModification;
using System.Collections.Generic;
using SciaTools.Kernel.Common.Implementation.App;
using SciaTools.Kernel.ModelExchangerExtension.Integration.Modules;
using SciaTools.Kernel.ModelExchangerExtension.Contracts.AnalysisModelModifications;
using SciaTools.Kernel.ModelExchangerExtension.Models.AnalysisModelModifications;
using SciaTools.Kernel.ModelExchangerExtension.Contracts.Services;
using ModelExchanger.AnalysisDataModel.Implementation.Repositories;
using SCIA.OpenAPI;
using SciaTools.Kernel.ModelExchangerExtension.Contracts.Ioc;
using Microsoft.Win32;
using System.Diagnostics;

namespace OpenAPIAndADMDemo
{
    class Program
    {
        private static Guid LC1Id { get; } = Guid.NewGuid();
        private static string N1Name { get; } = "N1";
        private static string B1Name { get; } = "B1";
        private static Guid C1Id { get; } = Guid.NewGuid();
        private static string S1Name { get; } = "S1";

        private static string GetAppPath(string version)
        {
            string registryPath = $@"Software\SCIA\ESA\{version}\Admin\Dir";

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key != null)
                {
                    object systemValue = key.GetValue("SYSTEM");
                    if (systemValue != null)
                    {
                        return systemValue.ToString();
                    }
                }
            }
            throw new InvalidOperationException($"Registry key {registryPath} not found.");
        }

        /// <summary>
        /// Path to Scia engineer
        /// </summary>
        static private string SciaEngineerFullPath => GetAppPath("25.0");


        /// <summary>
        /// Path to SCIA Engineer temp
        /// </sumamary>
        static private string SciaEngineerTempPath => GetTempPath("25.0");

        private static string GetTempPath(string version)
        {
            string registryPath = $@"Software\SCIA\ESA\{version}\Admin\Dir";

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key != null)
                {
                    object tempValue = key.GetValue("Temp");
                    if (tempValue != null)
                    {
                        return tempValue.ToString();
                    }
                }
            }
            throw new InvalidOperationException($"Registry key {registryPath} not found.");
        }

        static private string AppLogPath => GetThisAppLogPath();

        static private string GetThisAppLogPath()
        {
            return @"C:\temp\SCIA_OpenAPI_Logs"; // Folder for storing of log files for this console application
        }

        private static void DeleteTemp()
        {

            if (Directory.Exists(SciaEngineerTempPath))
            {
                Directory.Delete(SciaEngineerTempPath, true);
            }

        }
        private static void KillSCIAEngineerOrphanRuns()
        {

            foreach (var process in Process.GetProcessesByName("EsaStartupScreen"))
            {
                process.Kill();
                Console.WriteLine($"Kill old EsaStartupScreen!");
                System.Threading.Thread.Sleep(1000);
            }
            foreach (var process in Process.GetProcessesByName("Esa"))
            {
                process.Kill();
                Console.WriteLine($"Kill old SEN!");
                System.Threading.Thread.Sleep(5000);
            }

        }

        /// <summary>
        /// Assembly resolve method has to be call here
        /// </summary>
        private static void SciaOpenApiAssemblyResolve()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string dllName = args.Name.Substring(0, args.Name.IndexOf(",")) + ".dll";
                string dllFullPath = Path.Combine(SciaEngineerFullPath, dllName);
                if (!File.Exists(dllFullPath))
                {
                    //return null;
                    dllFullPath = Path.Combine(SciaEngineerFullPath, "OpenAPI_dll", dllName);
                }
                if (!File.Exists(dllFullPath))
                {
                    return null;
                }
                return Assembly.LoadFrom(dllFullPath);
            };
        }
        static void RunSCIAOpenAPI_simple()
        {
            using (SCIA.OpenAPI.Environment env = new SCIA.OpenAPI.Environment(SciaEngineerFullPath, AppLogPath, "1.0.0.0"))//path to the location of your installation and temp path for logs)
            {
                // Create an empty project file
                SciaFileGetter fileGetter = new SciaFileGetter();
                var EsaFile = fileGetter.PrepareBasicEmptyFile(@"C:/TEMP/");//path where the template file "template.esa" is created
                if (!File.Exists(EsaFile))
                {
                    throw new InvalidOperationException($"File from manifest resource is not created ! Temp: {env.AppTempPath}");
                }

                //Run SCIA Engineer application
                bool openedSE = env.RunSCIAEngineer(SCIA.OpenAPI.Environment.GuiMode.ShowWindowShow);
                if (!openedSE)
                {
                    return;
                }
                Console.WriteLine($"SEN opened");
                

                EsaProject proj = env.OpenProject(EsaFile);
                if (proj == null)
                {
                    return;
                }
                Console.WriteLine($"Proj opened");

                CreateModel(proj.Model);

                proj.Model.RefreshModel_ToSCIAEngineer();

                Console.WriteLine($"My model sent to SEn");



                // Run calculation
                proj.RunCalculation();
                Console.WriteLine($"My model calculate");

                //Initialize Results API
                ResultsAPI rapi = proj.Model.InitializeResultsAPI();
                if (rapi != null)
                {
                    //Create container for 1D results
                    Result IntFor1Db1 = new Result();
                    //Results key for internal forces on beam 1
                    ResultKey keyIntFor1Db1 = new ResultKey
                    {
                        CaseType = eDsElementType.eDsElementType_LoadCase,
                        CaseId = LC1Id,
                        EntityType = eDsElementType.eDsElementType_Beam,
                        EntityName = B1Name,
                        Dimension = eDimension.eDim_1D,
                        ResultType = eResultType.eFemBeamInnerForces,
                        CoordSystem = eCoordSystem.eCoordSys_Local
                    };
                    //Load 1D results based on results key
                    IntFor1Db1 = rapi.LoadResult(keyIntFor1Db1);
                    if (IntFor1Db1 != null)
                    {
                        Console.WriteLine(IntFor1Db1.GetTextOutput());
                        var N = IntFor1Db1.GetMagnitudeName(0);
                        var Nvalue = IntFor1Db1.GetValue(0, 0);
                        Console.WriteLine(N);
                        Console.WriteLine(Nvalue);
                    }
                    //combination
                    //Create container for 1D results
                    Result IntFor1Db1Combi = new Result();
                    //Results key for internal forces on beam 1
                    ResultKey keyIntFor1Db1Combi = new ResultKey
                    {
                        EntityType = eDsElementType.eDsElementType_Beam,
                        EntityName = B1Name,
                        CaseType = eDsElementType.eDsElementType_Combination,
                        CaseId = C1Id,
                        Dimension = eDimension.eDim_1D,
                        ResultType = eResultType.eFemBeamInnerForces,
                        CoordSystem = eCoordSystem.eCoordSys_Local
                    };
                    // Load 1D results based on results key
                    IntFor1Db1Combi = rapi.LoadResult(keyIntFor1Db1Combi);
                    if (IntFor1Db1Combi != null)
                    {
                        Console.WriteLine(IntFor1Db1Combi.GetTextOutput());
                    }
                    //Results key for reaction on node 1
                    ResultKey keyReactionsSu1 = new ResultKey
                    {
                        CaseType = eDsElementType.eDsElementType_LoadCase,
                        CaseId = LC1Id,
                        EntityType = eDsElementType.eDsElementType_Node,
                        EntityName = N1Name,
                        Dimension = eDimension.eDim_reactionsPoint,
                        ResultType = eResultType.eReactionsNodes,
                        CoordSystem = eCoordSystem.eCoordSys_Global
                    };
                    Result reactionsSu1 = new Result();
                    reactionsSu1 = rapi.LoadResult(keyReactionsSu1);
                    if (reactionsSu1 != null)
                    {
                        Console.WriteLine(reactionsSu1.GetTextOutput());
                    }

                    Result Def2Ds1 = new Result();
                    // Results key for internal forces on slab
                    ResultKey keyDef2Ds1 = new ResultKey
                    {
                        CaseType = eDsElementType.eDsElementType_LoadCase,
                        CaseId = LC1Id,
                        EntityType = eDsElementType.eDsElementType_Slab,
                        EntityName = S1Name,
                        Dimension = eDimension.eDim_2D,
                        ResultType = eResultType.eFemDeformations,
                        CoordSystem = eCoordSystem.eCoordSys_Local
                    };

                    Def2Ds1 = rapi.LoadResult(keyDef2Ds1);
                    if (Def2Ds1 != null)
                    {
                        Console.WriteLine(Def2Ds1.GetTextOutput());

                        double maxvalue = 0;
                        double pivot;
                        for (int i = 0; i < Def2Ds1.GetMeshElementCount(); i++)
                        {
                            pivot = Def2Ds1.GetValue(2, i);
                            if (System.Math.Abs(pivot) > System.Math.Abs(maxvalue))
                            {
                                maxvalue = pivot;

                            }
                        }
                        Console.WriteLine("Maximum deformation on slab:");
                        Console.WriteLine(maxvalue);
                    }

                }
                Console.WriteLine($"Press key to exit");
                Console.ReadKey();
                proj.CloseProject(SCIA.OpenAPI.SaveMode.SaveChangesNo);
                env.Dispose();
            }
        }
        static private object SciaOpenApiWorker(SCIA.OpenAPI.Environment env)
        {
            //Run SCIA Engineer application
            bool openedSE = env.RunSCIAEngineer(SCIA.OpenAPI.Environment.GuiMode.ShowWindowShow);
            if (!openedSE)
            {
                throw new InvalidOperationException($"SCIA Engineer not started");
            }
            Console.WriteLine($"SEn opened");
            SciaFileGetter fileGetter = new SciaFileGetter();
            var EsaFile = fileGetter.PrepareBasicEmptyFile(@"C:/TEMP/");//path where the template file "template.esa" is created
            if (!File.Exists(EsaFile))
            {
                throw new InvalidOperationException($"File from manifest resource is not created ! Temp: {env.AppTempPath}");
            }

            SCIA.OpenAPI.EsaProject proj = env.OpenProject(EsaFile);
            //SCIA.OpenAPI.EsaProject proj = env.OpenProject(SciaEngineerProjecTemplate);
            if (proj == null)
            {
                throw new InvalidOperationException($"File from manifest resource is not opened ! Temp: {env.AppTempPath}");
            }
            Console.WriteLine($"Proj opened");


            CreateModel(proj.Model);
 
            proj.Model.RefreshModel_ToSCIAEngineer();

            Console.WriteLine($"My model sent to SEn");


            // Run calculation
            proj.RunCalculation();
            Console.WriteLine($"My model calculate");

            //storage for results
            OpenApiE2EResults storage = new OpenApiE2EResults();

            //Initialize Results API
            using (ResultsAPI resultsApi = proj.Model.InitializeResultsAPI())
            {
                if (resultsApi == null)
                {
                    return storage;
                }
                {
                    OpenApiE2EResult beamB1InnerForLc = new OpenApiE2EResult("beamB1InnerForcesLC1")
                    {
                        ResultKey = new ResultKey
                        {
                            EntityType = eDsElementType.eDsElementType_Beam,
                            EntityName = B1Name,
                            CaseType = eDsElementType.eDsElementType_LoadCase,
                            CaseId = LC1Id,
                            Dimension = eDimension.eDim_1D,
                            ResultType = eResultType.eFemBeamInnerForces,
                            CoordSystem = eCoordSystem.eCoordSys_Local
                        }
                    };
                    beamB1InnerForLc.Result = resultsApi.LoadResult(beamB1InnerForLc.ResultKey);
                    storage.SetResult(beamB1InnerForLc);
                }
                {
                    OpenApiE2EResult beamB1IDeformationLc = new OpenApiE2EResult("beamB1DeformationsLC1")
                    {
                        ResultKey = new ResultKey
                        {
                            EntityType = eDsElementType.eDsElementType_Beam,
                            EntityName = B1Name,
                            CaseType = eDsElementType.eDsElementType_LoadCase,
                            CaseId = LC1Id,
                            Dimension = eDimension.eDim_1D,
                            ResultType = eResultType.eFemBeamDeformation,
                            CoordSystem = eCoordSystem.eCoordSys_Local
                        }
                    };
                    beamB1IDeformationLc.Result = resultsApi.LoadResult(beamB1IDeformationLc.ResultKey);
                    storage.SetResult(beamB1IDeformationLc);
                }
                {
                    OpenApiE2EResult beamB1RelIDeformationLc = new OpenApiE2EResult("beamB1RelativeDeformationsLC1")
                    {
                        ResultKey = new ResultKey
                        {
                            EntityType = eDsElementType.eDsElementType_Beam,
                            EntityName = B1Name,
                            CaseType = eDsElementType.eDsElementType_LoadCase,
                            CaseId = LC1Id,
                            Dimension = eDimension.eDim_1D,
                            ResultType = eResultType.eFemBeamRelativeDeformation,
                            CoordSystem = eCoordSystem.eCoordSys_Local
                        }
                    };
                    beamB1RelIDeformationLc.Result = resultsApi.LoadResult(beamB1RelIDeformationLc.ResultKey);
                    storage.SetResult(beamB1RelIDeformationLc);
                }
                //{
                //    OpenApiE2EResult beamInnerForcesCombi = new OpenApiE2EResult("beamInnerForcesCombi")
                //    {
                //        ResultKey = new ResultKey
                //        {
                //            EntityType = eDsElementType.eDsElementType_Beam,
                //            EntityName = B1Name,
                //            CaseType = eDsElementType.eDsElementType_Combination,
                //            CaseId = C1Id,
                //            Dimension = eDimension.eDim_1D,
                //            ResultType = eResultType.eFemBeamInnerForces,
                //            CoordSystem = eCoordSystem.eCoordSys_Local
                //        }
                //    };
                //    beamInnerForcesCombi.Result = resultsApi.LoadResult(beamInnerForcesCombi.ResultKey);
                //    storage.SetResult(beamInnerForcesCombi);
                //}


                {
                    OpenApiE2EResult slabInnerForces = new OpenApiE2EResult("slabInnerForces")
                    {
                        ResultKey = new ResultKey
                        {
                            EntityType = eDsElementType.eDsElementType_Slab,
                            EntityName = S1Name,
                            CaseType = eDsElementType.eDsElementType_LoadCase,
                            CaseId = LC1Id,
                            Dimension = eDimension.eDim_2D,
                            ResultType = eResultType.eFemInnerForces,
                            CoordSystem = eCoordSystem.eCoordSys_Local
                        }
                    };
                    slabInnerForces.Result = resultsApi.LoadResult(slabInnerForces.ResultKey);
                    storage.SetResult(slabInnerForces);
                }
                {
                    OpenApiE2EResult slabDeformations = new OpenApiE2EResult("slabDeformations")
                    {
                        ResultKey = new ResultKey
                        {
                            EntityType = eDsElementType.eDsElementType_Slab,
                            EntityName = S1Name,
                            CaseType = eDsElementType.eDsElementType_LoadCase,
                            CaseId = LC1Id,
                            Dimension = eDimension.eDim_2D,
                            ResultType = eResultType.eFemDeformations,
                            CoordSystem = eCoordSystem.eCoordSys_Local
                        }
                    };
                    slabDeformations.Result = resultsApi.LoadResult(slabDeformations.ResultKey);
                    storage.SetResult(slabDeformations);
                }
                {
                    OpenApiE2EResult slabStresses = new OpenApiE2EResult("slabStresses")
                    {
                        ResultKey = new ResultKey
                        {
                            EntityType = eDsElementType.eDsElementType_Slab,
                            EntityName = S1Name,
                            CaseType = eDsElementType.eDsElementType_LoadCase,
                            CaseId = LC1Id,
                            Dimension = eDimension.eDim_2D,
                            ResultType = eResultType.eFemStress,
                            CoordSystem = eCoordSystem.eCoordSys_Local
                        }
                    };
                    slabStresses.Result = resultsApi.LoadResult(slabStresses.ResultKey);
                    storage.SetResult(slabStresses);
                }
                {
                    OpenApiE2EResult slabStrains = new OpenApiE2EResult("slabStrains")
                    {
                        ResultKey = new ResultKey
                        {
                            EntityType = eDsElementType.eDsElementType_Slab,
                            EntityName = S1Name,
                            CaseType = eDsElementType.eDsElementType_LoadCase,
                            CaseId = LC1Id,
                            Dimension = eDimension.eDim_2D,
                            ResultType = eResultType.eFemStrains,
                            CoordSystem = eCoordSystem.eCoordSys_Local
                        }
                    };
                    slabStrains.Result = resultsApi.LoadResult(slabStrains.ResultKey);
                    storage.SetResult(slabStrains);
                }
                {
                    OpenApiE2EResult slabInnerForcesExtended = new OpenApiE2EResult("slabInnerForcesExtended")
                    {
                        ResultKey = new ResultKey
                        {
                            EntityType = eDsElementType.eDsElementType_Slab,
                            EntityName = S1Name,
                            CaseType = eDsElementType.eDsElementType_LoadCase,
                            CaseId = LC1Id,
                            Dimension = eDimension.eDim_2D,
                            ResultType = eResultType.eFemInnerForces_Extended,
                            CoordSystem = eCoordSystem.eCoordSys_Local
                        }
                    };
                    slabInnerForcesExtended.Result = resultsApi.LoadResult(slabInnerForcesExtended.ResultKey);
                    storage.SetResult(slabInnerForcesExtended);
                }

                //{
                //    OpenApiE2EResult reactions = new OpenApiE2EResult("ReactionsN1")
                //    {
                //        ResultKey = new ResultKey
                //        {
                //            CaseType = eDsElementType.eDsElementType_LoadCase,
                //            CaseId = Lc1Id,
                //            EntityType = eDsElementType.eDsElementType_Node,
                //            EntityName = "n1",
                //            Dimension = eDimension.eDim_reactionsPoint,
                //            ResultType = eResultType.eReactionsNodes,
                //            CoordSystem = eCoordSystem.eCoordSys_Global
                //        }
                //    };
                //    reactions.Result = resultsApi.LoadResult(reactions.ResultKey);
                //    storage.SetResult(reactions);
                //}
                //{
                //    OpenApiE2EResult reactions = new OpenApiE2EResult("ReactionsSu1")
                //    {
                //        ResultKey = new ResultKey
                //        {
                //            CaseType = eDsElementType.eDsElementType_LoadCase,
                //            CaseId = Lc1Id,
                //            EntityType = eDsElementType.eDsElementType_PointSupportPoint,
                //            EntityName = "Su1",
                //            Dimension = eDimension.eDim_reactionsPoint,
                //            ResultType = eResultType.eResultTypeReactionsSupport0D,
                //            CoordSystem = eCoordSystem.eCoordSys_Global,

                //        }
                //    };
                //    reactions.Result = resultsApi.LoadResult(reactions.ResultKey);
                //    storage.SetResult(reactions);
                //}
                //{
                //    OpenApiE2EResult ReactionslinSupBeam = new OpenApiE2EResult("ReactionslinSupBeam")
                //    {
                //        ResultKey = new ResultKey
                //        {
                //            EntityType = eDsElementType.eDsElementType_LineSupportLine,
                //            EntityName = "linSupBeam",
                //            Dimension = eDimension.eDim_reactionsLine,
                //            CoordSystem = eCoordSystem.eCoordSys_Global,
                //            CaseType = eDsElementType.eDsElementType_LoadCase,
                //            CaseId = Lc1Id,
                //            ResultType = eResultType.eResultTypeReactionsSupport1D,

                //        }
                //    };
                //    ReactionslinSupBeam.Result = resultsApi.LoadResult(ReactionslinSupBeam.ResultKey);
                //    storage.SetResult(ReactionslinSupBeam);
                //}             

                return storage;
            }
        }

        private static void CreateModel(Structure model)
        {
            // info about Project 
            ProjectInformation projectInformation = new ProjectInformation(Guid.NewGuid(), "ProjectX")
            {
                BuildingType = "SimpleFrame",
                Location = "39XG+P7 Praha",
                LastUpdate = DateTime.Today,
                Status = "Draft",
                ProjectType = "New construction"
            };



            // info about Model ModelExchanger.AnalysisDataModel.ModelInformation
            ModelInformation modelInformation = new ModelInformation(Guid.NewGuid(), "ModelOne")
            {
                Discipline = "Static",
                Owner = "JB",
                LevelOfDetail = "200",
                LastUpdate = DateTime.Today,
                SourceApplication = "OpenAPI",
                RevisionNumber = "1",
                SourceCompany = "SCIA",
                SystemOfUnits = SystemOfUnits.Metric

            };
            ResultOfPartialAddToAnalysisModel addResult = model.CreateAdmObject(projectInformation, modelInformation);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            string conMatGrade = "C20/25";

            StructuralMaterial concrete = new StructuralMaterial(Guid.NewGuid(), "Concrete", MaterialType.Concrete, conMatGrade);


            string steelMatGrade = "S 235";
            StructuralMaterial steel = new StructuralMaterial(Guid.NewGuid(), "Steel", MaterialType.Steel, steelMatGrade);
            addResult = model.CreateAdmObject(concrete, steel);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            Console.WriteLine($"Materials created in ADM");

            //Create cross-sections in local ADM
            string steelProfile = "HEA260";

            StructuralCrossSection steelprofile = new StructuralManufacturedCrossSection(Guid.NewGuid(), steelProfile, steel, steelProfile, FormCode.ISection, DescriptionId.EuropeanIBeam);

            addResult = model.CreateAdmObject(steelprofile);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            
            double heigth = 600;
           
            double width = 300;
            StructuralCrossSection concreteRectangle = new StructuralParametricCrossSection(Guid.NewGuid(), "Concrete", concrete, ProfileLibraryId.Rectangle, new UnitsNet.Length[2] { UnitsNet.Length.FromMillimeters(heigth), UnitsNet.Length.FromMillimeters(width) });
            addResult = model.CreateAdmObject(concreteRectangle);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            Console.WriteLine($"CSSs created in ADM");

            
            double a = 4.0;
           
            double b = 5.0;
           
            double c = 3.0;


            StructuralPointConnection N1 = new StructuralPointConnection(Guid.NewGuid(), N1Name, UnitsNet.Length.FromMeters(0), UnitsNet.Length.FromMeters(0), UnitsNet.Length.FromMeters(0));
            addResult = model.CreateAdmObject(N1);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N2 = new StructuralPointConnection(Guid.NewGuid(), "N2", UnitsNet.Length.FromMeters(a), UnitsNet.Length.FromMeters(0), UnitsNet.Length.FromMeters(0));
            addResult = model.CreateAdmObject(N2);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N3 = new StructuralPointConnection(Guid.NewGuid(), "N3", UnitsNet.Length.FromMeters(a), UnitsNet.Length.FromMeters(b), UnitsNet.Length.FromMeters(0));
            addResult = model.CreateAdmObject(N3);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N4 = new StructuralPointConnection(Guid.NewGuid(), "N4", UnitsNet.Length.FromMeters(0), UnitsNet.Length.FromMeters(b), UnitsNet.Length.FromMeters(0));
            addResult = model.CreateAdmObject(N4);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N5 = new StructuralPointConnection(Guid.NewGuid(), "N5", UnitsNet.Length.FromMeters(0), UnitsNet.Length.FromMeters(0), UnitsNet.Length.FromMeters(c));
            addResult = model.CreateAdmObject(N5);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N6 = new StructuralPointConnection(Guid.NewGuid(), "N6", UnitsNet.Length.FromMeters(a), UnitsNet.Length.FromMeters(0), UnitsNet.Length.FromMeters(c));
            addResult = model.CreateAdmObject(N6);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N7 = new StructuralPointConnection(Guid.NewGuid(), "N7", UnitsNet.Length.FromMeters(a), UnitsNet.Length.FromMeters(b), UnitsNet.Length.FromMeters(c));
            addResult = model.CreateAdmObject(N7);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N8 = new StructuralPointConnection(Guid.NewGuid(), "N8", UnitsNet.Length.FromMeters(0), UnitsNet.Length.FromMeters(b), UnitsNet.Length.FromMeters(c));
            addResult = model.CreateAdmObject(N8);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            var beamB1lines = new Curve<StructuralPointConnection>[1] { new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N1, N5 }) };
            StructuralCurveMember B1 = new StructuralCurveMember(Guid.NewGuid(), B1Name, beamB1lines, steelprofile)
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new CSInfrastructure.FlexibleEnum<Member1DType>(Member1DType.Column),
                Layer = "Columns",
            };
            addResult = model.CreateAdmObject(B1);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            var beamB2lines = new Curve<StructuralPointConnection>[1] { new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N2, N6 }) };
            StructuralCurveMember B2 = new StructuralCurveMember(Guid.NewGuid(), "B2", beamB2lines, steelprofile)
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new CSInfrastructure.FlexibleEnum<Member1DType>(Member1DType.Column),
                Layer = "Columns",
            };
            addResult = model.CreateAdmObject(B2);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            var beamB3lines = new Curve<StructuralPointConnection>[1] { new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N3, N7 }) };
            StructuralCurveMember B3 = new StructuralCurveMember(Guid.NewGuid(), "B3", beamB3lines, steelprofile)
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new CSInfrastructure.FlexibleEnum<Member1DType>(Member1DType.Column),
                Layer = "Columns",
            };
            addResult = model.CreateAdmObject(B3);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            var beamB4lines = new Curve<StructuralPointConnection>[1] { new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N4, N8 }) };
            StructuralCurveMember B4 = new StructuralCurveMember(Guid.NewGuid(), "B4", beamB4lines, steelprofile)
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new CSInfrastructure.FlexibleEnum<Member1DType>(Member1DType.Column),
                Layer = "Columns",
            };
            addResult = model.CreateAdmObject(B4);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            var beamB5lines = new Curve<StructuralPointConnection>[1] { new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N5, N6 }) };
            StructuralCurveMember B5 = new StructuralCurveMember(Guid.NewGuid(), "B5", beamB5lines, steelprofile)
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new CSInfrastructure.FlexibleEnum<Member1DType>(Member1DType.Beam),
                Layer = "Beams",
            };
            addResult = model.CreateAdmObject(B5);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            var beamB6lines = new Curve<StructuralPointConnection>[1] { new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N6, N7 }) };
            StructuralCurveMember B6 = new StructuralCurveMember(Guid.NewGuid(), "B6", beamB6lines, steelprofile)
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new CSInfrastructure.FlexibleEnum<Member1DType>(Member1DType.Beam),
                Layer = "Beams",
            };
            addResult = model.CreateAdmObject(B6);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            var beamB7lines = new Curve<StructuralPointConnection>[1] { new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N7, N8 }) };
            StructuralCurveMember B7 = new StructuralCurveMember(Guid.NewGuid(), "B7", beamB7lines, steelprofile)
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new CSInfrastructure.FlexibleEnum<Member1DType>(Member1DType.Beam),
                Layer = "Beams",
            };
            addResult = model.CreateAdmObject(B7);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            var beamB8lines = new Curve<StructuralPointConnection>[1] { new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N8, N5 }) };
            StructuralCurveMember B8 = new StructuralCurveMember(Guid.NewGuid(), "B8", beamB8lines, steelprofile)
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new CSInfrastructure.FlexibleEnum<Member1DType>(Member1DType.Beam),
                Layer = "Beams",
            };
            addResult = model.CreateAdmObject(B8);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            Constraint<UnitsNet.RotationalStiffness?> FreeRotation = new Constraint<UnitsNet.RotationalStiffness?>(ConstraintType.Free, UnitsNet.RotationalStiffness.FromKilonewtonMetersPerRadian(0));
            Constraint<UnitsNet.RotationalStiffness?> FixedRotation = new Constraint<UnitsNet.RotationalStiffness?>(ConstraintType.Rigid, UnitsNet.RotationalStiffness.FromKilonewtonMetersPerRadian(1e+10));
            Constraint<UnitsNet.ForcePerLength?> FixedTranslation = new Constraint<UnitsNet.ForcePerLength?>(ConstraintType.Rigid, UnitsNet.ForcePerLength.FromKilonewtonsPerMeter(1e+10));
            Constraint<UnitsNet.ForcePerLength?> FreeTranslation = new Constraint<UnitsNet.ForcePerLength?>(ConstraintType.Free, UnitsNet.ForcePerLength.FromKilonewtonsPerMeter(0));
            Constraint<UnitsNet.Pressure?> FixedTranslationLine = new Constraint<UnitsNet.Pressure?>(ConstraintType.Rigid, UnitsNet.Pressure.FromKilopascals(1e+10));
            Constraint<UnitsNet.RotationalStiffnessPerLength?> FreeRotationLine = new Constraint<UnitsNet.RotationalStiffnessPerLength?>(ConstraintType.Free, UnitsNet.RotationalStiffnessPerLength.FromKilonewtonMetersPerRadianPerMeter(0));

            StructuralPointSupport PS1 = new StructuralPointSupport(Guid.NewGuid(), "SPS1", N1)
            {
                RotationX = FreeRotation,
                RotationY = FixedRotation,
                TranslationX = FixedTranslation,
                TranslationY = FixedTranslation,
                TranslationZ = FixedTranslation
            };

            StructuralPointSupport PS2 = new StructuralPointSupport(Guid.NewGuid(), "SPS2", N2)
            {
                RotationX = FreeRotation,
                RotationY = FixedRotation,
                TranslationX = FixedTranslation,
                TranslationY = FixedTranslation,
                TranslationZ = FixedTranslation
            };
            StructuralPointSupport PS3 = new StructuralPointSupport(Guid.NewGuid(), "SPS3", N3)
            {
                RotationX = FreeRotation,
                RotationY = FixedRotation,
                TranslationX = FixedTranslation,
                TranslationY = FixedTranslation,
                TranslationZ = FixedTranslation
            };
            StructuralPointSupport PS4 = new StructuralPointSupport(Guid.NewGuid(), "SPS4", N4)
            {
                RotationX = FreeRotation,
                RotationY = FixedRotation,
                TranslationX = FixedTranslation,
                TranslationY = FixedTranslation,
                TranslationZ = FixedTranslation
            };
            addResult = model.CreateAdmObject(PS1, PS2, PS3, PS4);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            
            double thickness = 0.3;
            var edgecurves = new Curve<StructuralPointConnection>[4] {
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N5, N6 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N6, N7 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N7, N8 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N8, N5 })
                };
            StructuralSurfaceMember S1 = new StructuralSurfaceMember(Guid.NewGuid(), S1Name, edgecurves, concrete, UnitsNet.Length.FromMeters(thickness))
            {
                Type = new CSInfrastructure.FlexibleEnum<Member2DType>(Member2DType.Plate),
                Behaviour = Member2DBehaviour.Isotropic,
                Alignment = Member2DAlignment.Centre,
                Shape = Member2DShape.Flat
            };
            addResult = model.CreateAdmObject(S1);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

           
            double lengthOpening = 1.0;
            
            double withOpening = 1.0;

            StructuralPointConnection N9 = new StructuralPointConnection(Guid.NewGuid(), "N9", UnitsNet.Length.FromMeters(0.5 * a - 0.5 * lengthOpening), UnitsNet.Length.FromMeters(0.5 * b - 0.5 * withOpening), UnitsNet.Length.FromMeters(c));
            addResult = model.CreateAdmObject(N9);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N10 = new StructuralPointConnection(Guid.NewGuid(), "N10", UnitsNet.Length.FromMeters(0.5 * a + 0.5 * lengthOpening), UnitsNet.Length.FromMeters(0.5 * b - 0.5 * withOpening), UnitsNet.Length.FromMeters(c));
            addResult = model.CreateAdmObject(N10);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N11 = new StructuralPointConnection(Guid.NewGuid(), "N11", UnitsNet.Length.FromMeters(0.5 * a + 0.5 * lengthOpening), UnitsNet.Length.FromMeters(0.5 * b + 0.5 * withOpening), UnitsNet.Length.FromMeters(c));
            addResult = model.CreateAdmObject(N11);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N12 = new StructuralPointConnection(Guid.NewGuid(), "N12", UnitsNet.Length.FromMeters(0.5 * a - 0.5 * lengthOpening), UnitsNet.Length.FromMeters(0.5 * b + 0.5 * withOpening), UnitsNet.Length.FromMeters(c));
            addResult = model.CreateAdmObject(N12);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            var openingEdges = new Curve<StructuralPointConnection>[4] {
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N9, N10 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N10, N11 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N11, N12 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N12, N9 })
                };
            StructuralSurfaceMemberOpening O1S1 = new StructuralSurfaceMemberOpening(Guid.NewGuid(), "O1", S1, openingEdges);
            addResult = model.CreateAdmObject(O1S1);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }


            var edgecurvesS2 = new Curve<StructuralPointConnection>[4] {
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N1, N2 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N2, N3 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N3, N4 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N4, N1 })
                };
            StructuralSurfaceMember S2 = new StructuralSurfaceMember(Guid.NewGuid(), "S2", edgecurvesS2, concrete, UnitsNet.Length.FromMeters(thickness))
            {
                Type = new CSInfrastructure.FlexibleEnum<Member2DType>(Member2DType.Plate),
                Behaviour = Member2DBehaviour.Isotropic,
                Alignment = Member2DAlignment.Centre,
                Shape = Member2DShape.Flat
            };
            addResult = model.CreateAdmObject(S2);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            StructuralPointConnection N13 = new StructuralPointConnection(Guid.NewGuid(), "N13", UnitsNet.Length.FromMeters(0.5 * a - 0.5 * lengthOpening), UnitsNet.Length.FromMeters(0.5 * b - 0.5 * withOpening), UnitsNet.Length.FromMeters(0));
            addResult = model.CreateAdmObject(N13);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N14 = new StructuralPointConnection(Guid.NewGuid(), "N14", UnitsNet.Length.FromMeters(0.5 * a + 0.5 * lengthOpening), UnitsNet.Length.FromMeters(0.5 * b - 0.5 * withOpening), UnitsNet.Length.FromMeters(0));
            addResult = model.CreateAdmObject(N14);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N15 = new StructuralPointConnection(Guid.NewGuid(), "N15", UnitsNet.Length.FromMeters(0.5 * a + 0.5 * lengthOpening), UnitsNet.Length.FromMeters(0.5 * b + 0.5 * withOpening), UnitsNet.Length.FromMeters(0));
            addResult = model.CreateAdmObject(N15);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            StructuralPointConnection N16 = new StructuralPointConnection(Guid.NewGuid(), "N16", UnitsNet.Length.FromMeters(0.5 * a - 0.5 * lengthOpening), UnitsNet.Length.FromMeters(0.5 * b + 0.5 * withOpening), UnitsNet.Length.FromMeters(0));
            addResult = model.CreateAdmObject(N16);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            var regionEdges = new Curve<StructuralPointConnection>[4] {
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N13, N14 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N14, N15 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N15, N16 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N16, N13 })
                };
            StructuralSurfaceMemberRegion SMR = new StructuralSurfaceMemberRegion(Guid.NewGuid(), "Region", S2, regionEdges, concrete)
            {
                Thickness = UnitsNet.Length.FromMeters(2 * thickness),
                Alignment = Member2DAlignment.Centre
            };
            addResult = model.CreateAdmObject(SMR);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            Subsoil subsoil = new Subsoil("Subsoil", UnitsNet.SpecificWeight.FromMeganewtonsPerCubicMeter(80.5), UnitsNet.SpecificWeight.FromMeganewtonsPerCubicMeter(35.5), UnitsNet.SpecificWeight.FromMeganewtonsPerCubicMeter(50), UnitsNet.ForcePerLength.FromMeganewtonsPerMeter(15.5), UnitsNet.ForcePerLength.FromMeganewtonsPerMeter(10.2));
            StructuralSurfaceConnection SS1 = new StructuralSurfaceConnection(Guid.NewGuid(), "SS1", S2, subsoil);
            addResult = model.CreateAdmObject(SS1);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            var edgecurvesS3 = new Curve<StructuralPointConnection>[4] {
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N3, N4 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N4, N8 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N8, N7 }),
                    new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N7, N3 })
                };
            StructuralSurfaceMember S3 = new StructuralSurfaceMember(Guid.NewGuid(), "S3", edgecurvesS3, concrete, UnitsNet.Length.FromMeters(thickness))
            {
                Type = new CSInfrastructure.FlexibleEnum<Member2DType>(Member2DType.Wall),
                Behaviour = Member2DBehaviour.Isotropic,
                Alignment = Member2DAlignment.Centre,
                Shape = Member2DShape.Flat
            };
            addResult = model.CreateAdmObject(S3);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            RelConnectsSurfaceEdge LH1 = new RelConnectsSurfaceEdge(Guid.NewGuid(), "LH1", S3, 0)
            {
                StartPointRelative = 0,
                EndPointRelative = 1,
                TranslationX = FixedTranslationLine,
                TranslationY = FixedTranslationLine,
                TranslationZ = FixedTranslationLine,
                RotationX = FreeRotationLine,
            };
            addResult = model.CreateAdmObject(LH1);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            RelConnectsSurfaceEdge LH2 = new RelConnectsSurfaceEdge(Guid.NewGuid(), "LH2", S3, 1)
            {
                StartPointRelative = 0,
                EndPointRelative = 1,
                TranslationX = FixedTranslationLine,
                TranslationY = FixedTranslationLine,
                TranslationZ = FixedTranslationLine,
                RotationX = FreeRotationLine,
            };
            addResult = model.CreateAdmObject(LH2);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            RelConnectsSurfaceEdge LH3 = new RelConnectsSurfaceEdge(Guid.NewGuid(), "LH3", S3, 2)
            {
                StartPointRelative = 0,
                EndPointRelative = 1,
                TranslationX = FixedTranslationLine,
                TranslationY = FixedTranslationLine,
                TranslationZ = FixedTranslationLine,
                RotationX = FreeRotationLine,
            };
            addResult = model.CreateAdmObject(LH3);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            RelConnectsSurfaceEdge LH4 = new RelConnectsSurfaceEdge(Guid.NewGuid(), "LH4", S3, 3)
            {
                StartPointRelative = 0,
                EndPointRelative = 1,
                TranslationX = FixedTranslationLine,
                TranslationY = FixedTranslationLine,
                TranslationZ = FixedTranslationLine,
                RotationX = FreeRotationLine,
            };
            addResult = model.CreateAdmObject(LH4);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }


            RelConnectsStructuralMember H1 = new RelConnectsStructuralMember(Guid.NewGuid(), "H1", B1)
            {
                Position = Position.End,
                TranslationX = FixedTranslation,
                TranslationY = FixedTranslation,
                TranslationZ = FixedTranslation,
                RotationX = FreeRotation,
            };
            addResult = model.CreateAdmObject(H1);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            RelConnectsStructuralMember H2 = new RelConnectsStructuralMember(Guid.NewGuid(), "H2", B2)
            {
                Position = Position.End,
                TranslationX = FixedTranslation,
                TranslationY = FixedTranslation,
                TranslationZ = FixedTranslation,
                RotationX = FreeRotation,
                RotationY = FreeRotation,
                RotationZ = FreeRotation
            };
            addResult = model.CreateAdmObject(H2);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            RelConnectsStructuralMember H3 = new RelConnectsStructuralMember(Guid.NewGuid(), "H3", B3)
            {
                Position = Position.End,
                TranslationX = FixedTranslation,
                TranslationY = FixedTranslation,
                TranslationZ = FixedTranslation,
                RotationX = FreeRotation,
                RotationY = FreeRotation,
                RotationZ = FreeRotation
            };
            addResult = model.CreateAdmObject(H3);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }
            RelConnectsStructuralMember H4 = new RelConnectsStructuralMember(Guid.NewGuid(), "H4", B4)
            {
                Position = Position.End,
                TranslationX = FixedTranslation,
                TranslationY = FixedTranslation,
                TranslationZ = FixedTranslation,
                RotationX = FreeRotation,
                RotationY = FreeRotation,
                RotationZ = FreeRotation
            };
            addResult = model.CreateAdmObject(H4);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            StructuralPointConnection N17 = new StructuralPointConnection(Guid.NewGuid(), "N17", UnitsNet.Length.FromMeters(-1 * b), UnitsNet.Length.FromMeters(0), UnitsNet.Length.FromMeters(0));
            addResult = model.CreateAdmObject(N17);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            var beamB9lines = new Curve<StructuralPointConnection>[1] { new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { N1, N17 }) };
            StructuralCurveMember B9 = new StructuralCurveMember(Guid.NewGuid(), "B9", beamB9lines, concreteRectangle)
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new CSInfrastructure.FlexibleEnum<Member1DType>(Member1DType.Beam),
                Layer = "Beams",
            };
            addResult = model.CreateAdmObject(B9);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            StructuralCurveConnection LSB = new StructuralCurveConnection(Guid.NewGuid(), "LSB", B9)
            {
                Origin = Origin.FromStart,
                CoordinateDefinition = CoordinateDefinition.Relative,
                StartPointRelative = 0.25,
                EndPointRelative = 0.75
            };
            addResult = model.CreateAdmObject(LSB);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            StructuralLoadGroup LG1 = new StructuralLoadGroup(Guid.NewGuid(), "LG1", LoadGroupType.Variable)
            {
                Load = new CSInfrastructure.FlexibleEnum<Load>(Load.Domestic)
            };
            addResult = model.CreateAdmObject(LG1);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            StructuralLoadCase LC1 = new StructuralLoadCase(LC1Id, "LC1", ActionType.Variable, LG1, LoadCaseType.Static)
            {
                Duration = Duration.Long,
                //Specification = Specification.Standard
            };
            addResult = model.CreateAdmObject(LC1);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            
            double lineloadValue = -5.0;


            StructuralCurveAction<CurveStructuralReferenceOnBeam> lineloadB1 = new StructuralCurveAction<CurveStructuralReferenceOnBeam>(Guid.NewGuid(), "lineLoadB1", CurveForceAction.OnBeam, UnitsNet.ForcePerLength.FromKilonewtonsPerMeter(lineloadValue), LC1, new CurveStructuralReferenceOnBeam(B1))
            {
                Direction = ActionDirection.Y,
                Distribution = CurveDistribution.Uniform
            };

            StructuralCurveAction<CurveStructuralReferenceOnBeam> lineloadB2 = new StructuralCurveAction<CurveStructuralReferenceOnBeam>(Guid.NewGuid(), "lineLoadB2", CurveForceAction.OnBeam, UnitsNet.ForcePerLength.FromKilonewtonsPerMeter(lineloadValue), LC1, new CurveStructuralReferenceOnBeam(B2))
            {
                Direction = ActionDirection.Y,
                Distribution = CurveDistribution.Uniform
            };
            StructuralCurveAction<CurveStructuralReferenceOnBeam> lineloadB3 = new StructuralCurveAction<CurveStructuralReferenceOnBeam>(Guid.NewGuid(), "lineLoadB3", CurveForceAction.OnBeam, UnitsNet.ForcePerLength.FromKilonewtonsPerMeter(lineloadValue), LC1, new CurveStructuralReferenceOnBeam(B3))
            {
                Direction = ActionDirection.Y,
                Distribution = CurveDistribution.Uniform
            };
            StructuralCurveAction<CurveStructuralReferenceOnBeam> lineloadB4 = new StructuralCurveAction<CurveStructuralReferenceOnBeam>(Guid.NewGuid(), "lineLoadB4", CurveForceAction.OnBeam, UnitsNet.ForcePerLength.FromKilonewtonsPerMeter(lineloadValue), LC1, new CurveStructuralReferenceOnBeam(B4))
            {
                Direction = ActionDirection.Y,
                Distribution = CurveDistribution.Uniform
            };

            addResult = model.CreateAdmObject(lineloadB1, lineloadB2, lineloadB3, lineloadB4);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            StructuralCurveAction<CurveStructuralReferenceOnEdge> edgeloadS1E1 = new StructuralCurveAction<CurveStructuralReferenceOnEdge>(Guid.NewGuid(), "edgeLoadS1E1", CurveForceAction.OnEdge, UnitsNet.ForcePerLength.FromKilonewtonsPerMeter(lineloadValue), LC1, new CurveStructuralReferenceOnEdge(S1, 0))
            {
                Direction = ActionDirection.Z
            };
            StructuralCurveAction<CurveStructuralReferenceOnEdge> edgeloadS1E2 = new StructuralCurveAction<CurveStructuralReferenceOnEdge>(Guid.NewGuid(), "edgeLoadS1E2", CurveForceAction.OnEdge, UnitsNet.ForcePerLength.FromKilonewtonsPerMeter(lineloadValue), LC1, new CurveStructuralReferenceOnEdge(S1, 1))
            {
                Direction = ActionDirection.Z
            };
            StructuralCurveAction<CurveStructuralReferenceOnEdge> edgeloadS1E3 = new StructuralCurveAction<CurveStructuralReferenceOnEdge>(Guid.NewGuid(), "edgeLoadS1E3", CurveForceAction.OnEdge, UnitsNet.ForcePerLength.FromKilonewtonsPerMeter(lineloadValue), LC1, new CurveStructuralReferenceOnEdge(S1, 2))
            {
                Direction = ActionDirection.Z
            };
            StructuralCurveAction<CurveStructuralReferenceOnEdge> edgeloadS1E4 = new StructuralCurveAction<CurveStructuralReferenceOnEdge>(Guid.NewGuid(), "edgeLoadS1E4", CurveForceAction.OnEdge, UnitsNet.ForcePerLength.FromKilonewtonsPerMeter(lineloadValue), LC1, new CurveStructuralReferenceOnEdge(S1, 3))
            {
                Direction = ActionDirection.Z
            };

            addResult = model.CreateAdmObject(edgeloadS1E1, edgeloadS1E2, edgeloadS1E3, edgeloadS1E4);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }


            StructuralLoadCase LC2 = new StructuralLoadCase(Guid.NewGuid(), "LC2", ActionType.Variable, LG1, LoadCaseType.Static)
            {
                Duration = Duration.Long,
                //Specification = Specification.Standard
            };
            addResult = model.CreateAdmObject(LC2);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }



           
            double surfaceloadValue = -7.0;


            StructuralSurfaceAction sls1 = new StructuralSurfaceAction(Guid.NewGuid(), "sls1", UnitsNet.Pressure.FromKilonewtonsPerSquareMeter(surfaceloadValue), S1, LC2)
            {
                Direction = ActionDirection.Z,
                Location = Location.Length
            };

            addResult = model.CreateAdmObject(sls1);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            StructuralPointAction<PointStructuralReferenceOnPoint> FP = new StructuralPointAction<PointStructuralReferenceOnPoint>(Guid.NewGuid(), "FP", UnitsNet.Force.FromKilonewtons(150), LC2, PointForceAction.InNode, new PointStructuralReferenceOnPoint(N13))
            {
                Direction = ActionDirection.Z
            };
            addResult = model.CreateAdmObject(FP);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }


            StructuralLoadCase LC3 = new StructuralLoadCase(Guid.NewGuid(), "LC3", ActionType.Variable, LG1, LoadCaseType.Static)
            {
                Duration = Duration.Long,
                //Specification = Specification.Standard
            };
            addResult = model.CreateAdmObject(LC3);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }


            var refB1 = new PointStructuralReferenceOnBeam(B1)
            {
                Origin = Origin.FromStart,
                CoordinateDefinition = CoordinateDefinition.Relative,
                RelativePositionX = 0.5,
                Repeat = 1
            };

            StructuralPointAction<PointStructuralReferenceOnBeam> FB = new StructuralPointAction<PointStructuralReferenceOnBeam>(Guid.NewGuid(), "FP", UnitsNet.Force.FromKilonewtons(150), LC2, PointForceAction.InNode, refB1)
            {
                Direction = ActionDirection.Z
            };
            addResult = model.CreateAdmObject(FB);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            //not yet published
            StructuralCurveMoment<CurveStructuralReferenceOnBeam> linemomentloadB4 = new StructuralCurveMoment<CurveStructuralReferenceOnBeam>(Guid.NewGuid(), "linemomentLoadB4", CurveForceAction.OnBeam, UnitsNet.TorquePerLength.FromKilonewtonMetersPerMeter(lineloadValue), LC1, new CurveStructuralReferenceOnBeam(B4))
            {
                Direction = MomentDirection.My,
                Distribution = CurveDistribution.Uniform
            };
            addResult = model.CreateAdmObject(linemomentloadB4);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

            var Combinations = new StructuralLoadCombinationData[3] { new StructuralLoadCombinationData(LC1, 1.0, 1.5), new StructuralLoadCombinationData(LC2, 1.0, 1.35), new StructuralLoadCombinationData(LC3, 1.0, 1.35) };
            StructuralLoadCombination C1 = new StructuralLoadCombination(C1Id, "C1", LoadCaseCombinationCategory.AccordingNationalStandard, Combinations)
            {
                NationalStandard = LoadCaseCombinationStandard.EnUlsSetC
            };
            addResult = model.CreateAdmObject(C1);
            if (addResult.PartialAddResult.Status != AdmChangeStatus.Ok) { throw HandleErrorResult(addResult); }

        }

        private static Exception HandleErrorResult(ResultOfPartialAddToAnalysisModel addResult)
        {
            switch (addResult.PartialAddResult.Status)
            {
                case AdmChangeStatus.InvalidInput:
                    throw new Exception(addResult.PartialAddResult.Warnings);
                case AdmChangeStatus.Error:
                    throw new Exception(addResult.PartialAddResult.Errors);
                case AdmChangeStatus.NotDone:
                    throw new Exception(addResult.PartialAddResult.Warnings);
            }
            if (addResult.PartialAddResult.Exception != null)
            {
                throw addResult.PartialAddResult.Exception;
            }
            throw new Exception("Unknown Error");
        }

        static void Main(string[] args)
        {
            KillSCIAEngineerOrphanRuns();
            SciaOpenApiAssemblyResolve();

            DeleteTemp();
            RunSCIAOpenAPI_simple();

            //ExcelTest();
        }

        private static void ExcelTest()
        {
                
        // info about Project 
        ProjectInformation projectInformation = new ProjectInformation(Guid.NewGuid(), "ProjectX")
            {
                BuildingType = "SimpleFrame",
                Location = "39XG+P7 Praha",
                LastUpdate = DateTime.Today,
                Status = "Draft",
                ProjectType = "New construction"
            };



            // info about Model ModelExchanger.AnalysisDataModel.ModelInformation
            ModelInformation modelInformation = new ModelInformation(Guid.NewGuid(), "ModelOne")
            {
                Discipline = "Static",
                Owner = "JB",
                LevelOfDetail = "200",
                LastUpdate = DateTime.Today,
                SourceApplication = "OpenAPI",
                RevisionNumber = "1",
                SourceCompany = "SCIA",
                SystemOfUnits = SystemOfUnits.Metric

            };
            var AnalysisObjects = new List<IAnalysisObject>();
            AnalysisObjects.Add(projectInformation);
            AnalysisObjects.Add(modelInformation);

            StructuralMaterial concrete = new StructuralMaterial(Guid.NewGuid(), "Concrete", MaterialType.Concrete, "C20/25");
            AnalysisObjects.Add(concrete);

            BootstrapperBase bootstrapperADM;
            bootstrapperADM = new BootstrapperBase();
            bootstrapperADM.Boostrapp<ModelExchangerExtensionIntegrationModule>();
            var ModelHolder = bootstrapperADM.Container.Resolve<IAnalysisModelHolder>();
            PartialAddResult actualResult = ModelHolder.AddToModel(AnalysisObjects);

            //sending Analytical model to SEn Client storage
            //ResultOfPartialAddToAnalysisModel addResult;
            //foreach (var analysisObject in ModelHolder.AnalysisModel)
            //{
            //    //addResult = model.CreateAdmObject(analysisObject);
            //}




            var Repository = bootstrapperADM.Container.Resolve<IProxyServiceExcel<IAnalysisModelRepository>>().GetServiceProxy();
            //Repository.GetById();

            //var Repository =  bootstrapperADM.Container.Resolve<IAnalysisModelInspector>();


            //BootstrapperBase bootstrapperExchanger;
            //bootstrapperExchanger = new BootstrapperBase();
            //bootstrapperExchanger.Boostrapp<ModelExchangerExtensionIntegrationModule>();
            ExchangeResult result = bootstrapperADM.Container.Resolve<ICoreToExcelFileService>().WriteExcel(ModelHolder.AnalysisModel, @"C:/TEMP/A.xls");

            ExchangeCoreResult readedExcelModel = bootstrapperADM.Container.Resolve<IExcelToCoreFileService>().ReadExcel(@"C:/TEMP/A.xls");
            //readedExcelModel.Model.IsModelValid

            result = bootstrapperADM.Container.Resolve<ICoreToJsonFileService>().WriteJson(ModelHolder.AnalysisModel, @"C:/TEMP/A.json");
            ExchangeCoreResult readedJsonModel = bootstrapperADM.Container.Resolve<IJsonToCoreFileService>().ReadJson(@"C:/TEMP/A.json");

        
        }
    }
}
