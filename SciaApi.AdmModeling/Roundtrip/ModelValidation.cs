using System;
using System.Linq;
using ModelExchanger.AnalysisDataModel.Models;

namespace SciaApi.AdmModeling.Roundtrip
{
    /// <summary>
    /// Runs ADM validation and reports failures with the model's error text.
    /// </summary>
    public static class ModelValidation
    {
        /// <summary>
        /// Runs <see cref="AnalysisModel.EnforceModelValidity"/> and <see cref="AnalysisModel.IsModelValid"/>.
        /// </summary>
        /// <param name="model">Model to validate.</param>
        /// <param name="operation">Description of the step that produced the model, used in the error message.</param>
        /// <exception cref="InvalidOperationException">The model is invalid; the message contains its errors.</exception>
        public static void Validate(AnalysisModel model, string operation)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            try
            {
                model.EnforceModelValidity();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"ADM validation failed while {operation}. {FormatErrors(model)}",
                    exception);
            }

            if (!model.IsModelValid())
            {
                throw new InvalidOperationException(
                    $"ADM validation reported an invalid model while {operation}. {FormatErrors(model)}");
            }
        }

        /// <summary>
        /// Formats the model's validation errors, one per line.
        /// </summary>
        public static string FormatErrors(AnalysisModel model)
        {
            if (model.Errors == null || model.Errors.Count == 0)
            {
                return "No ADM validation details were returned.";
            }

            return string.Join(
                System.Environment.NewLine,
                model.Errors.Select(error => error.ToString()));
        }
    }
}
