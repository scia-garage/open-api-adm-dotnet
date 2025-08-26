using Microsoft.Win32;
using System;
using System.IO;

namespace OpenAPIAndADMDemo.Infrastructure
{
    /// <summary>
    /// Manages SCIA Engineer environment setup, paths, and registry access
    /// </summary>
    public class SciaEnvironmentManager
    {
        private readonly string _version;

        public SciaEnvironmentManager(string version)
        {
            _version = version;
        }

        /// <summary>
        /// Path to SCIA Engineer installation
        /// </summary>
        public string SciaEngineerFullPath => GetAppPath(_version);

        /// <summary>
        /// Path to SCIA Engineer temp directory
        /// </summary>
        public string SciaEngineerTempPath => GetTempPath(_version);

        /// <summary>
        /// Path for application logs
        /// </summary>
        public string AppLogPath => GetThisAppLogPath();

        /// <summary>
        /// Gets the SCIA Engineer installation path from registry
        /// </summary>
        /// <param name="version">Version of SCIA Engineer</param>
        /// <returns>Installation path</returns>
        private string GetAppPath(string version)
        {
            string registryPath = $@"Software\SCIA\ESA\{version}\Admin\Dir";

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key != null)
                {
                    object systemValue = key.GetValue("SYSTEM");
                    if (systemValue != null)
                    {
                        return systemValue.ToString();
                    }
                }
            }
            throw new InvalidOperationException($"Registry key {registryPath} not found.");
        }

        /// <summary>
        /// Gets the SCIA Engineer temp path from registry
        /// </summary>
        /// <param name="version">Version of SCIA Engineer</param>
        /// <returns>Temp path</returns>
        private string GetTempPath(string version)
        {
            string registryPath = $@"Software\SCIA\ESA\{version}\Admin\Dir";

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
            {
                if (key != null)
                {
                    object tempValue = key.GetValue("Temp");
                    if (tempValue != null)
                    {
                        return tempValue.ToString();
                    }
                }
            }
            throw new InvalidOperationException($"Registry key {registryPath} not found.");
        }

        /// <summary>
        /// Gets the path for this application's log files
        /// </summary>
        /// <returns>Log path</returns>
        private string GetThisAppLogPath()
        {
            return @"C:\temp\SCIA_OpenAPI_Logs"; // Folder for storing of log files for this console application
        }

        /// <summary>
        /// Deletes the SCIA Engineer temp directory if it exists
        /// </summary>
        public void DeleteTemp()
        {
            if (Directory.Exists(SciaEngineerTempPath))
            {
                Directory.Delete(SciaEngineerTempPath, true);
            }
        }
    }
}
