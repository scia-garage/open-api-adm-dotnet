using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using ModelExchanger.AnalysisDataModel.Subtypes;
using ModelExchanger.AnalysisDataModel;
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
        private readonly List<StructuralPointSupport> _pointSupports;
        private readonly List<StructuralSurfaceConnection> _surfaceSupports;

        public SupportBuilder(AnalysisModel model, IAnalysisModelService modelService)
        {
            _model = model;
            _modelService = modelService;
            _pointSupports = new List<StructuralPointSupport>();
            _surfaceSupports = new List<StructuralSurfaceConnection>();
        }

        public SupportBuilder AddPointSupport(string name, string nodeName, PointConstraints constraints)
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
                RotationZ = constraints.RotationZ,
                TranslationX = constraints.TranslationX,
                TranslationY = constraints.TranslationY,
                TranslationZ = constraints.TranslationZ
            };

            _pointSupports.Add(pointSupport);
            
            return this;
        }

        public SupportBuilder AddSurfaceSupport(string name, string surfaceName, Subsoil subsoil)
        {
            // Find the surface by name in the model
            var surface = GeometryBuilder.FindByNameAndType(_model, surfaceName, typeof(StructuralSurfaceMember)) as StructuralSurfaceMember
                ?? throw new ArgumentException($"Surface '{surfaceName}' not found in model for surface support '{name}'");

            var surfaceSupport = new StructuralSurfaceConnection(
                Guid.NewGuid(),
                name,
                surface,
                subsoil);

            _surfaceSupports.Add(surfaceSupport);
            
            return this;
        }

        public void Build()
        {
            var allSupports = new List<IAnalysisObject>();
            allSupports.AddRange(_pointSupports);
            allSupports.AddRange(_surfaceSupports);

            var result = _modelService.AddItemsToModel(_model, allSupports);

            foreach (IAnalysisObject support in allSupports)
            {
                if (!result.TryGetValue(support.Id, out bool created) || !created)
                {
                    throw new InvalidOperationException($"Error: Support '{support.Name}' (ID: {support.Id}) was not successfully created.");
                }
            }

            _model.EnforceModelValidity();

            Console.WriteLine($"Supports created in ADM: {_pointSupports.Count} point supports, {_surfaceSupports.Count} surface supports");
        }
    }

    /// <summary>
    /// General point constraints that can be used for both supports and hinges
    /// </summary>
    public class PointConstraints
    {
        public Constraint<RotationalStiffness?> RotationX { get; set; }
        public Constraint<RotationalStiffness?> RotationY { get; set; }
        public Constraint<RotationalStiffness?> RotationZ { get; set; }
        public Constraint<ForcePerLength?> TranslationX { get; set; }
        public Constraint<ForcePerLength?> TranslationY { get; set; }
        public Constraint<ForcePerLength?> TranslationZ { get; set; }
    }

    /// <summary>
    /// General linear constraints that can be used for both surface supports and linear hinges
    /// </summary>
    public class LinearConstraints
    {
        public Constraint<Pressure?> TranslationX { get; set; }
        public Constraint<Pressure?> TranslationY { get; set; }
        public Constraint<Pressure?> TranslationZ { get; set; }
        public Constraint<RotationalStiffnessPerLength?> RotationX { get; set; }
    }

    /// <summary>
    /// Legacy alias for backwards compatibility
    /// </summary>
    public class PointSupportConstraints : PointConstraints
    {
    }
}
