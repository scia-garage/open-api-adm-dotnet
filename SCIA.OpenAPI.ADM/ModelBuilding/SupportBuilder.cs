using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using ModelExchanger.AnalysisDataModel.Subtypes;
using System;
using System.Collections.Generic;
using UnitsNet;

namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Builds structural supports for the model
    /// </summary>
    public class SupportBuilder : IModelBuilder
    {
        private readonly AnalysisModel _model;
        private readonly IAnalysisModelService _modelService;
        private readonly List<StructuralPointSupport> _supports;

        public SupportBuilder(AnalysisModel model, IAnalysisModelService modelService)
        {
            _model = model;
            _modelService = modelService;
            _supports = new List<StructuralPointSupport>();
        }

        public SupportBuilder AddPointSupport(string name, string nodeName, SupportConstraints constraints)
        {
            // Find the node by name in the model
            var node = GeometryBuilder.FindByNameAndType(_model, nodeName, typeof(StructuralPointConnection)) as StructuralPointConnection
                ?? throw new ArgumentException($"Node '{nodeName}' not found in model for support '{name}'");

            var pointSupport = new StructuralPointSupport(
                Guid.NewGuid(),
                name,
                node)
            {
                RotationX = constraints.RotationX,
                RotationY = constraints.RotationY,
                TranslationX = constraints.TranslationX,
                TranslationY = constraints.TranslationY,
                TranslationZ = constraints.TranslationZ
            };

            _supports.Add(pointSupport);
            
            return this;
        }

        public SupportBuilder SetupDefaultSupports()
        {
            // Define standard constraint values
            var freeRotation = new Constraint<RotationalStiffness?>(
                ConstraintType.Free, 
                RotationalStiffness.FromKilonewtonMetersPerRadian(0));

            var fixedRotation = new Constraint<RotationalStiffness?>(
                ConstraintType.Rigid, 
                RotationalStiffness.FromKilonewtonMetersPerRadian(1e+10));

            var fixedTranslation = new Constraint<ForcePerLength?>(
                ConstraintType.Rigid, 
                ForcePerLength.FromKilonewtonsPerMeter(1e+10));

            var constraints = new SupportConstraints
            {
                RotationX = freeRotation,
                RotationY = fixedRotation,
                TranslationX = fixedTranslation,
                TranslationY = fixedTranslation,
                TranslationZ = fixedTranslation
            };

            // Add the four point supports from the original code
            AddPointSupport("SPS1", "N1", constraints);
            AddPointSupport("SPS2", "N2", constraints);
            AddPointSupport("SPS3", "N3", constraints);
            AddPointSupport("SPS4", "N4", constraints);

            return this;
        }

        public void Build()
        {
            var result = _modelService.AddItemsToModel(_model, _supports);

            foreach (var support in _supports)
            {
                if (!result.TryGetValue(support.Id, out bool created) || !created)
                {
                    throw new InvalidOperationException($"Error: Support '{support.Name}' (ID: {support.Id}) was not successfully created.");
                }
            }
            Console.WriteLine($"Supports created in ADM: {string.Join(", ", _supports.ConvertAll(s => s.Name))}");
        }
    }

    public class SupportConstraints
    {
        public Constraint<RotationalStiffness?> RotationX { get; set; }
        public Constraint<RotationalStiffness?> RotationY { get; set; }
        public Constraint<RotationalStiffness?> RotationZ { get; set; }
        public Constraint<ForcePerLength?> TranslationX { get; set; }
        public Constraint<ForcePerLength?> TranslationY { get; set; }
        public Constraint<ForcePerLength?> TranslationZ { get; set; }
    }
}
