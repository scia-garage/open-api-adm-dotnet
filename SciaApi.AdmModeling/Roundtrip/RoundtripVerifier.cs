using System;
using System.Collections.Generic;
using System.Linq;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Libraries;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.StructuralElements;

namespace SciaApi.AdmModeling.Roundtrip
{
    /// <summary>
    /// Read-back verification of the roundtrip test objects in a loaded or saved ADM model.
    /// </summary>
    public static class RoundtripVerifier
    {
        private const string ExistingSectionRole = "_Beam_ExistingSection";
        private const string RectangleRole = "_Beam_Rectangle";
        private const string LegacyExistingSectionInfix = "OpenApiRoundTrip_ExistingSection_Beam";
        private const string LegacyExistingSectionSuffix = "_BeamExistingSection";
        private const string LegacyRectangleInfix = "OpenApiRoundTrip_Rectangle_Beam";
        private const string LegacyRectangleSuffix = "_BeamRectangularSection";
        private const string RectangleNameMarker = "300x500";
        private const double RectangleHeightMillimetres = 500.0;
        private const double RectangleWidthMillimetres = 300.0;
        private const double DimensionToleranceMillimetres = 0.01;

        /// <summary>
        /// Verifies the existing-section beam, the 300 x 500 mm rectangular beam, their shared material and,
        /// when requested, a hinge connected to one of the two beams.
        /// </summary>
        /// <param name="model">Loaded or saved model.</param>
        /// <param name="expectHinge">True when a hinge connected to one of the test beams must exist.</param>
        /// <param name="namePrefix">Prefix used when the objects were built; null or empty accepts any test beam name.</param>
        /// <returns>The verified objects and their report.</returns>
        /// <remarks>
        /// When several beams match, the last one in ordinal name order is verified, which is the most recent numbered run.
        /// The prototype names (OpenApiRoundTrip_*, *_BeamExistingSection, *_BeamRectangularSection) are used only when no beam matches the current names.
        /// </remarks>
        /// <exception cref="InvalidOperationException">A read-back check failed.</exception>
        public static RoundtripVerificationResult Verify(AnalysisModel model, bool expectHinge, string namePrefix = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            string prefix = string.IsNullOrWhiteSpace(namePrefix) ? null : namePrefix.Trim();
            Dictionary<Guid, StructuralCrossSection> sectionsById = ModelLookup.ById<StructuralCrossSection>(model);
            Dictionary<Guid, StructuralMaterial> materialsById = ModelLookup.ById<StructuralMaterial>(model);
            List<StructuralCurveMember> beams = model.OfType<StructuralCurveMember>().ToList();

            StructuralCurveMember existingSectionBeam = SelectTestBeam(
                beams,
                prefix,
                ExistingSectionRole,
                LegacyExistingSectionInfix,
                LegacyExistingSectionSuffix);
            if (existingSectionBeam == null ||
                existingSectionBeam.CrossSection == null ||
                !sectionsById.ContainsKey(existingSectionBeam.CrossSection.Id))
            {
                throw new InvalidOperationException(
                    "Read-back verification failed: the test beam using an existing cross-section is missing or its section reference was not retained.");
            }

            StructuralCurveMember rectangularSectionBeam = SelectTestBeam(
                beams,
                prefix,
                RectangleRole,
                LegacyRectangleInfix,
                LegacyRectangleSuffix);
            StructuralParametricCrossSection rectangularSection = rectangularSectionBeam?.CrossSection as StructuralParametricCrossSection;
            if (rectangularSection == null ||
                !sectionsById.ContainsKey(rectangularSection.Id) ||
                ModelLookup.NameOf(rectangularSection).IndexOf(RectangleNameMarker, StringComparison.OrdinalIgnoreCase) < 0 ||
                rectangularSection.Shape != ProfileLibraryId.Rectangle ||
                rectangularSection.Parameters == null ||
                rectangularSection.Parameters.Count < 2 ||
                Math.Abs(rectangularSection.Parameters[0].Millimeters - RectangleHeightMillimetres) > DimensionToleranceMillimetres ||
                Math.Abs(rectangularSection.Parameters[1].Millimeters - RectangleWidthMillimetres) > DimensionToleranceMillimetres)
            {
                throw new InvalidOperationException(
                    "Read-back verification failed: the 300 x 500 mm parametric rectangular cross-section or its beam reference was not retained.");
            }

            if (existingSectionBeam.CrossSection.Material == null ||
                !materialsById.TryGetValue(existingSectionBeam.CrossSection.Material.Id, out StructuralMaterial existingMaterial) ||
                rectangularSection.Material == null ||
                !materialsById.TryGetValue(rectangularSection.Material.Id, out StructuralMaterial rectangularMaterial) ||
                existingMaterial.Id != rectangularMaterial.Id)
            {
                throw new InvalidOperationException(
                    "Read-back verification failed: the existing and test cross-sections do not reference a retained shared project material.");
            }

            List<RelConnectsStructuralMember> hinges = model.OfType<RelConnectsStructuralMember>()
                .Where(hinge => hinge.Member != null &&
                                (hinge.Member.Id == existingSectionBeam.Id || hinge.Member.Id == rectangularSectionBeam.Id))
                .ToList();
            if (expectHinge && hinges.Count == 0)
            {
                throw new InvalidOperationException(
                    "Read-back verification failed: a hinge was expected on one of the two test beams, but none was found.");
            }

            string summary = string.Join(
                System.Environment.NewLine,
                $"[VERIFY] Existing-section beam: {existingSectionBeam.Name} [{existingSectionBeam.Id}], section '{existingSectionBeam.CrossSection.Name}' [{existingSectionBeam.CrossSection.Id}].",
                $"[VERIFY] Rectangular beam: {rectangularSectionBeam.Name} [{rectangularSectionBeam.Id}], section '{rectangularSection.Name}' [{rectangularSection.Id}], profile {rectangularSection.Shape}, parameters {rectangularSection.Parameters[0].Millimeters:0.###} x {rectangularSection.Parameters[1].Millimeters:0.###} mm.",
                $"[VERIFY] Shared existing material: {existingMaterial.Name} [{existingMaterial.Id}].",
                $"[VERIFY] Hinges connected to test beams: {hinges.Count}.",
                "[VERIFY] Saved ADM references and requested test objects are present.");

            return new RoundtripVerificationResult(
                existingSectionBeam,
                rectangularSectionBeam,
                rectangularSection,
                existingMaterial,
                hinges.Count,
                summary);
        }

        private static StructuralCurveMember SelectTestBeam(
            List<StructuralCurveMember> beams,
            string prefix,
            string role,
            string legacyInfix,
            string legacySuffix)
        {
            List<StructuralCurveMember> candidates = beams
                .Where(beam => prefix == null
                    ? ModelLookup.NameOf(beam).IndexOf(role, StringComparison.OrdinalIgnoreCase) >= 0
                    : ModelLookup.NameOf(beam).StartsWith(prefix + role, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 0)
            {
                candidates = beams
                    .Where(beam => ModelLookup.NameOf(beam).IndexOf(legacyInfix, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   ModelLookup.NameOf(beam).EndsWith(legacySuffix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return candidates
                .OrderBy(beam => ModelLookup.NameOf(beam), StringComparer.OrdinalIgnoreCase)
                .ThenBy(beam => beam.Id)
                .LastOrDefault();
        }
    }
}
