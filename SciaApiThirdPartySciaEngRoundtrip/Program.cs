using System;
using SciaApi.Infrastructure;

namespace SciaApiThirdPartySciaEngRoundtrip
{
    /// <summary>
    /// Entry point of the ThirdParty SDK roundtrip. Exit codes: 0 success, 1 failure, 2 usage or input error,
    /// 3 SCIA Engineer or its ThirdParty connector is already running.
    /// </summary>
    internal static class Program
    {
        private const int ExitSucceeded = 0;
        private const int ExitFailed = 1;
        private const int ExitUsage = 2;
        private const int ExitSciaRunning = 3;

        private static int Main(string[] args)
        {
            CliOptions options;
            try
            {
                options = CliOptions.Parse(args);
                if (options.ShowHelp)
                {
                    CliOptions.WriteUsage(Console.Out);
                    return ExitSucceeded;
                }

                options.Resolve();
            }
            catch (UsageException exception)
            {
                Console.Error.WriteLine("[USAGE] " + exception.Message);
                CliOptions.WriteUsage(Console.Error);
                return ExitUsage;
            }

            try
            {
                SciaProcessManager.EnsureNoSciaProcessRunning();
            }
            catch (InvalidOperationException exception)
            {
                Console.Error.WriteLine("[SCIA] " + exception.Message);
                return ExitSciaRunning;
            }

            var assemblyResolver = new SciaAssemblyResolver(options.SciaRoot, new[] { CliOptions.DllCalatravaFolderName });
            assemblyResolver.Initialize();
            try
            {
                ThirdPartySession.Run(options);
                return ExitSucceeded;
            }
            catch (OperationCanceledException exception)
            {
                Console.Error.WriteLine("[CANCELLED] " + exception.Message);
                return ExitFailed;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("[FAILED] " + exception);
                return ExitFailed;
            }
            finally
            {
                assemblyResolver.Cleanup();
            }
        }
    }
}
