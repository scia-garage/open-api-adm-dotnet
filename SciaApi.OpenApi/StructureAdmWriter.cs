using System;
using System.Collections.Generic;
using System.Linq;
using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Libraries;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using SCIA.OpenAPI;
using SciaTools.AdmToAdm.AdmSignalR.Models.ModelModification;
using SciaTools.Kernel.ModelExchangerExtension.Models.Exchange;

namespace SciaApi.OpenApi
{
    /// <summary>
    /// Writes ADM objects into the structure of an opened SCIA Engineer project through Structure.CreateAdmObject.
    /// </summary>
    public static class StructureAdmWriter
    {
        /// <summary>
        /// Adds every object of the model, one CreateAdmObject call per object, and reports the objects SCIA Engineer rejects.
        /// </summary>
        /// <param name="structure">The structure of the opened project</param>
        /// <param name="model">The ADM whose objects are added</param>
        public static void AddAll(Structure structure, AnalysisModel model)
        {
            if (structure == null)
            {
                throw new ArgumentNullException(nameof(structure));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            foreach (IAnalysisObject admObject in model)
            {
                ResultOfPartialAddToAnalysisModel result = structure.CreateAdmObject(admObject);
                if (result.PartialAddResult.Status != AdmChangeStatus.Ok)
                {
                    Console.WriteLine($"Error adding object {admObject.Name} to model: {result.PartialAddResult.Errors}");
                }
            }
        }

        /// <summary>
        /// Adds the items with one CreateAdmObject call. Throws unless SCIA Engineer reports success and every item is present in the loaded ADM afterwards.
        /// An empty list is a no-op.
        /// </summary>
        /// <param name="structure">The structure of the opened project</param>
        /// <param name="loadedModel">The ADM loaded from the project, used to verify the added items</param>
        /// <param name="items">The objects of one stage</param>
        /// <param name="operation">Description of the step, used in logs and exception messages</param>
        public static void AddStaged(
            Structure structure,
            AnalysisModel loadedModel,
            IReadOnlyList<IAnalysisObject> items,
            string operation)
        {
            if (structure == null)
            {
                throw new ArgumentNullException(nameof(structure));
            }

            if (loadedModel == null)
            {
                throw new ArgumentNullException(nameof(loadedModel));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (items.Count == 0)
            {
                return;
            }

            IAnalysisObject[] stageItems = items.ToArray();
            Console.WriteLine(
                $"[ADM] {operation}: calling Structure.CreateAdmObject for {string.Join(", ", stageItems.Select(item => $"{item.GetType().Name} '{item.Name}' [{item.Id}]"))}.");

            ResultOfPartialAddToAnalysisModel result = structure.CreateAdmObject(stageItems);
            if (result == null || result.PartialAddResult == null)
            {
                throw new InvalidOperationException(
                    $"Structure.CreateAdmObject returned no partial-add status while {operation}.");
            }

            var partial = result.PartialAddResult;
            string resultDetails =
                $"status={partial.Status}; messageSent={result.IsMessageSendResult}; names={partial.Names ?? "<none>"}; errors={partial.Errors ?? "<none>"}; warnings={partial.Warnings ?? "<none>"}; exception={partial.Exception}";
            Console.WriteLine($"[ADM] {operation}: {resultDetails}");

            if (partial.Status != AdmChangeStatus.Ok ||
                !result.IsMessageSendResult ||
                !string.IsNullOrWhiteSpace(partial.Errors) ||
                partial.Exception != null)
            {
                throw new InvalidOperationException(
                    $"Structure.CreateAdmObject failed while {operation}: {resultDetails}");
            }

            foreach (IAnalysisObject item in stageItems)
            {
                if (!loadedModel.Any(existing => existing.Id == item.Id))
                {
                    throw new InvalidOperationException(
                        $"Structure.CreateAdmObject reported success, but {item.GetType().Name} '{item.Name}' ({item.Id}) is not present in the loaded ADM after {operation}.");
                }
            }

            Console.WriteLine($"[ADM] {operation}: complete.");
        }

        /// <summary>
        /// Adds each non-empty stage in order using AddStaged, one CreateAdmObject call per stage.
        /// </summary>
        /// <param name="structure">The structure of the opened project</param>
        /// <param name="loadedModel">The ADM loaded from the project, used to verify the added items</param>
        /// <param name="stages">The stages in the order they must be sent</param>
        /// <param name="operation">Description of the step; the stage number is appended for each call</param>
        public static void AddStaged(
            Structure structure,
            AnalysisModel loadedModel,
            IEnumerable<IReadOnlyList<IAnalysisObject>> stages,
            string operation)
        {
            if (structure == null)
            {
                throw new ArgumentNullException(nameof(structure));
            }

            if (loadedModel == null)
            {
                throw new ArgumentNullException(nameof(loadedModel));
            }

            if (stages == null)
            {
                throw new ArgumentNullException(nameof(stages));
            }

            List<IReadOnlyList<IAnalysisObject>> nonEmptyStages = stages.Where(stage => stage.Count > 0).ToList();
            for (int index = 0; index < nonEmptyStages.Count; index++)
            {
                AddStaged(
                    structure,
                    loadedModel,
                    nonEmptyStages[index],
                    $"{operation} (stage {index + 1} of {nonEmptyStages.Count})");
            }
        }

        /// <summary>
        /// Groups consecutive items of the same category into stages and keeps the given order.
        /// Categories are cross-sections (StructuralCrossSection), point connections (StructuralPointConnection),
        /// curve members (StructuralCurveMember) and everything else.
        /// </summary>
        /// <param name="orderedItems">The ADM objects in the order they must be added</param>
        /// <returns>The stages, each a consecutive run of one category</returns>
        public static IReadOnlyList<IReadOnlyList<IAnalysisObject>> SplitIntoDependencyStages(IReadOnlyList<IAnalysisObject> orderedItems)
        {
            if (orderedItems == null)
            {
                throw new ArgumentNullException(nameof(orderedItems));
            }

            var stages = new List<IReadOnlyList<IAnalysisObject>>();
            List<IAnalysisObject> currentStage = null;
            AdmStage? currentCategory = null;
            foreach (IAnalysisObject item in orderedItems)
            {
                AdmStage category = GetAdmStage(item);
                if (currentStage == null || category != currentCategory)
                {
                    currentStage = new List<IAnalysisObject>();
                    stages.Add(currentStage);
                    currentCategory = category;
                }

                currentStage.Add(item);
            }

            return stages;
        }

        private static AdmStage GetAdmStage(IAnalysisObject item)
        {
            if (item is StructuralCrossSection)
            {
                return AdmStage.CrossSection;
            }

            if (item is StructuralPointConnection)
            {
                return AdmStage.PointConnection;
            }

            if (item is StructuralCurveMember)
            {
                return AdmStage.CurveMember;
            }

            return AdmStage.Other;
        }

        private enum AdmStage
        {
            CrossSection,
            PointConnection,
            CurveMember,
            Other
        }
    }
}
