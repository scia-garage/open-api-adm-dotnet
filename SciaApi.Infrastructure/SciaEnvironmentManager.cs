using Microsoft.Win32;
using System;
using System.IO;

namespace SciaApi.Infrastructure
{
    /// <summary>
    /// Manages SCIA Engineer environment setup, paths, and registry access
    /// </summary>
    public class SciaEnvironmentManager
    {
        private readonly string _version;
        private readonly string _sciaRootOverride;
        private readonly string _tempPathOverride;
        private readonly string _appLogPathOverride;

        /// <summary>
        /// Initializes a new instance of the SciaEnvironmentManager
        /// </summary>
        /// <param name="version">SCIA Engineer version, used as the registry key name (default "2027")</param>
        /// <param name="sciaRootOverride">Installation folder used instead of the registry SYSTEM value; null or empty reads the registry</param>
        /// <param name="tempPathOverride">Temp folder used instead of the registry Temp value; null or empty reads the registry</param>
        /// <param name="appLogPathOverride">Folder for application logs; null or empty uses C:\temp\SCIA_OpenAPI_Logs</param>
        public SciaEnvironmentManager(string version = "2027", string sciaRootOverride = null, string tempPathOverride = null, string appLogPathOverride = null)
        {
            _version = version;
            _sciaRootOverride = NormalizeOverride(sciaRootOverride);
            _tempPathOverride = NormalizeOverride(tempPathOverride);
            _appLogPathOverride = NormalizeOverride(appLogPathOverride);
        }

        /// <summary>
        /// Path to SCIA Engineer installation
        /// </summary>
        public string SciaEngineerFullPath => _sciaRootOverride ?? GetAppPath(_version);

        /// <summary>
        /// Path to SCIA Engineer temp directory
        /// </summary>
        public string SciaEngineerTempPath => _tempPathOverride ?? GetTempPath(_version);

        /// <summary>
        /// Path to the OpenAPI_dll folder of the SCIA Engineer installation
        /// </summary>
        public string OpenApiDllPath => Path.Combine(SciaEngineerFullPath, "OpenAPI_dll");

        /// <summary>
        /// Path to the dll_calatrava folder of the SCIA Engineer installation
        /// </summary>
        public string DllCalatravaPath => Path.Combine(SciaEngineerFullPath, "dll_calatrava");

        /// <summary>
        /// Path for application logs
        /// </summary>
        public string AppLogPath => _appLogPathOverride ?? GetThisAppLogPath();

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

        private static string NormalizeOverride(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        }

        /// <summary>
        /// Deletes the WHOLE SCIA Engineer temp folder (recursively) if it exists.
        /// Destructive: must not be called while SCIA Engineer is running.
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
