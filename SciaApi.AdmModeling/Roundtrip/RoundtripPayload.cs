using System.Collections.Generic;
using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Libraries;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.StructuralElements;

namespace SciaApi.AdmModeling.Roundtrip
{
    /// <summary>
    /// Scratch model and object lists describing the test delta of one roundtrip scenario.
    /// </summary>
    public sealed class RoundtripPayload
    {
        internal RoundtripPayload(
            AnalysisModel scratchModel,
            IReadOnlyList<IAnalysisObject> references,
            IReadOnlyList<IAnalysisObject> newObjects,
            StructuralMaterial existingMaterial,
            StructuralCrossSection existingSection,
            StructuralCurveMember sourceBeam,
            StructuralParametricCrossSection testSection,
            IReadOnlyList<StructuralPointConnection> nodes,
            StructuralCurveMember existingSectionBeam,
            StructuralCurveMember rectangularSectionBeam,
            RelConnectsStructuralMember pointHinge)
        {
            ScratchModel = scratchModel;
            References = references;
            NewObjects = newObjects;
            ExistingMaterial = existingMaterial;
            ExistingSection = existingSection;
            SourceBeam = sourceBeam;
            TestSection = testSection;
            Nodes = nodes;
            ExistingSectionBeam = existingSectionBeam;
            RectangularSectionBeam = rectangularSectionBeam;
            PointHinge = pointHinge;
        }

        /// <summary>
        /// New model holding <see cref="References"/> followed by <see cref="NewObjects"/>. The source model is never modified.
        /// </summary>
        public AnalysisModel ScratchModel { get; }

        /// <summary>
        /// Existing objects that the new objects reference: project and model information when present, then material and cross-section.
        /// </summary>
        public IReadOnlyList<IAnalysisObject> References { get; }

        /// <summary>
        /// New objects in dependency order: test section, four nodes, two beams and the optional hinge.
        /// </summary>
        public IReadOnlyList<IAnalysisObject> NewObjects { get; }

        public StructuralMaterial ExistingMaterial { get; }

        public StructuralCrossSection ExistingSection { get; }

        /// <summary>
        /// Curve member the existing pair was taken from; null when a standalone cross-section was used.
        /// </summary>
        public StructuralCurveMember SourceBeam { get; }

        public StructuralParametricCrossSection TestSection { get; }

        public IReadOnlyList<StructuralPointConnection> Nodes { get; }

        /// <summary>
        /// Beam that uses <see cref="ExistingSection"/>.
        /// </summary>
        public StructuralCurveMember ExistingSectionBeam { get; }

        /// <summary>
        /// Beam that uses <see cref="TestSection"/>.
        /// </summary>
        public StructuralCurveMember RectangularSectionBeam { get; }

        /// <summary>
        /// Point hinge on <see cref="ExistingSectionBeam"/>; null when no hinge was requested.
        /// </summary>
        public RelConnectsStructuralMember PointHinge { get; }
    }
}
