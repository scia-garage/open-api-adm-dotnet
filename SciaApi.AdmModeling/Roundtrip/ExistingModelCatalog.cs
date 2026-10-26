using System;
using System.Collections.Generic;
using System.Linq;
using ModelExchanger.AnalysisDataModel.Libraries;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.StructuralElements;

namespace SciaApi.AdmModeling.Roundtrip
{
    /// <summary>
    /// Finds the reusable cross-section/material pair and the free space for the roundtrip test beams in a loaded ADM model.
    /// </summary>
    public static class ExistingModelCatalog
    {
        private const double StartOffsetFromMaxNodeXMetres = 2.0;
        private const double BeamLengthMetres = 3.0;
        private const double SecondBeamOffsetYMetres = 2.0;

        /// <summary>
        /// Returns the cross-section/material pair of the first curve member (by name) whose section and material are both in the model.
        /// Falls back to the first standalone cross-section (by name) that has a material in the model.
        /// </summary>
        /// <exception cref="InvalidOperationException">The model has no usable cross-section/material pair.</exception>
        public static ExistingDefinitions Find(AnalysisModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            Dictionary<Guid, StructuralCrossSection> sectionsById = ModelLookup.ById<StructuralCrossSection>(model);
            Dictionary<Guid, StructuralMaterial> materialsById = ModelLookup.ById<StructuralMaterial>(model);
            List<StructuralCurveMember> beams = model.OfType<StructuralCurveMember>().ToList();

            foreach (StructuralCurveMember beam in beams
                         .OrderBy(candidate => ModelLookup.NameOf(candidate), StringComparer.OrdinalIgnoreCase)
                         .ThenBy(candidate => candidate.Id))
            {
                StructuralCrossSection section = beam.CrossSection;
                if (section == null || section.Material == null)
                {
                    continue;
                }

                if (sectionsById.TryGetValue(section.Id, out StructuralCrossSection modelSection) &&
                    materialsById.TryGetValue(section.Material.Id, out StructuralMaterial sourceMaterial))
                {
                    return new ExistingDefinitions(beam, modelSection, sourceMaterial);
                }
            }

            foreach (StructuralCrossSection section in sectionsById.Values
                         .OrderBy(candidate => ModelLookup.NameOf(candidate), StringComparer.OrdinalIgnoreCase)
                         .ThenBy(candidate => candidate.Id))
            {
                if (section.Material != null && materialsById.TryGetValue(section.Material.Id, out StructuralMaterial material))
                {
                    return new ExistingDefinitions(null, section, material);
                }
            }

            throw new InvalidOperationException(
                $"The loaded ADM has no reusable cross-section/material pair. No existing beam with a project cross-section/material or standalone cross-section with its material was found ({beams.Count} beams, {sectionsById.Count} cross-sections, {materialsById.Count} materials).");
        }

        /// <summary>
        /// Places the test beams beside the existing geometry: starting 2 m beyond the largest node X, centred on the
        /// node Y range, at the largest node Z. The second beam is 2 m further in Y and both beams are 3 m long.
        /// </summary>
        /// <exception cref="InvalidOperationException">The model has no structural point connections.</exception>
        public static BeamPlacement FindPlacement(AnalysisModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            List<StructuralPointConnection> nodes = model.OfType<StructuralPointConnection>().ToList();
            if (nodes.Count == 0)
            {
                throw new InvalidOperationException(
                    "The ADM model has no structural point connections, so the test beams cannot be placed clear of the existing geometry.");
            }

            double maxX = nodes.Max(node => node.X.Meters);
            double minY = nodes.Min(node => node.Y.Meters);
            double maxY = nodes.Max(node => node.Y.Meters);
            double maxZ = nodes.Max(node => node.Z.Meters);

            double startX = maxX + StartOffsetFromMaxNodeXMetres;
            double firstBeamY = (minY + maxY) / 2.0;
            return new BeamPlacement(
                startX,
                startX + BeamLengthMetres,
                firstBeamY,
                firstBeamY + SecondBeamOffsetYMetres,
                maxZ);
        }
    }
}
