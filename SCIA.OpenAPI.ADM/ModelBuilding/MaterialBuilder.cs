using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.Libraries;
using System;
using System.Collections.Generic;

namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Builds structural materials for the model
    /// </summary>
    public class MaterialBuilder : IModelBuilder
    {
        private readonly AnalysisModel _model;
        private readonly IAnalysisModelService _modelService;
        private readonly List<StructuralMaterial> _materials;

        public MaterialBuilder(AnalysisModel model, IAnalysisModelService modelService)
        {
            _model = model;
            _modelService = modelService;
            _materials = new List<StructuralMaterial>();
        }

        public MaterialBuilder SetupDefaultMaterials()
        {
            // Add default concrete material
            _materials.Add(new StructuralMaterial(
                Guid.NewGuid(),
                "Concrete",
                MaterialType.Concrete,
                "C20/25"
            ));

            // Add default steel material
            _materials.Add(new StructuralMaterial(
                Guid.NewGuid(),
                "Steel",
                MaterialType.Steel,
                "S 235"
            ));
            return this;
        }

        public MaterialBuilder AddMaterial(string name, MaterialType type, string quality)
        {
            _materials.Add(new StructuralMaterial(
                Guid.NewGuid(),
                name,
                type,
                quality
            ));
            return this;
        }

        public void Build()
        {
            var result = _modelService.AddItemsToModel(_model, _materials);

            foreach (var material in _materials)
            {
                if (!result.TryGetValue(material.Id, out bool created) || !created)
                {
                    throw new InvalidOperationException($"Error: Material '{material.Name}' (ID: {material.Id}) was not successfully created.");
                }
            }
            Console.WriteLine($"Materials created in ADM: {string.Join(", ", _materials.ConvertAll(m => m.Name))}");
        }
        public static StructuralMaterial FindByName(AnalysisModel model, string name)
        {
            foreach (var item in model)
            {
                if (item is StructuralMaterial material && material.Name == name)
                {
                    return material;
                }
            }
            throw new ArgumentException($"Material '{name}' not found in model");
        }
    }

}
