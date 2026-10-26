using System;
using System.Collections.Generic;
using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Models;

namespace SciaApi.AdmModeling.Roundtrip
{
    /// <summary>
    /// Lookup helpers shared by the roundtrip catalog and verifier.
    /// </summary>
    internal static class ModelLookup
    {
        public static Dictionary<Guid, T> ById<T>(AnalysisModel model)
            where T : class, IAnalysisObject
        {
            var result = new Dictionary<Guid, T>();
            foreach (IAnalysisObject item in model)
            {
                if (item is T typed)
                {
                    result[typed.Id] = typed;
                }
            }

            return result;
        }

        public static string NameOf(IAnalysisObject item)
        {
            return item.Name ?? string.Empty;
        }
    }
}
