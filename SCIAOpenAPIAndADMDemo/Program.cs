using System;
using Results64Enums;

using OpenAPIAndADMDemo.Infrastructure;
using OpenAPIAndADMDemo.Configuration;
using OpenAPIAndADMDemo.ModelBuilding;
using OpenAPIAndADMDemo.Results;

namespace OpenAPIAndADMDemo
{
    class Program
    {
        // Infrastructure components
        private static SciaEnvironmentManager _environmentManager;
        private static SciaAssemblyResolver _assemblyResolver;

        /// <summary>
        /// Initializes the infrastructure components
        /// </summary>
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
                SciaProcessManager.KillOrphanRuns();

                // Clean up temp directory
                _environmentManager.DeleteTemp();

                // Run the example
                Run();
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
        private static void InitializeInfrastructure()
        {
            _environmentManager = new SciaEnvironmentManager(ModelConstants.SciaVersion);
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

        static void Run()
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
                    // Initialize the project (create empty project template and open it)
                    if (!projectManager.InitializeEmptyProjectTemplate())
                    {
                        Console.WriteLine("Failed to initialize project");
                        return;
                    }

                    // Create the model with ADM
                    var modelDirector = new ModelDirector(projectManager.Project.Model);
                    modelDirector.BuildCompleteModel();

                    // Send the model to SCIA Engineer
                    projectManager.Project.Model.RefreshModel_ToSCIAEngineer();

                    Console.WriteLine($"Model sent to SCIA Engineer");
                    Console.WriteLine($"Press any key to run the calculation.");
                    Console.ReadKey();

                    // Run calculation
                    Console.WriteLine($"Calculation started...");
                    projectManager.Project.RunCalculation();

                    // Read the results
                    using (var resultsManager = new ResultsManager(projectManager.Project.Model))
                    {
                        resultsManager.ReadMemberInternalForces("Column C1 : Inner Forces : Load case LC1", "LC1", "C1");
                        resultsManager.ReadMemberInternalForces("Column C1 : InnerForces : Load combination LComb1", "LComb1", "C1", caseType: eDsElementType.eDsElementType_Combination);
                        resultsManager.ReadMemberDeformations("Beam B3 : Deformations : Load case LC1", "LC1", "B3");
                        resultsManager.ReadMemberDeformations("Beam B3 : Relative deformations : Load case LC1", "LC1", "B3", relative: true);
                        resultsManager.ReadPointSupportReactions("Point Support PS1 : Reactions : Load case LC1", "LC1", "PS1");
                        resultsManager.ReadSurfaceInternalForces("Slab S1 : Inner Forces : Load case LC2", "LC2", "S1");
                        resultsManager.ReadSurfaceStresses("Slab S1 : Stresses : Load case LC2", "LC2", "S1");
                        resultsManager.ReadSurfaceStrains("Slab S1 : Strains : Load case LC2", "LC2", "S1");
                        resultsManager.ReadSurfaceDeformations("Slab S1 : Deformations : Load case LC2", "LC2", "S1");
                        resultsManager.ReadSurfaceContactStresses("Slab S2 : Contact Stresses : Load case LC2", "LC2", "S2");
                        resultsManager.PrintAllResults();
                    }

                    Console.WriteLine($"Press any key to close SEN.");
                    Console.ReadKey();
                }
            }
        }
    }
}
