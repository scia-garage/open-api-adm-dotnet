using System;
using System.IO;
using SCIA.OpenAPI;
using SCIA.OpenAPI.Utils;

namespace OpenAPIAndADMDemo.Infrastructure
{
    /// <summary>
    /// Manages the lifecycle of a SCIA project - creation, opening, and cleanup.
    /// Implements IDisposable to ensure proper resource cleanup.
    /// </summary>
    public class ProjectManager : IDisposable
    {
        private readonly SCIA.OpenAPI.Environment _environment;
        private readonly string _tempPath;
        private string _projectFilePath;
        private EsaProject _project;
        private bool _disposed = false;

        /// <summary>
        /// Gets the opened SCIA project
        /// </summary>
        public EsaProject Project => _project;

        /// <summary>
        /// Initializes a new instance of the ProjectManager
        /// </summary>
        /// <param name="environment">The SCIA OpenAPI environment</param>
        /// <param name="tempPath">Path where temporary project files should be created</param>
        public ProjectManager(SCIA.OpenAPI.Environment environment, string tempPath = @"C:/TEMP/")
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _tempPath = tempPath ?? throw new ArgumentNullException(nameof(tempPath));
        }

        /// <summary>
        /// Creates an empty project template file and opens the project in SCIA
        /// Note: SCIA Engineer must already be running!
        /// </summary>
        /// <returns>True if the project was successfully created and opened</returns>
        public bool InitializeEmptyProjectTemplate()
        {
            try
            {
                // Create an empty project file
                SciaFileGetter fileGetter = new SciaFileGetter();
                _projectFilePath = fileGetter.PrepareBasicEmptyFile(_tempPath);
                
                if (!File.Exists(_projectFilePath))
                {
                    throw new InvalidOperationException($"File from manifest resource is not created! Temp: {_environment.AppTempPath}");
                }

                // Open the project
                _project = _environment.OpenProject(_projectFilePath);
                if (_project == null)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                // Clean up on failure
                CleanupResources();
                throw;
            }
        }

        /// <summary>
        /// Closes the project and cleans up temporary files
        /// </summary>
        private void CleanupResources()
        {
            try
            {
                // Close the project if it's open
                _project?.CloseProject(SaveMode.SaveChangesNo);
                _project = null;
            }
            catch (Exception ex)
            {
                // Log but don't throw during cleanup
                Console.WriteLine($"Warning: Error closing project: {ex.Message}");
            }

            try
            {
                // Delete the temporary project file
                if (!string.IsNullOrEmpty(_projectFilePath) && File.Exists(_projectFilePath))
                {
                    File.Delete(_projectFilePath);
                    _projectFilePath = null;
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw during cleanup
                Console.WriteLine($"Warning: Error deleting temporary project file: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes of the ProjectManager and cleans up resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern
        /// </summary>
        /// <param name="disposing">True if disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    CleanupResources();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~ProjectManager()
        {
            Dispose(false);
        }
    }
}
