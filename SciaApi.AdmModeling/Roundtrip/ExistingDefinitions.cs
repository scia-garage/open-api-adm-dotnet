using ModelExchanger.AnalysisDataModel.Libraries;
using ModelExchanger.AnalysisDataModel.StructuralElements;

namespace SciaApi.AdmModeling.Roundtrip
{
    /// <summary>
    /// Cross-section and material that already exist in a loaded ADM model and are reused by the roundtrip test.
    /// </summary>
    public sealed class ExistingDefinitions
    {
        public ExistingDefinitions(StructuralCurveMember sourceBeam, StructuralCrossSection section, StructuralMaterial material)
        {
            SourceBeam = sourceBeam;
            Section = section;
            Material = material;
        }

        /// <summary>
        /// Curve member the pair was taken from; null when a standalone cross-section was used.
        /// </summary>
        public StructuralCurveMember SourceBeam { get; }

        public StructuralCrossSection Section { get; }

        public StructuralMaterial Material { get; }
    }
}
