using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Loads;
using ModelExchanger.AnalysisDataModel;
using CSInfrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Builds load groups and load cases for the model using the grouping pattern
    /// </summary>
    public class LoadCaseBuilder : IModelBuilder
    {
        private readonly AnalysisModel _model;
        private readonly IAnalysisModelService _modelService;
        private readonly List<StructuralLoadGroup> _loadGroups;
        private readonly List<StructuralLoadCase> _loadCases;

        public LoadCaseBuilder(AnalysisModel model, IAnalysisModelService modelService)
        {
            _model = model;
            _modelService = modelService;
            _loadGroups = new List<StructuralLoadGroup>();
            _loadCases = new List<StructuralLoadCase>();
        }

        /// <summary>
        /// Adds a load group with specified properties
        /// </summary>
        /// <param name="name">Name of the load group</param>
        /// <param name="groupType">Type of the load group</param>
        /// <param name="loadType">Load type classification</param>
        /// <returns>LoadCaseBuilder for method chaining</returns>
        public LoadCaseBuilder AddLoadGroup(string name, LoadGroupType groupType, Load loadType)
        {
            var groupId = Guid.NewGuid();
            var loadGroup = new StructuralLoadGroup(groupId, name, groupType)
            {
                Load = new FlexibleEnum<Load>(loadType)
            };

            _loadGroups.Add(loadGroup);

            return this;
        }

        /// <summary>
        /// Adds a load case to a specific load group
        /// </summary>
        /// <param name="name">Name of the load case</param>
        /// <param name="loadGroupName">Name of the parent load group</param>
        /// <param name="actionType">Action type of the load case</param>
        /// <param name="caseType">Type of the load case</param>
        /// <param name="duration">Duration of the load</param>
        /// <param name="id">Optional specific ID for the load case</param>
        /// <returns>LoadCaseBuilder for method chaining</returns>
        public LoadCaseBuilder AddLoadCase(string name, string loadGroupName, ActionType actionType, 
            LoadCaseType caseType, Duration duration)
        {
            // Find the load group object
            var loadGroup = _loadGroups.FirstOrDefault(lg => lg.Name == loadGroupName)
                ?? throw new ArgumentException($"Load group '{loadGroupName}' not found. Add the load group first.");

            var loadCase = new StructuralLoadCase(
                Guid.NewGuid(),
                name,
                actionType,
                loadGroup,
                caseType)
            {
                Duration = duration
            };

            _loadCases.Add(loadCase);

            return this;
        }

        /// <summary>
        /// Sets up default load groups and cases for the demo model
        /// </summary>
        /// <returns>LoadCaseBuilder for method chaining</returns>
        public LoadCaseBuilder SetupDefaultLoadCases()
        {
            // Add default load group
            AddLoadGroup("LG1", LoadGroupType.Variable, Load.Domestic);

            // Add load cases to the group
            AddLoadCase("LC1", "LG1", ActionType.Variable, LoadCaseType.Static, Duration.Long);
            AddLoadCase("LC2", "LG1", ActionType.Variable, LoadCaseType.Static, Duration.Long);
            AddLoadCase("LC3", "LG1", ActionType.Variable, LoadCaseType.Static, Duration.Long);

            return this;
        }

        /// <summary>
        /// Gets the GUID of a load case by name
        /// </summary>
        /// <param name="name">Name of the load case</param>
        /// <returns>GUID of the load case</returns>
        public static StructuralLoadCase FindByName(AnalysisModel model, string name)
        {
            foreach (IAnalysisObject item in model)
            {
                if (item is StructuralLoadCase loadCase && loadCase.Name == name)
                {
                    return loadCase;
                }
            }
            throw new ArgumentException($"Load case '{name}' not found.");
        }

        /// <summary>
        /// Builds all load groups and cases and adds them to the model
        /// </summary>
        public void Build()
        {
            if (_loadGroups.Count == 0)
            {
                return; // Nothing to build
            }
            List<IAnalysisObject> loadGroupsAndCases = new List<IAnalysisObject>();
            loadGroupsAndCases.AddRange(_loadGroups);
            loadGroupsAndCases.AddRange(_loadCases);

            var groupResult = _modelService.AddItemsToModel(_model, loadGroupsAndCases);

            foreach (IAnalysisObject loadEntity in loadGroupsAndCases)
            {
                if (!groupResult.TryGetValue(loadEntity.Id, out bool created) || !created)
                {
                    throw new InvalidOperationException($"Error: Load group or case '{loadEntity.Name}' (ID: {loadEntity.Id}) was not successfully created.");
                }
            }
            _model.EnforceModelValidity();
        }
    }
}
