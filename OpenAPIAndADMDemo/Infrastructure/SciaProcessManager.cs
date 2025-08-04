using System;
using System.Diagnostics;
using System.Threading;

namespace OpenAPIAndADMDemo.Infrastructure
{
    /// <summary>
    /// Manages SCIA Engineer processes - killing orphan runs and process management
    /// </summary>
    public class SciaProcessManager
    {
        /// <summary>
        /// Kills any orphaned SCIA Engineer processes
        /// </summary>
        public static void KillOrphanRuns()
        {
            KillProcessesByName("EsaStartupScreen", 1000);
            KillProcessesByName("SciaEngineer", 5000);
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
