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

        public GeometryBuilder SetupDefaultGeometry()
        {
            // Model dimensions
            double a = 4.0, b = 5.0, c = 3.0;

            // Opening dimensions for regions and openings
            double lengthOpening = 1.0;
            double widthOpening = 1.0;

            // Bottom level nodes
            AddNode("N1", 0, 0, 0);
            AddNode("N2", a, 0, 0);
            AddNode("N3", a, b, 0);
            AddNode("N4", 0, b, 0);
            AddNode("N5", 2*a, 0, 0);

            // Top level nodes
            AddNode("N11", 0, 0, c);
            AddNode("N12", a, 0, c);
            AddNode("N13", a, b, c);
            AddNode("N14", 0, b, c);
            AddNode("N15", 2*a, 0, c);

            // Columns
            AddLineMember("C1", "N1", "N11", "HEA260", Member1DType.Column, "Columns");
            AddLineMember("C2", "N2", "N12", "HEA260", Member1DType.Column, "Columns");
            AddLineMember("C3", "N3", "N13", "HEA260", Member1DType.Column, "Columns");
            AddLineMember("C4", "N4", "N14", "HEA260", Member1DType.Column, "Columns");
            AddLineMember("C5", "N5", "N15", "HEA260", Member1DType.Column, "Columns");

            // Top level beams
            AddLineMember("B1", "N11", "N12", "HEA260", Member1DType.Beam, "Beams");
            AddLineMember("B2", "N13", "N14", "HEA260", Member1DType.Beam, "Beams");
            AddLineMember("B3", "N12", "N15", "HEA260", Member1DType.Beam, "Beams");

            // Top slab
            AddSurfaceMember("S1", new string[] { "N11", "N12", "N13", "N14" }, "Concrete", 0.3, Member2DType.Plate);

            // Bottom slab
            AddSurfaceMember("S2", new string[] { "N1", "N2", "N3", "N4" }, "Concrete", 0.3);

            // Side wall
            AddSurfaceMember("S3", new string[] { "N3", "N4", "N14", "N13" }, "Concrete", 0.3, Member2DType.Wall);

            // Additional nodes for opening in top slab  
            AddNode("N101", 0.5 * a - 0.5 * lengthOpening, 0.5 * b - 0.5 * widthOpening, c);
            AddNode("N102", 0.5 * a + 0.5 * lengthOpening, 0.5 * b - 0.5 * widthOpening, c);
            AddNode("N103", 0.5 * a + 0.5 * lengthOpening, 0.5 * b + 0.5 * widthOpening, c);
            AddNode("N104", 0.5 * a - 0.5 * lengthOpening, 0.5 * b + 0.5 * widthOpening, c);

            // Opening in top slab
            AddOpening("O1", "S1", new string[] { "N101", "N102", "N103", "N104" });

            // Additional nodes for the region with different thickness on bottom slab
            AddNode("N111", 0.5 * a - 0.5 * lengthOpening, 0.5 * b - 0.5 * widthOpening, 0);
            AddNode("N112", 0.5 * a + 0.5 * lengthOpening, 0.5 * b - 0.5 * widthOpening, 0);
            AddNode("N113", 0.5 * a + 0.5 * lengthOpening, 0.5 * b + 0.5 * widthOpening, 0);
            AddNode("N114", 0.5 * a - 0.5 * lengthOpening, 0.5 * b + 0.5 * widthOpening, 0);

            // Region on bottom slab with different thickness
            AddRegion("Region", "S2", new string[] { "N111", "N112", "N113", "N114" }, "Concrete", 0.6);

            return this;
        }

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
