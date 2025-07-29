using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using ModelExchanger.AnalysisDataModel.Subtypes;
using System;
using System.Collections.Generic;
using UnitsNet;
using ModelExchanger.AnalysisDataModel;

namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Builds structural hinges for the model
    /// </summary>
    public class HingeBuilder : IModelBuilder
    {
        private readonly AnalysisModel _model;
        private readonly IAnalysisModelService _modelService;
        private readonly List<RelConnectsStructuralMember> _pointHinges;
        private readonly List<RelConnectsSurfaceEdge> _linearHinges;

        public HingeBuilder(AnalysisModel model, IAnalysisModelService modelService)
        {
            _model = model;
            _modelService = modelService;
            _pointHinges = new List<RelConnectsStructuralMember>();
            _linearHinges = new List<RelConnectsSurfaceEdge>();
        }

        public HingeBuilder AddPointHinge(string name, string memberName, Position position, PointHingeConstraints constraints)
        {
            // Find the member by name in the model
            var member = GeometryBuilder.FindByNameAndType(_model, memberName, typeof(StructuralCurveMember)) as StructuralCurveMember
                ?? throw new ArgumentException($"Member '{memberName}' not found in model for point hinge '{name}'");

            var pointHinge = new RelConnectsStructuralMember(
                Guid.NewGuid(),
                name,
                member)
            {
                Position = position,
                TranslationX = constraints.TranslationX,
                TranslationY = constraints.TranslationY,
                TranslationZ = constraints.TranslationZ,
                RotationX = constraints.RotationX,
                RotationY = constraints.RotationY,
                RotationZ = constraints.RotationZ
            };

            _pointHinges.Add(pointHinge);
            
            return this;
        }

        public HingeBuilder AddLinearHinge(string name, string surfaceName, int edgeIndex, double startPointRelative, double endPointRelative, LinearHingeConstraints constraints)
        {
            // Find the surface by name in the model
            var surface = GeometryBuilder.FindByNameAndType(_model, surfaceName, typeof(StructuralSurfaceMember)) as StructuralSurfaceMember
                ?? throw new ArgumentException($"Surface '{surfaceName}' not found in model for linear hinge '{name}'");

            var linearHinge = new RelConnectsSurfaceEdge(
                Guid.NewGuid(),
                name,
                surface,
                edgeIndex)
            {
                StartPointRelative = startPointRelative,
                EndPointRelative = endPointRelative,
                TranslationX = constraints.TranslationX,
                TranslationY = constraints.TranslationY,
                TranslationZ = constraints.TranslationZ,
                RotationX = constraints.RotationX
            };

            _linearHinges.Add(linearHinge);
            
            return this;
        }

        public HingeBuilder SetupDefaultHinges()
        {
            // Define standard constraint values for point hinges
            var freeRotation = new Constraint<RotationalStiffness?>(
                ConstraintType.Free, 
                RotationalStiffness.FromKilonewtonMetersPerRadian(0));

            var fixedRotation = new Constraint<RotationalStiffness?>(
                ConstraintType.Rigid, 
                RotationalStiffness.FromKilonewtonMetersPerRadian(1e+10));

            var fixedTranslation = new Constraint<ForcePerLength?>(
                ConstraintType.Rigid, 
                ForcePerLength.FromKilonewtonsPerMeter(1e+10));

            var pointConstraints = new PointHingeConstraints
            {
                TranslationX = fixedTranslation,
                TranslationY = fixedTranslation,
                TranslationZ = fixedTranslation,
                RotationX = fixedRotation,
                RotationY = freeRotation,
                RotationZ = fixedRotation
            };

            // Add point hinges to one of the top beams
            AddPointHinge("H1", "B3", Position.Both, pointConstraints);

            // Define standard constraint values for linear hinges
            var fixedTranslationLine = new Constraint<Pressure?>(
                ConstraintType.Rigid, 
                Pressure.FromKilopascals(1e+10));

            var freeRotationLine = new Constraint<RotationalStiffnessPerLength?>(
                ConstraintType.Free, 
                RotationalStiffnessPerLength.FromKilonewtonMetersPerRadianPerMeter(0));

            var freeRotationFixedTranslationConstraint = new LinearHingeConstraints
            {
                TranslationX = fixedTranslationLine,
                TranslationY = fixedTranslationLine,
                TranslationZ = fixedTranslationLine,
                RotationX = freeRotationLine
            };

            // Add linear hinges from the original code (assuming S3 surface exists)
            AddLinearHinge("LH1", "S3", 0, 0, 1, freeRotationFixedTranslationConstraint);
            // AddLinearHinge("LH2", "S3", 1, 0, 1, linearConstraints);
            AddLinearHinge("LH3", "S3", 2, 0, 1, freeRotationFixedTranslationConstraint);
            // AddLinearHinge("LH4", "S3", 3, 0, 1, linearConstraints);

            return this;
        }

        public void Build()
        {
            var allHinges = new List<IAnalysisObject>();
            allHinges.AddRange(_pointHinges);
            allHinges.AddRange(_linearHinges);

            var result = _modelService.AddItemsToModel(_model, allHinges);

            foreach (IAnalysisObject hinge in allHinges)
            {
                if (!result.TryGetValue(hinge.Id, out bool created) || !created)
                {
                    throw new InvalidOperationException($"Error: Hinge '{hinge.Name}' (ID: {hinge.Id}) was not successfully created.");
                }
            }
    
            _model.EnforceModelValidity();

            Console.WriteLine($"Hinges created in ADM: {_pointHinges.Count} point hinges, {_linearHinges.Count} linear hinges");
        }
    }

    public class PointHingeConstraints
    {
        public Constraint<ForcePerLength?> TranslationX { get; set; }
        public Constraint<ForcePerLength?> TranslationY { get; set; }
        public Constraint<ForcePerLength?> TranslationZ { get; set; }
        public Constraint<RotationalStiffness?> RotationX { get; set; }
        public Constraint<RotationalStiffness?> RotationY { get; set; }
        public Constraint<RotationalStiffness?> RotationZ { get; set; }
    }

    public class LinearHingeConstraints
    {
        public Constraint<Pressure?> TranslationX { get; set; }
        public Constraint<Pressure?> TranslationY { get; set; }
        public Constraint<Pressure?> TranslationZ { get; set; }
        public Constraint<RotationalStiffnessPerLength?> RotationX { get; set; }
    }
}
