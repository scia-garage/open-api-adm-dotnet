using ModelExchanger.AnalysisDataModel.Libraries;
using ModelExchanger.AnalysisDataModel.StructuralElements;

namespace SciaApi.AdmModeling.Roundtrip
{
    /// <summary>
    /// Objects found and checked by <see cref="RoundtripVerifier.Verify"/>.
    /// </summary>
    public sealed class RoundtripVerificationResult
    {
        internal RoundtripVerificationResult(
            StructuralCurveMember existingSectionBeam,
            StructuralCurveMember rectangularSectionBeam,
            StructuralParametricCrossSection rectangularSection,
            StructuralMaterial existingMaterial,
            int hingeCount,
            string summary)
        {
            ExistingSectionBeam = existingSectionBeam;
            RectangularSectionBeam = rectangularSectionBeam;
            RectangularSection = rectangularSection;
            ExistingMaterial = existingMaterial;
            HingeCount = hingeCount;
            Summary = summary;
        }

        public StructuralCurveMember ExistingSectionBeam { get; }

        public StructuralCurveMember RectangularSectionBeam { get; }

        public StructuralParametricCrossSection RectangularSection { get; }

        public StructuralMaterial ExistingMaterial { get; }

        /// <summary>
        /// Number of hinges connected to either test beam.
        /// </summary>
        public int HingeCount { get; }

        /// <summary>
        /// Multi-line [VERIFY] report of the checked objects.
        /// </summary>
        public string Summary { get; }
    }
}
