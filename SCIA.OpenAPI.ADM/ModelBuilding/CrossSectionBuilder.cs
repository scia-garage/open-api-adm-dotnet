using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.Libraries;
using System;
using System.Collections.Generic;
using UnitsNet;
using ModelExchanger.AnalysisDataModel;

namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Builds structural cross-sections for the model
    /// </summary>
    public class CrossSectionBuilder : IModelBuilder
    {
        private readonly AnalysisModel _model;
        private readonly IAnalysisModelService _modelService;
        private readonly List<StructuralCrossSection> _crossSections;

        public CrossSectionBuilder(AnalysisModel model, IAnalysisModelService modelService)
        {
            _model = model;
            _modelService = modelService;
            _crossSections = new List<StructuralCrossSection>();
        }

        public CrossSectionBuilder AddManufacturedSection(string name, string materialName, string profileName, FormCode formCode, DescriptionId descriptionId)
        {
            var material = MaterialBuilder.FindByName(_model, materialName)
                ?? throw new ArgumentException($"Material '{materialName}' not found in model for cross-section '{name}'");

            _crossSections.Add(new StructuralManufacturedCrossSection(
                Guid.NewGuid(),
                name,
                material,
                profileName,
                formCode,
                descriptionId
            ));
            return this;
        }

        public CrossSectionBuilder AddParametricSection(string name, string materialName, ProfileLibraryId profileLibrary, Length[] dimensions)
        {
            var material = MaterialBuilder.FindByName(_model, materialName)
                ?? throw new ArgumentException($"Material '{materialName}' not found in model for cross-section '{name}'");
            _crossSections.Add(new StructuralParametricCrossSection(
                Guid.NewGuid(),
                name,
                material,
                profileLibrary,
                dimensions
            ));
            return this;
        }

        public CrossSectionBuilder SetupDefaultSections()
        {
            // Steel profile
            AddManufacturedSection(
                "HEA260", 
                "Steel", 
                "HEA260", 
                FormCode.ISection, 
                DescriptionId.EuropeanIBeam);

            // Concrete rectangle
            AddParametricSection(
                "Concrete600x300",
                "Concrete",
                ProfileLibraryId.Rectangle,
                new Length[] { 
                    Length.FromMillimeters(600), // height
                    Length.FromMillimeters(300)  // width
                });

            return this;
        }

        public void Build()
        {
            var result = _modelService.AddItemsToModel(_model, _crossSections);

            foreach (StructuralCrossSection section in _crossSections)
            {
                if (!result.TryGetValue(section.Id, out bool created) || !created)
                {
                    throw new InvalidOperationException($"Error: Cross-section '{section.Name}' (ID: {section.Id}) was not successfully created.");
                }
            }

            _model.EnforceModelValidity();

            Console.WriteLine($"Cross-sections created in ADM: {string.Join(", ", _crossSections.ConvertAll(cs => cs.Name))}");
        }

        public static StructuralCrossSection FindByName(AnalysisModel model, string sectionName)
        {
            foreach (IAnalysisObject item in model)
            {
                if (item is StructuralCrossSection section && section.Name == sectionName)
                {
                    return section;
                }
            }
            return null;
        }
    }
}
