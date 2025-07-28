using System;

namespace OpenAPIAndADMDemo.Configuration
{
    /// <summary>
    /// Contains constants and identifiers used throughout the model
    /// </summary>
    public static class ModelConstants
    {
        // Load Case and Combination IDs
        public static readonly Guid LC1Id = Guid.NewGuid();
        public static readonly Guid C1Id = Guid.NewGuid();

        // Element Names
        public const string N1Name = "N1";
        public const string B1Name = "B1";
        public const string S1Name = "S1";

        // SCIA Engineer Version
        public const string SciaVersion = "25.0";

        // Application Settings
        public const string ApplicationVersion = "1.0.0.0";
    }
}
