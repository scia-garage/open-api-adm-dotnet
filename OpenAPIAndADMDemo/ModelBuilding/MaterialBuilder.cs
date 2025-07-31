using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.Libraries;
using System;
using System.Collections.Generic;
using ModelExchanger.AnalysisDataModel;
using UnitsNet;

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

        /// <summary>
        /// Adds a new material to the model
        /// </summary>
        /// <param name="name">Name of the material</param>
        /// <param name="type">Type of the material</param>
        /// <param name="quality">Quality of the material</param>
        /// <param name="EModulus">Young's modulus in GPa</param>
        /// <param name="GModulus">Shear modulus in GPa</param>
        /// <param name="poissonRatio">Poisson's ratio</param>
        /// <param name="density">Density of the material in kg/m³</param>
        /// <returns>Instance of MaterialBuilder for method chaining</returns>
        public MaterialBuilder AddMaterial(string name, MaterialType type, string quality, double EModulus, double GModulus, double poissonRatio, double density)
        {
            var newMaterial = new StructuralMaterial(
                Guid.NewGuid(),
                name,
                type,
                quality)
            {
                EModulus = new Pressure(EModulus, UnitsNet.Units.PressureUnit.Gigapascal),
                GModulus = new Pressure(GModulus, UnitsNet.Units.PressureUnit.Gigapascal),
                PoissonCoefficient = poissonRatio,
                UnitMass = new Density(density, UnitsNet.Units.DensityUnit.KilogramPerCubicMeter)
            };
            _materials.Add(newMaterial);
            return this;
        }

        public void Build()
        {
            var result = _modelService.AddItemsToModel(_model, _materials);

            foreach (StructuralMaterial material in _materials)
            {
                if (!result.TryGetValue(material.Id, out bool created) || !created)
                {
                    throw new InvalidOperationException($"Error: Material '{material.Name}' (ID: {material.Id}) was not successfully created.");
                }
            }
            _model.EnforceModelValidity();

            Console.WriteLine($"Materials created in ADM: {string.Join(", ", _materials.ConvertAll(m => m.Name))}");
        }
        public static StructuralMaterial FindByName(AnalysisModel model, string name)
        {
            foreach (IAnalysisObject item in model)
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
