using System;
using System.IO;
using SCIA.OpenAPI;
using SCIA.OpenAPI.Utils;

namespace SciaApi.OpenApi
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
        private bool _ownsProjectFile;
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
            PrepareForNewProject();

            try
            {
                // Create an empty project file
                SciaFileGetter fileGetter = new SciaFileGetter();
                _projectFilePath = fileGetter.PrepareBasicEmptyFile(_tempPath);
                _ownsProjectFile = true;
                
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
        /// Opens an existing SCIA project file in place. The file is neither copied nor deleted by this instance.
        /// Note: SCIA Engineer must already be running!
        /// </summary>
        /// <param name="esaPath">Path to an existing .esa project file</param>
        /// <returns>True if the project was successfully opened</returns>
        public bool OpenExistingProject(string esaPath)
        {
            if (esaPath == null)
            {
                throw new ArgumentNullException(nameof(esaPath));
            }

            string fullPath = Path.GetFullPath(esaPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("The SCIA project file does not exist.", fullPath);
            }

            PrepareForNewProject();

            _project = _environment.OpenProject(fullPath);
            if (_project == null)
            {
                return false;
            }

            _projectFilePath = fullPath;
            _ownsProjectFile = false;
            return true;
        }

        /// <summary>
        /// Closes the open project with the given save mode. The project is kept if SCIA Engineer reports the close as failed.
        /// </summary>
        /// <param name="mode">How SCIA Engineer should handle unsaved changes</param>
        /// <returns>True if the project was closed; false if no project is open or the close failed</returns>
        public bool CloseProject(SaveMode mode)
        {
            if (_project == null)
            {
                return false;
            }

            bool closed = _project.CloseProject(mode);
            if (closed)
            {
                _project = null;
            }

            return closed;
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

            DeleteOwnedProjectFile();
        }

        private void DeleteOwnedProjectFile()
        {
            if (!_ownsProjectFile)
            {
                return;
            }

            try
            {
                // Delete the temporary project file
                if (!string.IsNullOrEmpty(_projectFilePath) && File.Exists(_projectFilePath))
                {
                    File.Delete(_projectFilePath);
                }

                _projectFilePath = null;
                _ownsProjectFile = false;
            }
            catch (Exception ex)
            {
                // Log but don't throw during cleanup
                Console.WriteLine($"Warning: Error deleting temporary project file: {ex.Message}");
            }
        }

        private void PrepareForNewProject()
        {
            if (_project != null)
            {
                throw new InvalidOperationException("A project is already open. Close it before opening another one.");
            }

            DeleteOwnedProjectFile();
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
