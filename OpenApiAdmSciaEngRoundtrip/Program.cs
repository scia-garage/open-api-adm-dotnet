using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using SciaApi.Infrastructure;

namespace OpenApiAdmSciaEngRoundtrip
{
    /// <summary>
    /// Entry point: checks the options and files, guards against running SCIA Engineer processes, installs the assembly
    /// resolver and runs <see cref="RoundtripSession"/> on an STA thread. <c>Main</c> refers to no SCIA or ADM type, so
    /// those assemblies load only after the resolver is installed.
    /// </summary>
    internal static class Program
    {
        private const int ExitSuccess = 0;
        private const int ExitFailure = 1;
        private const int ExitUsageError = 2;
        private const int ExitSciaRunning = 3;
        private const string ThirdPartyConnectorName = "SciaTools.ThirdParty.Business.ThirdPartyConnector";
        private const int SciaExitPollMilliseconds = 250;
        private static readonly TimeSpan SciaExitTimeout = TimeSpan.FromSeconds(15);

        private static int Main(string[] args)
        {
            CliOptions options;
            try
            {
                options = CliOptions.Parse(args);
                if (options.HelpRequested)
                {
                    CliOptions.WriteUsage(Console.Out);
                    return ExitSuccess;
                }

                options.ValidateFiles();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Error: {exception.Message}");
                Console.Error.WriteLine();
                CliOptions.WriteUsage(Console.Error);
                return ExitUsageError;
            }

            string sciaRoot;
            try
            {
                sciaRoot = ResolveSciaRoot(options);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Error: {exception.Message}");
                return ExitFailure;
            }

            if (options.KillOrphans)
            {
                SciaProcessManager.KillOrphanRuns();
                WaitForSciaProcessesToExit();
            }

            if (!EnsureSciaNotRunning())
            {
                return ExitSciaRunning;
            }

            SciaAssemblyResolver assemblyResolver = null;
            try
            {
                assemblyResolver = new SciaAssemblyResolver(sciaRoot);
                assemblyResolver.Initialize();
                return RunSession(options, sciaRoot);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Error: {exception.Message}");
                return ExitFailure;
            }
            finally
            {
                assemblyResolver?.Cleanup();
            }
        }

        private static string ResolveSciaRoot(CliOptions options)
        {
            var environmentManager = new SciaEnvironmentManager(options.SciaVersion, options.SciaRoot);
            string openApiAssembly = Path.Combine(environmentManager.OpenApiDllPath, "SCIA.OpenAPI.dll");
            if (!File.Exists(openApiAssembly))
            {
                throw new FileNotFoundException(
                    "The SCIA Engineer OpenAPI assembly was not found. Check --scia-root or the SCIA Engineer installation.",
                    openApiAssembly);
            }

            return environmentManager.SciaEngineerFullPath;
        }

        private static void WaitForSciaProcessesToExit()
        {
            var stopwatch = Stopwatch.StartNew();
            while (SciaProcessManager.GetRunningSciaProcessDescriptions().Count > 0 && stopwatch.Elapsed < SciaExitTimeout)
            {
                Thread.Sleep(SciaExitPollMilliseconds);
            }
        }

        private static bool EnsureSciaNotRunning()
        {
            try
            {
                SciaProcessManager.EnsureNoSciaProcessRunning();
                return true;
            }
            catch (InvalidOperationException exception)
            {
                Console.Error.WriteLine($"Error: {exception.Message}");
                if (SciaProcessManager.GetRunningSciaProcessDescriptions().Any(IsThirdPartyConnector))
                {
                    Console.Error.WriteLine($"{ThirdPartyConnectorName} is not killed by this tool. End it manually, then run again.");
                }

                return false;
            }
        }

        private static bool IsThirdPartyConnector(string processDescription)
        {
            return processDescription.StartsWith(ThirdPartyConnectorName, StringComparison.Ordinal);
        }

        private static int RunSession(CliOptions options, string sciaRoot)
        {
            var session = new RoundtripSession(options, sciaRoot);
            Exception failure = null;
            var sciaThread = new Thread(() =>
            {
                try
                {
                    session.Run();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });

            sciaThread.SetApartmentState(ApartmentState.STA);
            sciaThread.Start();
            sciaThread.Join();

            if (failure == null)
            {
                return ExitSuccess;
            }

            Console.Error.WriteLine($"OpenApiAdmSciaEngRoundtrip failed: {failure}");
            return ExitFailure;
        }
    }
}
