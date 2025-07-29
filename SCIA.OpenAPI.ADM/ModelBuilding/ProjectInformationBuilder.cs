using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Models;
using System;

namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Builds project and model information
    /// </summary>
    public class ModelInformationBuilder : IModelBuilder
    {
        private readonly AnalysisModel _model;
        private readonly IAnalysisModelService _modelService;
        private readonly string _modelName;
        private readonly string _owner;
        private readonly string _sourceCompany;
        private readonly NationalCode _nationalCode;

        public ModelInformationBuilder(
            AnalysisModel model,
            IAnalysisModelService modelService,
            string modelName,
            string owner = "ExampleOwner",
            string sourceCompany = "ExampleCompany",
            NationalCode nationalCode = NationalCode.EC_Standard_EN
            )
        {
            _model = model;
            _modelService = modelService;
            _modelName = modelName;
            _owner = owner;
            _sourceCompany = sourceCompany;
            _nationalCode = nationalCode;
        }

        public void Build()
        {
            var modelInformation = new ModelInformation(
                Guid.NewGuid(),
                _modelName)
            {
                NationalCode = _nationalCode,
                SourceApplication = "OpenAPIAndADMDemo",
                SourceCompany = _sourceCompany,
                Owner = _owner,
                LastUpdate = DateTime.Now
            };
            _modelService.AddItemToModel(_model, modelInformation);
        }
    }
}
