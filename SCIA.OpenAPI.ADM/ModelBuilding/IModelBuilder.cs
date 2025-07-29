namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Base interface for all model builders
    /// </summary>
    public interface IModelBuilder
    {
        /// <summary>
        /// Builds the specific model components and adds them to the AnalysisModel
        /// </summary>
        void Build();
    }
}
