using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Integration.Bootstrapper;
using SciaTools.Kernel.ModelExchangerExtension.Models.Exchange;

using System;
using SCIA.OpenAPI;
using SciaTools.AdmToAdm.AdmSignalR.Models.ModelModification;

namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Orchestrates the model building process using the builder pattern
    /// </summary>
    public class ModelDirector
    {
        private readonly Structure _model;
        public ModelDirector(Structure model)
        {
            _model = model;
        }

        /// <summary>
        /// Creates a complete structural model using the ADM
        /// </summary>
        public void BuildCompleteModel()
        {
            AnalysisModel admModel = new AnalysisModel();
            AnalysisDataModelBootstrapper admBootstrapper = new AnalysisDataModelBootstrapper();

            using (var scope = admBootstrapper.CreateThreadedScope())
            {
                IAnalysisModelService modelService = scope.GetService<IAnalysisModelService>();
                IAnalysisModelQuery queryService = scope.GetService<IAnalysisModelQuery>();

                // Step 1: Model Information
                var modelInfoBuilder = new ModelInformationBuilder(admModel, modelService, "ExampleModel");
                modelInfoBuilder.Build();

                // Step 2: Materials
                var materialBuilder = new MaterialBuilder(admModel, modelService)
                    .SetupDefaultMaterials();
                materialBuilder.Build();

                // Step 3: Cross-sections
                var crossSectionBuilder = new CrossSectionBuilder(admModel, modelService)
                    .SetupDefaultSections();
                crossSectionBuilder.Build();

                // Step 4: Geometry
                var geometryBuilder = new GeometryBuilder(admModel, modelService)
                    .SetupDefaultGeometry();
                geometryBuilder.Build();

                // Step 5: Supports
                var supportBuilder = new SupportBuilder(admModel, modelService)
                    .SetupDefaultSupports();
                supportBuilder.Build();

                // Step 6: Loads (simplified for now)
                // TODO: Create LoadBuilder

                Console.WriteLine("Model building process completed successfully!");
            }
            admModel.EnforceModelValidity();

            // Add all ADM objects to the Structure
            foreach (IAnalysisObject admObject in admModel)
            {
                ResultOfPartialAddToAnalysisModel result = _model.CreateAdmObject(admObject);
                if (result.PartialAddResult.Status != AdmChangeStatus.Ok)
                {
                    Console.WriteLine($"Error adding object {admObject.Name} to model: {result.PartialAddResult.Errors}");
                }
            }
        }
    }
}
