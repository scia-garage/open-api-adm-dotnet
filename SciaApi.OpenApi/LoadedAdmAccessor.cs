using System;
using System.Reflection;
using ModelExchanger.AnalysisDataModel.Models;
using SCIA.OpenAPI;
using SciaTools.Kernel.ModelExchangerExtension.Contracts.AnalysisModelModifications;

namespace SciaApi.OpenApi
{
    /// <summary>
    /// Reads the ADM that SCIA Engineer loads for an opened project and reports SCIA OpenAPI errors.
    /// </summary>
    public static class LoadedAdmAccessor
    {
        /// <summary>
        /// Gets the ADM that SCIA Engineer loaded for the structure of an opened project.
        /// </summary>
        /// <remarks>
        /// SCIA.OpenAPI (verified for 27.0.42.64) exposes no public accessor for the populated AnalysisModel, hence the reflection:
        /// the non-public Structure.ServiceWrapper field, its public Client property, and the IHasAnalysisModel contract.
        /// Replace it when a public accessor appears.
        /// </remarks>
        /// <param name="structure">The structure of the opened project</param>
        /// <returns>The ADM loaded by SCIA Engineer</returns>
        public static AnalysisModel GetLoadedAnalysisModel(Structure structure)
        {
            if (structure == null)
            {
                throw new ArgumentNullException(nameof(structure));
            }

            FieldInfo serviceWrapperField = typeof(Structure).GetField(
                "ServiceWrapper",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (serviceWrapperField == null)
            {
                throw new InvalidOperationException(
                    "The installed SCIA.OpenAPI Structure API has no expected ServiceWrapper field; it does not expose a public populated AnalysisModel accessor.");
            }

            object serviceWrapper = serviceWrapperField.GetValue(structure);
            if (serviceWrapper == null)
            {
                throw new InvalidOperationException("SCIA.OpenAPI Structure.ServiceWrapper is null after the project was opened.");
            }

            PropertyInfo clientProperty = serviceWrapper.GetType().GetProperty(
                "Client",
                BindingFlags.Instance | BindingFlags.Public);
            object client = clientProperty == null ? null : clientProperty.GetValue(serviceWrapper, null);
            if (client == null)
            {
                throw new InvalidOperationException(
                    "The SCIA.OpenAPI ADM service wrapper has no usable public Client property.");
            }

            var modelProvider = client as IHasAnalysisModel;
            if (modelProvider == null)
            {
                throw new InvalidOperationException(
                    $"The SCIA.OpenAPI ADM client '{client.GetType().FullName}' does not implement the referenced IHasAnalysisModel contract.");
            }

            AnalysisModel model = modelProvider.AnalysisModel;
            if (model == null)
            {
                throw new InvalidOperationException(
                    "The SCIA.OpenAPI ADM client's IHasAnalysisModel.AnalysisModel property returned null after RefreshModel_FromSCIAEngineer.");
            }

            return model;
        }

        /// <summary>
        /// Throws when SCIA OpenAPI recorded an error for the environment.
        /// </summary>
        /// <param name="env">The SCIA OpenAPI environment</param>
        /// <param name="operation">Description of the step that was running, used in the exception message</param>
        public static void EnsureNoOpenApiErrors(SCIA.OpenAPI.Environment env, string operation)
        {
            if (env == null)
            {
                throw new ArgumentNullException(nameof(env));
            }

            if (env.ErrorRepository.IsError())
            {
                throw new InvalidOperationException(
                    $"SCIA OpenAPI reported an error while {operation}: {GetOpenApiErrorDetails(env)}");
            }
        }

        /// <summary>
        /// Gets the errors that SCIA OpenAPI recorded for the environment.
        /// </summary>
        /// <param name="env">The SCIA OpenAPI environment</param>
        /// <returns>The recorded error text, or a placeholder when none was recorded</returns>
        public static string GetOpenApiErrorDetails(SCIA.OpenAPI.Environment env)
        {
            if (env == null)
            {
                throw new ArgumentNullException(nameof(env));
            }

            string details = env.ErrorRepository.GetErrorsInfo();
            return string.IsNullOrWhiteSpace(details)
                ? "No additional SCIA OpenAPI error details were recorded."
                : details;
        }
    }
}
