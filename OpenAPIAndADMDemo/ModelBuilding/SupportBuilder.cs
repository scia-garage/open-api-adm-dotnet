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

        public SupportBuilder AddPointSupport(string name, string nodeName, PointSupportConstraints constraints)
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

            var freeXRotationContraint = new PointSupportConstraints
            {
                RotationX = freeRotation,
                RotationY = fixedRotation,
                RotationZ = fixedRotation,
                TranslationX = fixedTranslation,
                TranslationY = fixedTranslation,
                TranslationZ = fixedTranslation
            };
            var allFixedConstraint = new PointSupportConstraints
            {
                RotationX = fixedRotation,
                RotationY = fixedRotation,
                RotationZ = fixedRotation,
                TranslationX = fixedTranslation,
                TranslationY = fixedTranslation,
                TranslationZ = fixedTranslation
            };

            // Add the point supports under the columns
            AddPointSupport("PS1", "N1", freeXRotationContraint);
            AddPointSupport("PS2", "N2", freeXRotationContraint);
            AddPointSupport("PS3", "N3", freeXRotationContraint);
            AddPointSupport("PS4", "N4", freeXRotationContraint);
            AddPointSupport("PS5", "N5", allFixedConstraint);

            // Add surface support under the bottom slab
            var subsoil = new Subsoil(
                "Subsoil",
                SpecificWeight.FromMeganewtonsPerCubicMeter(80.5),
                SpecificWeight.FromMeganewtonsPerCubicMeter(35.5),
                SpecificWeight.FromMeganewtonsPerCubicMeter(50),
                ForcePerLength.FromMeganewtonsPerMeter(15.5),
                ForcePerLength.FromMeganewtonsPerMeter(10.2)
            );

            AddSurfaceSupport("SS1", "S2", subsoil);

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

    public class PointSupportConstraints
    {
        public Constraint<RotationalStiffness?> RotationX { get; set; }
        public Constraint<RotationalStiffness?> RotationY { get; set; }
        public Constraint<RotationalStiffness?> RotationZ { get; set; }
        public Constraint<ForcePerLength?> TranslationX { get; set; }
        public Constraint<ForcePerLength?> TranslationY { get; set; }
        public Constraint<ForcePerLength?> TranslationZ { get; set; }
    }
}
