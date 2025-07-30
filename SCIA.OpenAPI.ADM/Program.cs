using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Libraries;
using ModelExchanger.AnalysisDataModel.Loads;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.Subtypes;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using ModelExchanger.AnalysisDataModel.StructuralReferences.Curves;
using ModelExchanger.AnalysisDataModel.StructuralReferences.Points;
using SciaTools.Kernel.ModelExchangerExtension.Models.Exchange;
using System;
using SCIA.OpenAPI.Results;
using Results64Enums;
using System.IO;
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
using OpenAPIAndADMDemo.Infrastructure;
using OpenAPIAndADMDemo.Configuration;
using OpenAPIAndADMDemo.ModelBuilding;

namespace OpenAPIAndADMDemo
{
    class Program
    {
        // Infrastructure components
        private static SciaEnvironmentManager _environmentManager;
        private static SciaProcessManager _processManager;
        private static SciaAssemblyResolver _assemblyResolver;

        /// <summary>
        /// Initializes the infrastructure components
        /// </summary>
        private static void InitializeInfrastructure()
        {
            _environmentManager = new SciaEnvironmentManager(ModelConstants.SciaVersion);
            _processManager = new SciaProcessManager();
            _assemblyResolver = new SciaAssemblyResolver(_environmentManager.SciaEngineerFullPath);
            _assemblyResolver.Initialize();
        }

        /// <summary>
        /// Cleans up infrastructure components
        /// </summary>
        private static void CleanupInfrastructure()
        {
            _assemblyResolver?.Cleanup();
        }

        static void RunSCIAOpenAPI_simple()
        {
            using (SCIA.OpenAPI.Environment env = new SCIA.OpenAPI.Environment(_environmentManager.SciaEngineerFullPath, _environmentManager.AppLogPath, ModelConstants.ApplicationVersion))
            {
                // Run SCIA Engineer application
                if (!env.RunSCIAEngineer(SCIA.OpenAPI.Environment.GuiMode.ShowWindowShow))
                {
                    Console.WriteLine("Failed to start SCIA Engineer");
                    return;
                }

                using (var projectManager = new ProjectManager(env))
                {
                    // Initialize the project (create template, open project)
                    if (!projectManager.InitializeEmptyProjectTemplate())
                    {
                        Console.WriteLine("Failed to initialize project");
                        return;
                    }

                    var modelDirector = new ModelDirector(projectManager.Project.Model);
                    modelDirector.BuildCompleteModel();

                    projectManager.Project.Model.RefreshModel_ToSCIAEngineer();

                    // Run calculation
                    Console.WriteLine($"Calculation started...");
                    projectManager.Project.RunCalculation();
                    Console.WriteLine($"Calculation completed.");

                    Console.WriteLine($"Press any key to close SEN.");
                    Console.ReadKey();

                    // //Initialize Results API
                    // ResultsAPI rapi = proj.Model.InitializeResultsAPI();
                    // if (rapi != null)
                    // {
                    //     //Create container for 1D results
                    //     Result IntFor1Db1 = new Result();
                    //     //Results key for internal forces on beam 1
                    //     ResultKey keyIntFor1Db1 = new ResultKey
                    //     {
                    //         CaseType = eDsElementType.eDsElementType_LoadCase,
                    //         CaseId = ModelConstants.LC1Id,
                    //         EntityType = eDsElementType.eDsElementType_Beam,
                    //         EntityName = ModelConstants.B1Name,
                    //         Dimension = eDimension.eDim_1D,
                    //         ResultType = eResultType.eFemBeamInnerForces,
                    //         CoordSystem = eCoordSystem.eCoordSys_Local
                    //     };
                    //     //Load 1D results based on results key
                    //     IntFor1Db1 = rapi.LoadResult(keyIntFor1Db1);
                    //     if (IntFor1Db1 != null)
                    //     {
                    //         Console.WriteLine(IntFor1Db1.GetTextOutput());
                    //         var N = IntFor1Db1.GetMagnitudeName(0);
                    //         var Nvalue = IntFor1Db1.GetValue(0, 0);
                    //         Console.WriteLine(N);
                    //         Console.WriteLine(Nvalue);
                    //     }
                    //     //combination
                    //     //Create container for 1D results
                    //     Result IntFor1Db1Combi = new Result();
                    //     //Results key for internal forces on beam 1
                    //     ResultKey keyIntFor1Db1Combi = new ResultKey
                    //     {
                    //         EntityType = eDsElementType.eDsElementType_Beam,
                    //         EntityName = ModelConstants.B1Name,
                    //         CaseType = eDsElementType.eDsElementType_Combination,
                    //         CaseId = ModelConstants.C1Id,
                    //         Dimension = eDimension.eDim_1D,
                    //         ResultType = eResultType.eFemBeamInnerForces,
                    //         CoordSystem = eCoordSystem.eCoordSys_Local
                    //     };
                    //     // Load 1D results based on results key
                    //     IntFor1Db1Combi = rapi.LoadResult(keyIntFor1Db1Combi);
                    //     if (IntFor1Db1Combi != null)
                    //     {
                    //         Console.WriteLine(IntFor1Db1Combi.GetTextOutput());
                    //     }
                    //     //Results key for reaction on node 1
                    //     ResultKey keyReactionsSu1 = new ResultKey
                    //     {
                    //         CaseType = eDsElementType.eDsElementType_LoadCase,
                    //         CaseId = ModelConstants.LC1Id,
                    //         EntityType = eDsElementType.eDsElementType_Node,
                    //         EntityName = ModelConstants.N1Name,
                    //         Dimension = eDimension.eDim_reactionsPoint,
                    //         ResultType = eResultType.eReactionsNodes,
                    //         CoordSystem = eCoordSystem.eCoordSys_Global
                    //     };
                    //     Result reactionsSu1 = new Result();
                    //     reactionsSu1 = rapi.LoadResult(keyReactionsSu1);
                    //     if (reactionsSu1 != null)
                    //     {
                    //         Console.WriteLine(reactionsSu1.GetTextOutput());
                    //     }

                    //     Result Def2Ds1 = new Result();
                    //     // Results key for internal forces on slab
                    //     ResultKey keyDef2Ds1 = new ResultKey
                    //     {
                    //         CaseType = eDsElementType.eDsElementType_LoadCase,
                    //         CaseId = ModelConstants.LC1Id,
                    //         EntityType = eDsElementType.eDsElementType_Slab,
                    //         EntityName = ModelConstants.S1Name,
                    //         Dimension = eDimension.eDim_2D,
                    //         ResultType = eResultType.eFemDeformations,
                    //         CoordSystem = eCoordSystem.eCoordSys_Local
                    //     };

                    //     Def2Ds1 = rapi.LoadResult(keyDef2Ds1);
                    //     if (Def2Ds1 != null)
                    //     {
                    //         Console.WriteLine(Def2Ds1.GetTextOutput());

                    //         double maxvalue = 0;
                    //         double pivot;
                    //         for (int i = 0; i < Def2Ds1.GetMeshElementCount(); i++)
                    //         {
                    //             pivot = Def2Ds1.GetValue(2, i);
                    //             if (System.Math.Abs(pivot) > System.Math.Abs(maxvalue))
                    //             {
                    //                 maxvalue = pivot;

                    //             }
                    //         }
                    //         Console.WriteLine("Maximum deformation on slab:");
                    //         Console.WriteLine(maxvalue);
                    //     }

                    // }
                    
                    // Project cleanup is handled automatically by ProjectManager.Dispose()
                }
            }
        }
        // static private object SciaOpenApiWorker(SCIA.OpenAPI.Environment env)
        // {
        //     //Run SCIA Engineer application
        //     bool openedSE = env.RunSCIAEngineer(SCIA.OpenAPI.Environment.GuiMode.ShowWindowShow);
        //     if (!openedSE)
        //     {
        //         throw new InvalidOperationException($"SCIA Engineer not started");
        //     }
        //     Console.WriteLine($"SEn opened");
        //     SciaFileGetter fileGetter = new SciaFileGetter();
        //     var EsaFile = fileGetter.PrepareBasicEmptyFile(@"C:/TEMP/");//path where the template file "template.esa" is created
        //     if (!File.Exists(EsaFile))
        //     {
        //         throw new InvalidOperationException($"File from manifest resource is not created ! Temp: {env.AppTempPath}");
        //     }

        //     SCIA.OpenAPI.EsaProject proj = env.OpenProject(EsaFile);
        //     //SCIA.OpenAPI.EsaProject proj = env.OpenProject(SciaEngineerProjecTemplate);
        //     if (proj == null)
        //     {
        //         throw new InvalidOperationException($"File from manifest resource is not opened ! Temp: {env.AppTempPath}");
        //     }
        //     Console.WriteLine($"Proj opened");


        //     // Test the new ModelBuilder framework
        //     CreateModelWithBuilders(proj.Model);
            
        //     // Original model creation (commented out for testing)
        //     // CreateModel(proj.Model);
 
        //     proj.Model.RefreshModel_ToSCIAEngineer();

        //     Console.WriteLine($"My model sent to SEn");


        //     // Run calculation
        //     proj.RunCalculation();
        //     Console.WriteLine($"My model calculate");

        //     //storage for results
        //     OpenApiE2EResults storage = new OpenApiE2EResults();

        //     //Initialize Results API
        //     using (ResultsAPI resultsApi = proj.Model.InitializeResultsAPI())
        //     {
        //         if (resultsApi == null)
        //         {
        //             return storage;
        //         }
        //         {
        //             OpenApiE2EResult beamB1InnerForLc = new OpenApiE2EResult("beamB1InnerForcesLC1")
        //             {
        //                 ResultKey = new ResultKey
        //                 {
        //                     EntityType = eDsElementType.eDsElementType_Beam,
        //                     EntityName = ModelConstants.B1Name,
        //                     CaseType = eDsElementType.eDsElementType_LoadCase,
        //                     CaseId = ModelConstants.LC1Id,
        //                     Dimension = eDimension.eDim_1D,
        //                     ResultType = eResultType.eFemBeamInnerForces,
        //                     CoordSystem = eCoordSystem.eCoordSys_Local
        //                 }
        //             };
        //             beamB1InnerForLc.Result = resultsApi.LoadResult(beamB1InnerForLc.ResultKey);
        //             storage.SetResult(beamB1InnerForLc);
        //         }
        //         {
        //             OpenApiE2EResult beamB1IDeformationLc = new OpenApiE2EResult("beamB1DeformationsLC1")
        //             {
        //                 ResultKey = new ResultKey
        //                 {
        //                     EntityType = eDsElementType.eDsElementType_Beam,
        //                     EntityName = ModelConstants.B1Name,
        //                     CaseType = eDsElementType.eDsElementType_LoadCase,
        //                     CaseId = ModelConstants.LC1Id,
        //                     Dimension = eDimension.eDim_1D,
        //                     ResultType = eResultType.eFemBeamDeformation,
        //                     CoordSystem = eCoordSystem.eCoordSys_Local
        //                 }
        //             };
        //             beamB1IDeformationLc.Result = resultsApi.LoadResult(beamB1IDeformationLc.ResultKey);
        //             storage.SetResult(beamB1IDeformationLc);
        //         }
        //         {
        //             OpenApiE2EResult beamB1RelIDeformationLc = new OpenApiE2EResult("beamB1RelativeDeformationsLC1")
        //             {
        //                 ResultKey = new ResultKey
        //                 {
        //                     EntityType = eDsElementType.eDsElementType_Beam,
        //                     EntityName = ModelConstants.B1Name,
        //                     CaseType = eDsElementType.eDsElementType_LoadCase,
        //                     CaseId = ModelConstants.LC1Id,
        //                     Dimension = eDimension.eDim_1D,
        //                     ResultType = eResultType.eFemBeamRelativeDeformation,
        //                     CoordSystem = eCoordSystem.eCoordSys_Local
        //                 }
        //             };
        //             beamB1RelIDeformationLc.Result = resultsApi.LoadResult(beamB1RelIDeformationLc.ResultKey);
        //             storage.SetResult(beamB1RelIDeformationLc);
        //         }
        //         //{
        //         //    OpenApiE2EResult beamInnerForcesCombi = new OpenApiE2EResult("beamInnerForcesCombi")
        //         //    {
        //         //        ResultKey = new ResultKey
        //         //        {
        //         //            EntityType = eDsElementType.eDsElementType_Beam,
        //         //            EntityName = ModelConstants.B1Name,
        //         //            CaseType = eDsElementType.eDsElementType_Combination,
        //         //            CaseId = ModelConstants.C1Id,
        //         //            Dimension = eDimension.eDim_1D,
        //         //            ResultType = eResultType.eFemBeamInnerForces,
        //         //            CoordSystem = eCoordSystem.eCoordSys_Local
        //         //        }
        //         //    };
        //         //    beamInnerForcesCombi.Result = resultsApi.LoadResult(beamInnerForcesCombi.ResultKey);
        //         //    storage.SetResult(beamInnerForcesCombi);
        //         //}


        //         {
        //             OpenApiE2EResult slabInnerForces = new OpenApiE2EResult("slabInnerForces")
        //             {
        //                 ResultKey = new ResultKey
        //                 {
        //                     EntityType = eDsElementType.eDsElementType_Slab,
        //                     EntityName = ModelConstants.S1Name,
        //                     CaseType = eDsElementType.eDsElementType_LoadCase,
        //                     CaseId = ModelConstants.LC1Id,
        //                     Dimension = eDimension.eDim_2D,
        //                     ResultType = eResultType.eFemInnerForces,
        //                     CoordSystem = eCoordSystem.eCoordSys_Local
        //                 }
        //             };
        //             slabInnerForces.Result = resultsApi.LoadResult(slabInnerForces.ResultKey);
        //             storage.SetResult(slabInnerForces);
        //         }
        //         {
        //             OpenApiE2EResult slabDeformations = new OpenApiE2EResult("slabDeformations")
        //             {
        //                 ResultKey = new ResultKey
        //                 {
        //                     EntityType = eDsElementType.eDsElementType_Slab,
        //                     EntityName = ModelConstants.S1Name,
        //                     CaseType = eDsElementType.eDsElementType_LoadCase,
        //                     CaseId = ModelConstants.LC1Id,
        //                     Dimension = eDimension.eDim_2D,
        //                     ResultType = eResultType.eFemDeformations,
        //                     CoordSystem = eCoordSystem.eCoordSys_Local
        //                 }
        //             };
        //             slabDeformations.Result = resultsApi.LoadResult(slabDeformations.ResultKey);
        //             storage.SetResult(slabDeformations);
        //         }
        //         {
        //             OpenApiE2EResult slabStresses = new OpenApiE2EResult("slabStresses")
        //             {
        //                 ResultKey = new ResultKey
        //                 {
        //                     EntityType = eDsElementType.eDsElementType_Slab,
        //                     EntityName = ModelConstants.S1Name,
        //                     CaseType = eDsElementType.eDsElementType_LoadCase,
        //                     CaseId = ModelConstants.LC1Id,
        //                     Dimension = eDimension.eDim_2D,
        //                     ResultType = eResultType.eFemStress,
        //                     CoordSystem = eCoordSystem.eCoordSys_Local
        //                 }
        //             };
        //             slabStresses.Result = resultsApi.LoadResult(slabStresses.ResultKey);
        //             storage.SetResult(slabStresses);
        //         }
        //         {
        //             OpenApiE2EResult slabStrains = new OpenApiE2EResult("slabStrains")
        //             {
        //                 ResultKey = new ResultKey
        //                 {
        //                     EntityType = eDsElementType.eDsElementType_Slab,
        //                     EntityName = ModelConstants.S1Name,
        //                     CaseType = eDsElementType.eDsElementType_LoadCase,
        //                     CaseId = ModelConstants.LC1Id,
        //                     Dimension = eDimension.eDim_2D,
        //                     ResultType = eResultType.eFemStrains,
        //                     CoordSystem = eCoordSystem.eCoordSys_Local
        //                 }
        //             };
        //             slabStrains.Result = resultsApi.LoadResult(slabStrains.ResultKey);
        //             storage.SetResult(slabStrains);
        //         }
        //         {
        //             OpenApiE2EResult slabInnerForcesExtended = new OpenApiE2EResult("slabInnerForcesExtended")
        //             {
        //                 ResultKey = new ResultKey
        //                 {
        //                     EntityType = eDsElementType.eDsElementType_Slab,
        //                     EntityName = ModelConstants.S1Name,
        //                     CaseType = eDsElementType.eDsElementType_LoadCase,
        //                     CaseId = ModelConstants.LC1Id,
        //                     Dimension = eDimension.eDim_2D,
        //                     ResultType = eResultType.eFemInnerForces_Extended,
        //                     CoordSystem = eCoordSystem.eCoordSys_Local
        //                 }
        //             };
        //             slabInnerForcesExtended.Result = resultsApi.LoadResult(slabInnerForcesExtended.ResultKey);
        //             storage.SetResult(slabInnerForcesExtended);
        //         }

        //         //{
        //         //    OpenApiE2EResult reactions = new OpenApiE2EResult("ReactionsN1")
        //         //    {
        //         //        ResultKey = new ResultKey
        //         //        {
        //         //            CaseType = eDsElementType.eDsElementType_LoadCase,
        //         //            CaseId = ModelConstants.LC1Id,
        //         //            EntityType = eDsElementType.eDsElementType_Node,
        //         //            EntityName = "n1",
        //         //            Dimension = eDimension.eDim_reactionsPoint,
        //         //            ResultType = eResultType.eReactionsNodes,
        //         //            CoordSystem = eCoordSystem.eCoordSys_Global
        //         //        }
        //         //    };
        //         //    reactions.Result = resultsApi.LoadResult(reactions.ResultKey);
        //         //    storage.SetResult(reactions);
        //         //}
        //         //{
        //         //    OpenApiE2EResult reactions = new OpenApiE2EResult("ReactionsSu1")
        //         //    {
        //         //        ResultKey = new ResultKey
        //         //        {
        //         //            CaseType = eDsElementType.eDsElementType_LoadCase,
        //         //            CaseId = ModelConstants.LC1Id,
        //         //            EntityType = eDsElementType.eDsElementType_PointSupportPoint,
        //         //            EntityName = "Su1",
        //         //            Dimension = eDimension.eDim_reactionsPoint,
        //         //            ResultType = eResultType.eResultTypeReactionsSupport0D,
        //         //            CoordSystem = eCoordSystem.eCoordSys_Global,

        //         //        }
        //         //    };
        //         //    reactions.Result = resultsApi.LoadResult(reactions.ResultKey);
        //         //    storage.SetResult(reactions);
        //         //}
        //         //{
        //         //    OpenApiE2EResult ReactionslinSupBeam = new OpenApiE2EResult("ReactionslinSupBeam")
        //         //    {
        //         //        ResultKey = new ResultKey
        //         //        {
        //         //            EntityType = eDsElementType.eDsElementType_LineSupportLine,
        //         //            EntityName = "linSupBeam",
        //         //            Dimension = eDimension.eDim_reactionsLine,
        //         //            CoordSystem = eCoordSystem.eCoordSys_Global,
        //         //            CaseType = eDsElementType.eDsElementType_LoadCase,
        //         //            CaseId = ModelConstants.LC1Id,
        //         //            ResultType = eResultType.eResultTypeReactionsSupport1D,

        //         //        }
        //         //    };
        //         //    ReactionslinSupBeam.Result = resultsApi.LoadResult(ReactionslinSupBeam.ResultKey);
        //         //    storage.SetResult(ReactionslinSupBeam);
        //         //}             

        //         return storage;
        //     }
        // }

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("OpenAPIAndADMDemo Application");
                Console.WriteLine("==============================");
                Console.WriteLine();
                
                // Initialize infrastructure
                InitializeInfrastructure();
                
                // Clean up any orphan processes
                _processManager.KillOrphanRuns();
                
                // Clean up temp directory
                _environmentManager.DeleteTemp();
                                
                // Run the main example
                RunSCIAOpenAPI_simple();

                //ExcelTest();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Clean up infrastructure
                CleanupInfrastructure();
                
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
