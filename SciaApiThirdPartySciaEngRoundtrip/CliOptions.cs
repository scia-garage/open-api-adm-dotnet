using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using SciaApi.Infrastructure;

namespace SciaApiThirdPartySciaEngRoundtrip
{
    /// <summary>
    /// Command-line options of the ThirdParty roundtrip. The SCIA Engineer folders come from the registry unless given on the command line.
    /// </summary>
    internal sealed class CliOptions
    {
        public const string DllCalatravaFolderName = "dll_calatrava";

        public const string DefaultSciaVersion = "2027";

        public static readonly IReadOnlyList<string> RequiredSdkAssemblyNames = new[]
        {
            "CSInfrastructure",
            "UnitsNet",
            "ModelExchanger.AnalysisDataModel",
            "ModelExchanger.AnalysisDataModel.Contracts",
            "ModelExchanger.Shared.Models",
            "ModelExchanger.AnalysisDataModel.Integration.Bootstrapper",
            "SciaTools.ThirdParty.SDK",
            "SciaTools.ThirdParty.SDK.ADM"
        };

        private const string SciaExecutableFileName = "SciaEngineer.exe";
        private const string ConnectorFileName = "SciaTools.ThirdParty.Business.ThirdPartyConnector.exe";
        private const string OutputSuffix = "_ThirdPartyRoundtrip";

        private CliOptions()
        {
        }

        public string InputPath { get; private set; }

        public string OutputPath { get; private set; }

        public string SciaVersion { get; private set; } = DefaultSciaVersion;

        public string SciaRoot { get; private set; }

        public string SciaTempPath { get; private set; }

        public string DllCalatravaPath { get; private set; }

        public bool WithHinge { get; private set; }

        public bool InspectOnly { get; private set; }

        public bool ExpectHinge { get; private set; }

        public bool ShowHelp { get; private set; }

        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            var valueOptionsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                switch (argument.ToLowerInvariant())
                {
                    case "--help":
                    case "-h":
                        options.ShowHelp = true;
                        break;
                    case "--with-hinge":
                        options.WithHinge = true;
                        break;
                    case "--inspect-only":
                        options.InspectOnly = true;
                        break;
                    case "--expect-hinge":
                        options.ExpectHinge = true;
                        break;
                    case "--input":
                        options.InputPath = ReadValue(args, ref index, argument, valueOptionsSeen);
                        break;
                    case "--output":
                        options.OutputPath = ReadValue(args, ref index, argument, valueOptionsSeen);
                        break;
                    case "--scia-version":
                        options.SciaVersion = ReadValue(args, ref index, argument, valueOptionsSeen);
                        break;
                    case "--scia-root":
                        options.SciaRoot = ReadValue(args, ref index, argument, valueOptionsSeen);
                        break;
                    case "--scia-temp":
                        options.SciaTempPath = ReadValue(args, ref index, argument, valueOptionsSeen);
                        break;
                    default:
                        throw new UsageException("Unknown option or argument: " + argument);
                }
            }

            if (options.ShowHelp)
            {
                return options;
            }

            if (string.IsNullOrWhiteSpace(options.InputPath))
            {
                throw new UsageException("--input <file.esa> is required.");
            }

            if (options.InspectOnly && options.WithHinge)
            {
                throw new UsageException("--with-hinge modifies a project and cannot be combined with --inspect-only.");
            }

            if (options.InspectOnly && options.OutputPath != null)
            {
                throw new UsageException("--output cannot be used with --inspect-only because inspection never writes an ESA.");
            }

            if (options.ExpectHinge && !options.InspectOnly)
            {
                throw new UsageException("--expect-hinge is valid only with --inspect-only.");
            }

            return options;
        }

        public void Resolve()
        {
            InputPath = GetFullPath(InputPath, "--input");
            if (!File.Exists(InputPath))
            {
                throw new UsageException("The input ESA file does not exist: " + InputPath);
            }

            RequireEsaExtension(InputPath, "The input");

            if (!InspectOnly)
            {
                OutputPath = GetFullPath(OutputPath ?? GetDefaultOutputPath(InputPath), "--output");
                RequireEsaExtension(OutputPath, "The output");

                if (string.Equals(InputPath, OutputPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UsageException("The output must differ from the input so that the original ESA is never modified.");
                }

                if (File.Exists(OutputPath) || Directory.Exists(OutputPath))
                {
                    throw new UsageException("The output already exists and is never overwritten: " + OutputPath);
                }
            }

            ResolveSciaFolders();
        }

        public static void WriteUsage(TextWriter writer)
        {
            writer.WriteLine("Usage: SciaApiThirdPartySciaEngRoundtrip.exe --input <file.esa> [options]");
            writer.WriteLine("       SciaApiThirdPartySciaEngRoundtrip.exe --input <file.esa> --inspect-only [--expect-hinge]");
            writer.WriteLine();
            writer.WriteLine("  --input <file.esa>       Required. Existing ESA project; it is copied and never opened by SCIA.");
            writer.WriteLine("  --output <file.esa>      New ESA written by Save As (default: <input name>" + OutputSuffix + ".esa next to the input).");
            writer.WriteLine("  --scia-version <year>    SCIA Engineer registry version (default: " + DefaultSciaVersion + ").");
            writer.WriteLine("  --scia-root <dir>        SCIA Engineer installation folder (default: registry SYSTEM value).");
            writer.WriteLine("  --scia-temp <dir>        SCIA Engineer temp folder that must exist and be writable (default: registry Temp value).");
            writer.WriteLine("  --with-hinge             Also add a point hinge to the existing-section test beam.");
            writer.WriteLine("  --inspect-only           Open a working copy, verify the test objects, then close and shut down without saving. Writes no ESA.");
            writer.WriteLine("  --expect-hinge           With --inspect-only: require a hinge on one of the two test beams.");
            writer.WriteLine("  --help                   Show this help.");
            writer.WriteLine();
            writer.WriteLine("Exit codes: 0 success, 1 failure, 2 usage or input error, 3 SCIA Engineer or the ThirdParty connector is running.");
            writer.WriteLine("No calculation is run. An existing output file is never overwritten.");
        }

        private void ResolveSciaFolders()
        {
            if (SciaRoot != null)
            {
                SciaRoot = GetFullPath(SciaRoot, "--scia-root");
            }

            if (SciaTempPath != null)
            {
                SciaTempPath = GetFullPath(SciaTempPath, "--scia-temp");
            }

            try
            {
                var environment = new SciaEnvironmentManager(SciaVersion, SciaRoot, SciaTempPath);
                SciaRoot = GetFullPath(environment.SciaEngineerFullPath, "the SCIA Engineer folder (registry SYSTEM value; --scia-root overrides it)");
                DllCalatravaPath = GetFullPath(environment.DllCalatravaPath, "the dll_calatrava folder");
                SciaTempPath = GetFullPath(environment.SciaEngineerTempPath, "the SCIA temp folder (registry Temp value; --scia-temp overrides it)");
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is SecurityException || exception is UnauthorizedAccessException)
            {
                throw new UsageException(exception.Message + " Pass --scia-root and --scia-temp to set the SCIA Engineer folders explicitly.");
            }

            if (!Directory.Exists(SciaRoot))
            {
                throw new UsageException("The SCIA Engineer folder does not exist: " + SciaRoot);
            }

            RequireFile(Path.Combine(SciaRoot, SciaExecutableFileName));

            if (!Directory.Exists(DllCalatravaPath))
            {
                throw new UsageException("The " + DllCalatravaFolderName + " folder does not exist: " + DllCalatravaPath);
            }

            foreach (string assemblyName in RequiredSdkAssemblyNames)
            {
                RequireFile(Path.Combine(DllCalatravaPath, assemblyName + ".dll"));
            }

            RequireFile(Path.Combine(DllCalatravaPath, ConnectorFileName));

            if (!Directory.Exists(SciaTempPath))
            {
                throw new UsageException(
                    "The SCIA temp folder does not exist: " + SciaTempPath + ". Pass --scia-temp with the temp folder configured in SCIA Engineer.");
            }
        }

        private static string ReadValue(string[] args, ref int index, string optionName, ISet<string> seenOptions)
        {
            if (!seenOptions.Add(optionName))
            {
                throw new UsageException(optionName + " was given more than once.");
            }

            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new UsageException("A value is required after " + optionName + ".");
            }

            index++;
            return args[index];
        }

        private static string GetFullPath(string path, string description)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new UsageException("No path is set for " + description + ".");
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is IOException)
            {
                throw new UsageException("Invalid path for " + description + ": " + path + " (" + exception.Message + ")");
            }
        }

        private static string GetDefaultOutputPath(string inputPath)
        {
            return Path.Combine(
                Path.GetDirectoryName(inputPath),
                Path.GetFileNameWithoutExtension(inputPath) + OutputSuffix + ".esa");
        }

        private static void RequireEsaExtension(string path, string description)
        {
            if (!string.Equals(Path.GetExtension(path), ".esa", StringComparison.OrdinalIgnoreCase))
            {
                throw new UsageException(description + " must be an .esa file: " + path);
            }
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new UsageException("Required file not found: " + path);
            }
        }
    }

    /// <summary>
    /// Command-line usage or input error. It is reported with the usage text and exit code 2.
    /// </summary>
    internal sealed class UsageException : Exception
    {
        public UsageException(string message)
            : base(message)
        {
        }
    }
}
