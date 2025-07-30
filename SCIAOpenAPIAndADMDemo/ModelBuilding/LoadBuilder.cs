using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using ModelExchanger.AnalysisDataModel.Loads;
using ModelExchanger.AnalysisDataModel.StructuralReferences.Points;
using ModelExchanger.AnalysisDataModel.StructuralReferences.Curves;
using ModelExchanger.AnalysisDataModel;
using System;
using System.Collections.Generic;
using UnitsNet;

namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Builds various types of structural loads and moments
    /// </summary>
    public class LoadBuilder : IModelBuilder
    {
        private readonly AnalysisModel _model;
        private readonly IAnalysisModelService _modelService;
        private readonly List<IAnalysisObject> _loads;

        public LoadBuilder(AnalysisModel model, IAnalysisModelService modelService)
        {
            _model = model;
            _modelService = modelService;
            _loads = new List<IAnalysisObject>();
        }

        /// <summary>
        /// Adds a point force on a specific node
        /// </summary>
        /// <param name="name">Name of the load</param>
        /// <param name="nodeName">Name of the node</param>
        /// <param name="loadCaseName">Name of the load case</param>
        /// <param name="force">Force value in kN</param>
        /// <param name="direction">Direction of the force</param>
        /// <returns>LoadBuilder for method chaining</returns>
        public LoadBuilder AddPointForceOnNode(string name, string nodeName, string loadCaseName, double force, ActionDirection direction)
        {
            var node = GeometryBuilder.FindByNameAndType(_model, nodeName, typeof(StructuralPointConnection)) as StructuralPointConnection
                ?? throw new ArgumentException($"Node '{nodeName}' not found");

            var loadCase = LoadCaseBuilder.FindByName(_model, loadCaseName)
                ?? throw new ArgumentException($"Load case '{loadCaseName}' not found");

            var pointLoad = new StructuralPointAction<PointStructuralReferenceOnPoint>(
                Guid.NewGuid(),
                name,
                Force.FromKilonewtons(force),
                loadCase,
                PointForceAction.InNode,
                new PointStructuralReferenceOnPoint(node))
            {
                Direction = direction
            };

            _loads.Add(pointLoad);
            return this;
        }

        /// <summary>
        /// Adds a point force on a beam at a specific relative position
        /// </summary>
        /// <param name="name">Name of the load</param>
        /// <param name="beamName">Name of the beam</param>
        /// <param name="loadCaseName">Name of the load case</param>
        /// <param name="force">Force value in kN</param>
        /// <param name="direction">Direction of the force</param>
        /// <param name="relativePosition">Relative position on beam (0.0 to 1.0)</param>
        /// <returns>LoadBuilder for method chaining</returns>
        public LoadBuilder AddPointForceOnMember(string name, string beamName, string loadCaseName, double force, ActionDirection direction, double relativePosition = 0.5)
        {
            var beam = GeometryBuilder.FindByNameAndType(_model, beamName, typeof(StructuralCurveMember)) as StructuralCurveMember
                ?? throw new ArgumentException($"Beam '{beamName}' not found");

            var loadCase = LoadCaseBuilder.FindByName(_model, loadCaseName)
                ?? throw new ArgumentException($"Load case '{loadCaseName}' not found");

            var beamReference = new PointStructuralReferenceOnBeam(beam)
            {
                Origin = Origin.FromStart,
                CoordinateDefinition = CoordinateDefinition.Relative,
                RelativePositionX = relativePosition,
                Repeat = 1
            };

            var pointLoad = new StructuralPointAction<PointStructuralReferenceOnBeam>(
                Guid.NewGuid(),
                name,
                Force.FromKilonewtons(force),
                loadCase,
                PointForceAction.InNode,
                beamReference)
            {
                Direction = direction
            };

            _loads.Add(pointLoad);
            return this;
        }

        /// <summary>
        /// Adds a line load on a beam
        /// </summary>
        /// <param name="name">Name of the load</param>
        /// <param name="memberName">Name of the 1D member</param>
        /// <param name="loadCaseName">Name of the load case</param>
        /// <param name="force">Force per length value in kN/m</param>
        /// <param name="direction">Direction of the force</param>
        /// <param name="distribution">Distribution type</param>
        /// <returns>LoadBuilder for method chaining</returns>
        public LoadBuilder AddLineForceOnMember(string name, string memberName, string loadCaseName, double force, ActionDirection direction, CurveDistribution distribution = CurveDistribution.Uniform)
        {
            var beam = GeometryBuilder.FindByNameAndType(_model, memberName, typeof(StructuralCurveMember)) as StructuralCurveMember
                ?? throw new ArgumentException($"Member '{memberName}' not found");

            var loadCase = LoadCaseBuilder.FindByName(_model, loadCaseName)
                ?? throw new ArgumentException($"Load case '{loadCaseName}' not found");

            var lineLoad = new StructuralCurveAction<CurveStructuralReferenceOnBeam>(
                Guid.NewGuid(),
                name,
                CurveForceAction.OnBeam,
                ForcePerLength.FromKilonewtonsPerMeter(force),
                loadCase,
                new CurveStructuralReferenceOnBeam(beam))
            {
                Direction = direction,
                Distribution = distribution
            };

            _loads.Add(lineLoad);
            return this;
        }

        /// <summary>
        /// Adds a line load on a surface edge
        /// </summary>
        /// <param name="name">Name of the load</param>
        /// <param name="surfaceName">Name of the surface</param>
        /// <param name="edgeIndex">Index of the edge (0-based)</param>
        /// <param name="loadCaseName">Name of the load case</param>
        /// <param name="force">Force per length value in kN/m</param>
        /// <param name="direction">Direction of the force</param>
        /// <returns>LoadBuilder for method chaining</returns>
        public LoadBuilder AddLineForceOnSurfaceEdge(string name, string surfaceName, int edgeIndex, string loadCaseName, double force, ActionDirection direction)
        {
            var surface = GeometryBuilder.FindByNameAndType(_model, surfaceName, typeof(StructuralSurfaceMember)) as StructuralSurfaceMember
                ?? throw new ArgumentException($"Surface '{surfaceName}' not found");

            var loadCase = LoadCaseBuilder.FindByName(_model, loadCaseName)
                ?? throw new ArgumentException($"Load case '{loadCaseName}' not found");

            var edgeLoad = new StructuralCurveAction<CurveStructuralReferenceOnEdge>(
                Guid.NewGuid(),
                name,
                CurveForceAction.OnEdge,
                ForcePerLength.FromKilonewtonsPerMeter(force),
                loadCase,
                new CurveStructuralReferenceOnEdge(surface, edgeIndex))
            {
                Direction = direction
            };

            _loads.Add(edgeLoad);
            return this;
        }

        /// <summary>
        /// Adds a surface load on a slab/surface
        /// </summary>
        /// <param name="name">Name of the load</param>
        /// <param name="surfaceName">Name of the surface</param>
        /// <param name="loadCaseName">Name of the load case</param>
        /// <param name="pressure">Pressure value in kN/m²</param>
        /// <param name="direction">Direction of the load</param>
        /// <param name="location">Location specification</param>
        /// <returns>LoadBuilder for method chaining</returns>
        public LoadBuilder AddSurfaceLoad(string name, string surfaceName, string loadCaseName, double pressure, ActionDirection direction, Location location = Location.Length)
        {
            var surface = GeometryBuilder.FindByNameAndType(_model, surfaceName, typeof(StructuralSurfaceMember)) as StructuralSurfaceMember
                ?? throw new ArgumentException($"Surface '{surfaceName}' not found");

            var loadCase = LoadCaseBuilder.FindByName(_model, loadCaseName)
                ?? throw new ArgumentException($"Load case '{loadCaseName}' not found");

            var surfaceLoad = new StructuralSurfaceAction(
                Guid.NewGuid(),
                name,
                Pressure.FromKilonewtonsPerSquareMeter(pressure),
                surface,
                loadCase)
            {
                Direction = direction,
                Location = location
            };

            _loads.Add(surfaceLoad);
            return this;
        }

        /// <summary>
        /// Adds a line moment on a beam
        /// </summary>
        /// <param name="name">Name of the moment</param>
        /// <param name="beamName">Name of the beam</param>
        /// <param name="loadCaseName">Name of the load case</param>
        /// <param name="moment">Moment per length value in kNm/m</param>
        /// <param name="direction">Direction of the moment</param>
        /// <param name="distribution">Distribution type</param>
        /// <returns>LoadBuilder for method chaining</returns>
        public LoadBuilder AddLineMomentOnMember(string name, string beamName, string loadCaseName, double moment, MomentDirection direction, CurveDistribution distribution = CurveDistribution.Uniform)
        {
            var beam = GeometryBuilder.FindByNameAndType(_model, beamName, typeof(StructuralCurveMember)) as StructuralCurveMember
                ?? throw new ArgumentException($"Beam '{beamName}' not found");

            var loadCase = LoadCaseBuilder.FindByName(_model, loadCaseName)
                ?? throw new ArgumentException($"Load case '{loadCaseName}' not found");

            var lineMoment = new StructuralCurveMoment<CurveStructuralReferenceOnBeam>(
                Guid.NewGuid(),
                name,
                CurveForceAction.OnBeam,
                TorquePerLength.FromKilonewtonMetersPerMeter(moment),
                loadCase,
                new CurveStructuralReferenceOnBeam(beam))
            {
                Direction = direction,
                Distribution = distribution
            };

            _loads.Add(lineMoment);
            return this;
        }

        /// <summary>
        /// Sets up default loads for the demo model
        /// </summary>
        /// <returns>LoadBuilder for method chaining</returns>
        public LoadBuilder SetupDefaultLoads()
        {
            // Line loads on columns
            AddLineForceOnMember("LL_C1", "C1", "LC1", 5.0, ActionDirection.X);
            AddLineForceOnMember("LL_C2", "C2", "LC1", -5.0, ActionDirection.Y);
            AddLineForceOnMember("LL_C5", "C5", "LC1", -5.0, ActionDirection.Y);

            // Edge loads on surface S1
            AddLineForceOnSurfaceEdge("EL_S1E1", "S1", 0, "LC1", -5.0, ActionDirection.Z);
            AddLineForceOnSurfaceEdge("EL_S1E3", "S1", 2, "LC1", -5.0, ActionDirection.Z);

            // Surface load on S1
            AddSurfaceLoad("SL_S1", "S1", "LC2", -7.0, ActionDirection.Z);

            // Point forces
            AddPointForceOnNode("FP_N15", "N15", "LC2", -150.0, ActionDirection.Z);
            AddPointForceOnMember("PF_C5", "C5", "LC2", -150.0, ActionDirection.X, 0.5);

            // Line moment
            AddLineMomentOnMember("LM_B3", "B3", "LC1", -5.0, MomentDirection.Mx);

            return this;
        }

        /// <summary>
        /// Builds all loads and adds them to the model
        /// </summary>
        public void Build()
        {
            if (_loads.Count == 0)
            {
                return; // Nothing to build
            }

            var result = _modelService.AddItemsToModel(_model, _loads);

            foreach (IAnalysisObject load in _loads)
            {
                if (!result.TryGetValue(load.Id, out bool created) || !created)
                {
                    throw new InvalidOperationException($"Error: Load '{load.Name}' (ID: {load.Id}) was not successfully created.");
                }
            }

            _model.EnforceModelValidity();

            Console.WriteLine($"Loads created in ADM: {_loads.Count} loads");
        }
    }
}
