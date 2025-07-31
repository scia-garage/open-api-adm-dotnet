using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.Subtypes;
using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using ModelExchanger.AnalysisDataModel.Libraries;
using CSInfrastructure;
using System;
using System.Collections.Generic;
using UnitsNet;

namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Builds structural geometry including nodes, beams, and slabs
    /// </summary>
    public class GeometryBuilder : IModelBuilder
    {
        private readonly AnalysisModel _model;
        private readonly IAnalysisModelService _modelService;
        private readonly List<StructuralPointConnection> _nodes;
        private readonly List<StructuralCurveMember> _beams;
        private readonly List<StructuralSurfaceMember> _slabs;
        private readonly List<StructuralSurfaceMemberRegion> _regions;
        private readonly List<StructuralSurfaceMemberOpening> _openings;

        public GeometryBuilder(AnalysisModel model, IAnalysisModelService modelService)
        {
            _model = model;
            _modelService = modelService;
            _nodes = new List<StructuralPointConnection>();
            _beams = new List<StructuralCurveMember>();
            _slabs = new List<StructuralSurfaceMember>();
            _regions = new List<StructuralSurfaceMemberRegion>();
            _openings = new List<StructuralSurfaceMemberOpening>();
        }

        public GeometryBuilder AddNode(string name, double x, double y, double z)
        {
            var node = new StructuralPointConnection(
                Guid.NewGuid(),
                name,
                Length.FromMeters(x),
                Length.FromMeters(y),
                Length.FromMeters(z));
            _nodes.Add(node);
            return this;
        }

        /// <summary>
        /// Adds a line member (beam) to the model.
        /// /// The beam is defined by its start and end nodes, cross-section, and type.
        /// </summary>
        public GeometryBuilder AddLineMember(string name, string startNodeName, string endNodeName, string crossSectionName, Member1DType beamType, string layer = "Beams")
        {
            StructuralPointConnection startNode = _nodes.Find(n => n.Name == startNodeName)
                ?? throw new ArgumentException($"Start node '{startNodeName}' not found");

            StructuralPointConnection endNode = _nodes.Find(n => n.Name == endNodeName)
                ?? throw new ArgumentException($"End node '{endNodeName}' not found");

            StructuralCrossSection crossSection = CrossSectionBuilder.FindByName(_model, crossSectionName)
                ?? throw new ArgumentException($"Cross-section '{crossSectionName}' not found");

            var beamLines = new Curve<StructuralPointConnection>[1]
            {
                new Curve<StructuralPointConnection>(CurveGeometricalShape.Line, new StructuralPointConnection[2] { startNode, endNode })
            };

            var beam = new StructuralCurveMember(
                Guid.NewGuid(),
                name,
                beamLines,
                CrossSectionBuilder.FindByName(_model, crossSectionName))
            {
                Behaviour = CurveBehaviour.Standard,
                SystemLine = CurveAlignment.Centre,
                Type = new FlexibleEnum<Member1DType>(beamType),
                Layer = layer
            };

            _beams.Add(beam);
            return this;
        }

        /// <summary>
        /// Adds a surface member (slab) to the model.
        /// </summary>
        public GeometryBuilder AddSurfaceMember(string name, string[] nodeNames, string materialName, double thickness, Member2DType slabType = Member2DType.Plate)
        {
            var nodes = new List<StructuralPointConnection>();
            foreach (var nodeName in nodeNames)
            {
                var node = _nodes.Find(n => n.Name == nodeName)
                    ?? throw new ArgumentException($"Node '{nodeName}' not found");
                nodes.Add(node);
            }

            StructuralMaterial material = MaterialBuilder.FindByName(_model, materialName)
                ?? throw new ArgumentException($"Material '{materialName}' not found");

            var edgeCurves = new Curve<StructuralPointConnection>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var nextIndex = (i + 1) % nodes.Count;
                edgeCurves[i] = new Curve<StructuralPointConnection>(
                    CurveGeometricalShape.Line,
                    new StructuralPointConnection[2] { nodes[i], nodes[nextIndex] });
            }

            var slab = new StructuralSurfaceMember(
                Guid.NewGuid(),
                name,
                edgeCurves,
                material,
                Length.FromMeters(thickness))
            {
                Type = new FlexibleEnum<Member2DType>(slabType),
                Behaviour = Member2DBehaviour.Isotropic,
                Alignment = Member2DAlignment.Centre,
                Shape = Member2DShape.Flat
            };

            _slabs.Add(slab);
            return this;
        }

        /// <summary>
        /// Adds a region in a slab
        /// /// The region is defined by a set of nodes that form the edges of the region.
        /// </summary>
        public GeometryBuilder AddRegion(string name, string parentSlabName, string[] nodeNames, string materialName, double thickness, Member2DAlignment alignment = Member2DAlignment.Centre)
        {
            // Find the parent slab
            var parentSlab = _slabs.Find(s => s.Name == parentSlabName)
                ?? throw new ArgumentException($"Parent slab '{parentSlabName}' not found");

            // Find the nodes for the region
            var nodes = new List<StructuralPointConnection>();
            foreach (var nodeName in nodeNames)
            {
                var node = _nodes.Find(n => n.Name == nodeName)
                    ?? throw new ArgumentException($"Node '{nodeName}' not found for region '{name}'");
                nodes.Add(node);
            }

            // Find the material
            StructuralMaterial material = MaterialBuilder.FindByName(_model, materialName)
                ?? throw new ArgumentException($"Material '{materialName}' not found for region '{name}'");

            // Create edge curves for the region
            var regionEdges = new Curve<StructuralPointConnection>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var nextIndex = (i + 1) % nodes.Count;
                regionEdges[i] = new Curve<StructuralPointConnection>(
                    CurveGeometricalShape.Line,
                    new StructuralPointConnection[2] { nodes[i], nodes[nextIndex] });
            }

            var region = new StructuralSurfaceMemberRegion(
                Guid.NewGuid(),
                name,
                parentSlab,
                regionEdges,
                material)
            {
                Thickness = Length.FromMeters(thickness),
                Alignment = alignment
            };

            _regions.Add(region);
            return this;
        }

        /// <summary>
        /// Adds an opening in a slab
        /// /// The opening is defined by a set of nodes that form the edges of the opening.
        /// </summary>
        public GeometryBuilder AddOpening(string name, string parentSlabName, string[] nodeNames)
        {
            // Find the parent slab
            var parentSlab = _slabs.Find(s => s.Name == parentSlabName)
                ?? throw new ArgumentException($"Parent slab '{parentSlabName}' not found for opening '{name}'");

            // Find the nodes for the opening
            var nodes = new List<StructuralPointConnection>();
            foreach (var nodeName in nodeNames)
            {
                var node = _nodes.Find(n => n.Name == nodeName)
                    ?? throw new ArgumentException($"Node '{nodeName}' not found for opening '{name}'");
                nodes.Add(node);
            }

            // Create edge curves for the opening
            var openingEdges = new Curve<StructuralPointConnection>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var nextIndex = (i + 1) % nodes.Count;
                openingEdges[i] = new Curve<StructuralPointConnection>(
                    CurveGeometricalShape.Line,
                    new StructuralPointConnection[2] { nodes[i], nodes[nextIndex] });
            }

            var opening = new StructuralSurfaceMemberOpening(
                Guid.NewGuid(),
                name,
                parentSlab,
                openingEdges);

            _openings.Add(opening);
            return this;
        }

        // <summary>
        /// Builds the geometry in the ADM model
        /// /// This method creates all nodes, beams, slabs, regions, and openings defined in the builder.
        /// </summary>
        public void Build()
        {
            var allElements = new List<IAnalysisObject>();
            allElements.AddRange(_nodes);
            allElements.AddRange(_beams);
            allElements.AddRange(_slabs);
            allElements.AddRange(_regions);
            allElements.AddRange(_openings);

            var result = _modelService.AddItemsToModel(_model, allElements);

            foreach (IAnalysisObject element in allElements)
            {
                if (!result.TryGetValue(element.Id, out bool created) || !created)
                {
                    throw new InvalidOperationException($"Error: Geometry element '{element.Name}' (ID: {element.Id}) was not successfully created.");
                }
            }

            _model.EnforceModelValidity();

            Console.WriteLine($"Geometry created in ADM: {_nodes.Count} nodes, {_beams.Count} beams, {_slabs.Count} slabs, {_regions.Count} regions, {_openings.Count} openings");
        }
        /// <summary>
        /// Finds a geometry element in the model by its name and type
        /// </summary>
        public static IAnalysisObject FindByNameAndType(AnalysisModel model, string name, Type type)
        {
            foreach (IAnalysisObject item in model)
            {
                if (item.GetType() == type && item.Name == name)
                {
                    return item;
                }
            }
            throw new ArgumentException($"Item of type {type.Name} with name '{name}' not found in model.");
        }
    }
}
