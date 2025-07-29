using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Loads;
using ModelExchanger.AnalysisDataModel.Subtypes;
using System;
using System.Collections.Generic;

namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Builds load combinations for the model
    /// </summary>
    public class LoadCombinationBuilder : IModelBuilder
    {
        private readonly AnalysisModel _model;
        private readonly IAnalysisModelService _modelService;
        private readonly List<StructuralLoadCombination> _loadCombinations;

        public LoadCombinationBuilder(AnalysisModel model, IAnalysisModelService modelService)
        {
            _model = model;
            _modelService = modelService;
            _loadCombinations = new List<StructuralLoadCombination>();
        }

        /// <summary>
        /// Data structure to hold load case factors for a combination
        /// </summary>
        public class LoadCaseFactors
        {
            public string LoadCaseName { get; set; }
            public double VariableFactor { get; set; }
            public double SafetyFactor { get; set; }

            public LoadCaseFactors(string loadCaseName, double variableFactor, double safetyFactor)
            {
                LoadCaseName = loadCaseName;
                VariableFactor = variableFactor;
                SafetyFactor = safetyFactor;
            }
        }

        /// <summary>
        /// Adds a load combination with multiple load cases and their factors
        /// </summary>
        /// <param name="name">Name of the load combination</param>
        /// <param name="category">Category of the load combination</param>
        /// <param name="loadCaseFactors">List of load cases with their factors</param>
        /// <param name="nationalStandard">Optional national standard</param>
        /// <param name="id">Optional specific ID for the combination</param>
        /// <returns>LoadCombinationBuilder for method chaining</returns>
        public LoadCombinationBuilder AddLoadCombination(string name, LoadCaseCombinationCategory category, 
            List<LoadCaseFactors> loadCaseFactors, LoadCaseCombinationStandard? nationalStandard = null)
        {
            // Convert load case names to actual load case objects with factors
            var combinationData = new List<StructuralLoadCombinationData>();

            foreach (LoadCaseFactors lcf in loadCaseFactors)
            {
                var loadCase = LoadCaseBuilder.FindByName(_model, lcf.LoadCaseName);
                combinationData.Add(new StructuralLoadCombinationData(loadCase, lcf.VariableFactor, lcf.SafetyFactor));
            }

            var combination = new StructuralLoadCombination(
                Guid.NewGuid(),
                name,
                category,
                combinationData.ToArray());

            if (nationalStandard.HasValue)
            {
                combination.NationalStandard = nationalStandard.Value;
            }

            _loadCombinations.Add(combination);

            return this;
        }

        /// <summary>
        /// Sets up default load combinations for the demo model
        /// </summary>
        /// <returns>LoadCombinationBuilder for method chaining</returns>
        public LoadCombinationBuilder SetupDefaultLoadCombinations()
        {
            // Create a combination with different factors for each load case
            var loadCaseFactors = new List<LoadCaseFactors>
            {
                new LoadCaseFactors("LC1", 1.0, 1.5),
                new LoadCaseFactors("LC2", 1.0, 1.35),
                new LoadCaseFactors("LC3", 1.0, 1.35)
            };

            AddLoadCombination(
                "C1", 
                LoadCaseCombinationCategory.AccordingNationalStandard,
                loadCaseFactors,
                LoadCaseCombinationStandard.EnUlsSetC);

            return this;
        }

        /// <summary>
        /// Builds all load combinations and adds them to the model
        /// </summary>
        public void Build()
        {
            if (_loadCombinations.Count == 0)
            {
                return; // Nothing to build
            }

            var result = _modelService.AddItemsToModel(_model, _loadCombinations);

            foreach (StructuralLoadCombination combination in _loadCombinations)
            {
                if (!result.TryGetValue(combination.Id, out bool created) || !created)
                {
                    throw new InvalidOperationException($"Error: Load combination '{combination.Name}' (ID: {combination.Id}) was not successfully created.");
                }
            }

            _model.EnforceModelValidity();

            Console.WriteLine($"Load combinations created in ADM: {_loadCombinations.Count} combinations");
        }
    }
}
