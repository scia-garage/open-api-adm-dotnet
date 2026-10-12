using System;
using System.IO;

namespace OpenApiAdmSciaEngRoundtrip
{
    /// <summary>
    /// Command-line options of the roundtrip tool. <see cref="Parse"/> checks syntax and option combinations and
    /// <see cref="ValidateFiles"/> checks the file system; neither starts SCIA Engineer or writes a file.
    /// </summary>
    internal sealed class CliOptions
    {
        /// <summary>
        /// SCIA Engineer version used for the registry lookup when no <c>--scia-root</c> is given.
        /// </summary>
        public const string DefaultSciaVersion = "2027";

        private const string DefaultOutputSuffix = "_OpenApiRoundtrip.esa";

        private CliOptions()
        {
        }

        public string InputPath { get; private set; }

        public string OutputPath { get; private set; }

        public string SciaVersion { get; private set; } = DefaultSciaVersion;

        public string SciaRoot { get; private set; }

        public bool WithHinge { get; private set; }

        public bool InspectOnly { get; private set; }

        public bool ExpectHinge { get; private set; }

        public bool KillOrphans { get; private set; }

        public bool HelpRequested { get; private set; }

        /// <summary>
        /// Parses the command line. <c>--help</c> returns at once, before the required-option and combination checks.
        /// </summary>
        /// <param name="args">Command-line arguments without the executable name.</param>
        /// <exception cref="ArgumentException">An option is unknown, lacks its value, is missing a required input, or conflicts with another option.</exception>
        public static CliOptions Parse(string[] args)
        {
            var options = new CliOptions();
            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                switch (argument.ToLowerInvariant())
                {
                    case "--help":
                    case "-h":
                        options.HelpRequested = true;
                        break;
                    case "--input":
                        options.InputPath = Path.GetFullPath(ReadValue(args, ref index));
                        break;
                    case "--output":
                        options.OutputPath = Path.GetFullPath(ReadValue(args, ref index));
                        break;
                    case "--scia-version":
                        options.SciaVersion = ReadValue(args, ref index);
                        break;
                    case "--scia-root":
                        options.SciaRoot = Path.GetFullPath(ReadValue(args, ref index));
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
                    case "--kill-orphans":
                        options.KillOrphans = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{argument}'.");
                }
            }

            if (!options.HelpRequested)
            {
                options.CheckCombinations();
            }

            return options;
        }

        /// <summary>
        /// Checks that the input exists, that the output (modify mode) does not exist, and that an explicit SCIA root folder exists.
        /// </summary>
        /// <exception cref="ArgumentException">A check failed; the message names the path.</exception>
        public void ValidateFiles()
        {
            if (!File.Exists(InputPath))
            {
                throw new ArgumentException($"Input ESA not found: {InputPath}");
            }

            if (!InspectOnly && (File.Exists(OutputPath) || Directory.Exists(OutputPath)))
            {
                throw new ArgumentException($"Output already exists and is never overwritten: {OutputPath}");
            }

            if (SciaRoot != null && !Directory.Exists(SciaRoot))
            {
                throw new ArgumentException($"--scia-root folder not found: {SciaRoot}");
            }
        }

        /// <summary>
        /// Writes the usage text, the options and the exit codes.
        /// </summary>
        /// <param name="writer">Destination of the text.</param>
        public static void WriteUsage(TextWriter writer)
        {
            writer.WriteLine("Usage:");
            writer.WriteLine("  OpenApiAdmSciaEngRoundtrip.exe --input <file.esa> [--output <file.esa>] [--with-hinge] [options]");
            writer.WriteLine("  OpenApiAdmSciaEngRoundtrip.exe --inspect-only --input <file.esa> [--expect-hinge] [options]");
            writer.WriteLine();
            writer.WriteLine("Options:");
            writer.WriteLine("  --input <file.esa>         Existing ESA to read. Required; it is never modified.");
            writer.WriteLine("  --output <file.esa>        New ESA to write. Default: <input name>_OpenApiRoundtrip.esa next to the input. Must not exist.");
            writer.WriteLine("  --with-hinge               Also add a point hinge on the beam that uses the existing cross-section.");
            writer.WriteLine("  --inspect-only             Verify the test objects in a hidden copy of --input, close the copy without saving and delete it. No ESA is written.");
            writer.WriteLine("  --expect-hinge             With --inspect-only: require a hinge on one of the test beams.");
            writer.WriteLine($"  --scia-version <version>   SCIA Engineer version for the registry lookup. Default: {DefaultSciaVersion}.");
            writer.WriteLine("  --scia-root <dir>          SCIA Engineer installation folder. Default: registry value.");
            writer.WriteLine("  --kill-orphans             Kill running SciaEngineer and EsaStartupScreen processes, then wait up to 15 s. Destructive; off by default. ThirdPartyConnector is never killed.");
            writer.WriteLine("  --help                     Show this help and exit.");
            writer.WriteLine();
            writer.WriteLine("Exit codes: 0 success, 1 failure, 2 usage or input error, 3 SCIA Engineer already running.");
        }

        private void CheckCombinations()
        {
            if (string.IsNullOrEmpty(InputPath))
            {
                throw new ArgumentException("--input <file.esa> is required.");
            }

            if (!HasEsaExtension(InputPath))
            {
                throw new ArgumentException($"--input must be an .esa file: {InputPath}");
            }

            if (ExpectHinge && !InspectOnly)
            {
                throw new ArgumentException("--expect-hinge is valid only with --inspect-only.");
            }

            if (InspectOnly && WithHinge)
            {
                throw new ArgumentException("--with-hinge modifies a project and cannot be combined with --inspect-only.");
            }

            if (InspectOnly)
            {
                if (OutputPath != null)
                {
                    throw new ArgumentException("--output cannot be used with --inspect-only because inspection never writes an ESA.");
                }

                return;
            }

            OutputPath ??= Path.Combine(
                Path.GetDirectoryName(InputPath),
                Path.GetFileNameWithoutExtension(InputPath) + DefaultOutputSuffix);

            if (!HasEsaExtension(OutputPath))
            {
                throw new ArgumentException($"--output must be an .esa file: {OutputPath}");
            }

            if (string.Equals(OutputPath, InputPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("--output must differ from --input so that the original file is never modified.");
            }
        }

        private static string ReadValue(string[] args, ref int index)
        {
            string option = args[index];
            if (index + 1 >= args.Length ||
                string.IsNullOrWhiteSpace(args[index + 1]) ||
                args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for {option}.");
            }

            index++;
            return args[index];
        }

        private static bool HasEsaExtension(string path)
        {
            return string.Equals(Path.GetExtension(path), ".esa", StringComparison.OrdinalIgnoreCase);
        }
    }
}
