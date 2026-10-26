using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SciaApi.Infrastructure
{
    /// <summary>
    /// Manages SCIA Engineer processes - killing orphan runs and process management
    /// </summary>
    public class SciaProcessManager
    {
        /// <summary>
        /// Names of the processes that make up a running SCIA Engineer session
        /// </summary>
        public static IReadOnlyList<string> SciaProcessNames { get; } = new[]
        {
            "SciaEngineer",
            "EsaStartupScreen",
            "SciaTools.ThirdParty.Business.ThirdPartyConnector"
        };

        /// <summary>
        /// WARNING: kills ALL running SciaEngineer and EsaStartupScreen processes on this machine, not only orphaned ones
        /// </summary>
        public static void KillOrphanRuns()
        {
            KillProcessesByName("EsaStartupScreen", 1000);
            KillProcessesByName("SciaEngineer", 5000);
        }

        /// <summary>
        /// Gets a "name (PID n)" description of every running process whose name is in SciaProcessNames
        /// </summary>
        /// <returns>Descriptions of the running SCIA processes; empty if none is running</returns>
        public static IReadOnlyList<string> GetRunningSciaProcessDescriptions()
        {
            var descriptions = new List<string>();
            foreach (string processName in SciaProcessNames)
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    using (process)
                    {
                        descriptions.Add($"{processName} (PID {process.Id})");
                    }
                }
            }
            return descriptions;
        }

        /// <summary>
        /// Throws if any SCIA Engineer process is running. Never terminates a process.
        /// </summary>
        /// <exception cref="InvalidOperationException">SCIA Engineer is still running</exception>
        public static void EnsureNoSciaProcessRunning()
        {
            IReadOnlyList<string> running = GetRunningSciaProcessDescriptions();
            if (running.Count > 0)
            {
                throw new InvalidOperationException(
                    $"SCIA Engineer is still running. Close SCIA Engineer first, then try again. Running processes: {string.Join(", ", running)}.");
            }
        }

        /// <summary>
        /// Kills all processes with the specified name
        /// </summary>
        /// <param name="processName">Name of the process to kill</param>
        /// <param name="delayMs">Delay in milliseconds after killing processes</param>
        private static void KillProcessesByName(string processName,int delayMs)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    process.Kill();
                    Console.WriteLine($"Killed process {processName}, which was running in the background.");
                    Thread.Sleep(delayMs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to kill process {processName}: {ex.Message}");
                }
            }
        }
    }
}
