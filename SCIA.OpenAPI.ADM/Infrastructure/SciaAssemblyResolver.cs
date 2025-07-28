using System;
using System.IO;
using System.Reflection;

namespace OpenAPIAndADMDemo.Infrastructure
{
    /// <summary>
    /// Handles assembly resolution for SCIA Engineer OpenAPI assemblies
    /// </summary>
    public class SciaAssemblyResolver
    {
        private readonly string _sciaEngineerPath;
        private bool _isInitialized;

        public SciaAssemblyResolver(string sciaEngineerPath)
        {
            _sciaEngineerPath = sciaEngineerPath ?? throw new ArgumentNullException(nameof(sciaEngineerPath));
        }

        /// <summary>
        /// Initializes the assembly resolver. This method should be called before using any SCIA OpenAPI functionality.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            _isInitialized = true;
        }

        /// <summary>
        /// Cleans up the assembly resolver
        /// </summary>
        public void Cleanup()
        {
            if (_isInitialized)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Assembly resolve event handler
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="args">Assembly resolve event arguments</param>
        /// <returns>Resolved assembly or null if not found</returns>
        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string dllName = args.Name.Substring(0, args.Name.IndexOf(",")) + ".dll";
                string dllFullPath = Path.Combine(_sciaEngineerPath, dllName);
                
                if (!File.Exists(dllFullPath))
                {
                    dllFullPath = Path.Combine(_sciaEngineerPath, "OpenAPI_dll", dllName);
                }
                
                if (!File.Exists(dllFullPath))
                {
                    return null;
                }
                
                return Assembly.LoadFrom(dllFullPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to resolve assembly {args.Name}: {ex.Message}");
                return null;
            }
        }
    }
}
