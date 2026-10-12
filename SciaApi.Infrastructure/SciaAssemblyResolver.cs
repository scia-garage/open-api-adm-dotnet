using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SciaApi.Infrastructure
{
    /// <summary>
    /// Handles assembly resolution for SCIA Engineer OpenAPI assemblies
    /// </summary>
    public class SciaAssemblyResolver
    {
        private readonly string[] _searchFolders;
        private bool _isInitialized;

        /// <summary>
        /// Initializes a resolver that probes the SCIA Engineer folder, then its OpenAPI_dll subfolder
        /// </summary>
        /// <param name="sciaEngineerPath">SCIA Engineer installation folder</param>
        public SciaAssemblyResolver(string sciaEngineerPath)
            : this(sciaEngineerPath, new[] { "OpenAPI_dll" })
        {
        }

        /// <summary>
        /// Initializes a resolver that probes the SCIA Engineer folder, then the given subfolders in order
        /// </summary>
        /// <param name="sciaEngineerPath">SCIA Engineer installation folder</param>
        /// <param name="subFolders">Subfolders of the installation folder, e.g. OpenAPI_dll and dll_calatrava</param>
        public SciaAssemblyResolver(string sciaEngineerPath, IEnumerable<string> subFolders)
        {
            string rootPath = sciaEngineerPath ?? throw new ArgumentNullException(nameof(sciaEngineerPath));
            if (subFolders == null)
            {
                throw new ArgumentNullException(nameof(subFolders));
            }

            _searchFolders = new[] { rootPath }
                .Concat(subFolders.Select(subFolder => Path.Combine(rootPath, subFolder)))
                .ToArray();
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
                string simpleName = new AssemblyName(args.Name).Name;
                if (string.IsNullOrEmpty(simpleName) || simpleName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                string dllName = simpleName + ".dll";
                string dllFullPath = _searchFolders
                    .Select(folder => Path.Combine(folder, dllName))
                    .FirstOrDefault(File.Exists);

                if (dllFullPath == null)
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
