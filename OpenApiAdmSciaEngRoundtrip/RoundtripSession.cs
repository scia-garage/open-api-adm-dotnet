using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Libraries;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using SCIA.OpenAPI;
using SciaApi.AdmModeling.Roundtrip;
using SciaApi.Infrastructure;
using SciaApi.OpenApi;

namespace OpenApiAdmSciaEngRoundtrip
{
    /// <summary>
    /// SCIA Engineer lifecycle of one run. It uses SCIA and ADM types, so it must run on an STA thread after the assembly
    /// resolver is installed. Closing with SaveChangesNo discards unsaved changes and does not modify the file.
    /// </summary>
    internal sealed class RoundtripSession
    {
        private const string ApplicationVersion = "1.0.0.0";
        private const string NamePrefix = "OpenApiRoundtrip";
        private const int DeleteAttempts = 40;
        private const int DeleteRetryMilliseconds = 500;

        private readonly CliOptions _options;
        private readonly string _sciaRoot;
        private bool _copyCreated;
        private bool _outputSaved;

        public RoundtripSession(CliOptions options, string sciaRoot)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _sciaRoot = sciaRoot ?? throw new ArgumentNullException(nameof(sciaRoot));
        }

        /// <summary>
        /// Adds the test objects to a new copy of the input and saves the copy, or with --inspect-only verifies a hidden copy of
        /// the input and closes it without saving. The inspection copy, an unsaved output copy and the application temp folder are
        /// removed afterwards.
        /// </summary>
        public void Run()
        {
            string appTempPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "OpenApiAdmSciaEngRoundtrip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(appTempPath);

            try
            {
                if (_options.InspectOnly)
                {
                    InspectInput(appTempPath);
                }
                else
                {
                    ModifyCopy(appTempPath);
                }
            }
            finally
            {
                DeleteUnsavedCopy();
                DeleteApplicationTempDirectory(appTempPath);
            }
        }

        private void ModifyCopy(string appTempPath)
        {
            EsaWorkingCopy.CreateCopy(_options.InputPath, _options.OutputPath);
            _copyCreated = true;
            Console.WriteLine($"Working copy created: {_options.OutputPath}");

            using (SCIA.OpenAPI.Environment environment = StartSciaEngineer(appTempPath))
            using (var projectManager = new ProjectManager(environment))
            {
                AnalysisModel loaded = OpenProjectModel(projectManager, environment, _options.OutputPath);
                ModelValidation.Validate(loaded, "checking the loaded ADM before modification");

                RoundtripPayload payload = RoundtripScenarioBuilder.Build(loaded, _options.WithHinge, NamePrefix);
                AddRoundtripObjects(projectManager.Project.Model, loaded, payload);

                environment.ErrorRepository.Clear();
                Console.WriteLine("[SCIA] RefreshModel_ToSCIAEngineer begin.");
                projectManager.Project.Model.RefreshModel_ToSCIAEngineer();
                LoadedAdmAccessor.EnsureNoOpenApiErrors(environment, "refreshing the updated ADM to SCIA Engineer");
                Console.WriteLine("[SCIA] RefreshModel_ToSCIAEngineer completed.");

                Console.WriteLine("[SCIA] CloseProject(SaveChangesYes) begin.");
                if (!projectManager.CloseProject(SaveMode.SaveChangesYes))
                {
                    throw new InvalidOperationException(
                        $"CloseProject(SaveChangesYes) returned false; the output ESA was not confirmed saved. {LoadedAdmAccessor.GetOpenApiErrorDetails(environment)}");
                }

                _outputSaved = true;
                Console.WriteLine("[SCIA] CloseProject(SaveChangesYes) completed.");
                PrintModifySummary(loaded, payload);
            }
        }

        private void InspectInput(string appTempPath)
        {
            string hashBefore = EsaWorkingCopy.ComputeSha256(_options.InputPath);
            Exception inspectionFailure = null;
            try
            {
                InspectWorkingCopy(appTempPath);
            }
            catch (Exception exception)
            {
                inspectionFailure = exception;
            }

            // Runs after the inspection copy is deleted, also when the inspection failed.
            string hashAfter = EsaWorkingCopy.ComputeSha256(_options.InputPath);
            if (hashAfter != hashBefore)
            {
                throw new InvalidOperationException(
                    $"The input ESA changed on disk during --inspect-only: {_options.InputPath}",
                    inspectionFailure);
            }

            if (inspectionFailure != null)
            {
                ExceptionDispatchInfo.Capture(inspectionFailure).Throw();
            }

            Console.WriteLine("[VERIFY] Input ESA is unchanged (SHA-256 identical). No calculation was run.");
        }

        private void InspectWorkingCopy(string appTempPath)
        {
            string copyPath = InspectionCopyPath();
            try
            {
                EsaWorkingCopy.CreateCopy(_options.InputPath, copyPath);
                File.SetAttributes(copyPath, File.GetAttributes(copyPath) | FileAttributes.Hidden);
                Console.WriteLine($"Hidden inspection copy created: {copyPath}");

                using (SCIA.OpenAPI.Environment environment = StartSciaEngineer(appTempPath))
                using (var projectManager = new ProjectManager(environment))
                {
                    AnalysisModel loaded = OpenProjectModel(projectManager, environment, copyPath);
                    RoundtripVerificationResult verification = RoundtripVerifier.Verify(loaded, _options.ExpectHinge);
                    Console.WriteLine(verification.Summary);

                    Console.WriteLine("[SCIA] CloseProject(SaveChangesNo) begin.");
                    if (!projectManager.CloseProject(SaveMode.SaveChangesNo))
                    {
                        throw new InvalidOperationException(
                            $"CloseProject(SaveChangesNo) returned false after read-only verification. {LoadedAdmAccessor.GetOpenApiErrorDetails(environment)}");
                    }

                    Console.WriteLine("[VERIFY] Closed without saving.");
                }
            }
            finally
            {
                DeleteInspectionCopy(copyPath);
            }
        }

        private string InspectionCopyPath()
        {
            return Path.Combine(
                Path.GetDirectoryName(_options.InputPath),
                $".{Path.GetFileNameWithoutExtension(_options.InputPath)}.inspect-{Guid.NewGuid():N}.esa");
        }

        private SCIA.OpenAPI.Environment StartSciaEngineer(string appTempPath)
        {
            var environment = new SCIA.OpenAPI.Environment(_sciaRoot, appTempPath, ApplicationVersion);
            try
            {
                Console.WriteLine($"[SCIA] Starting SCIA Engineer from {_sciaRoot}.");
                if (!environment.RunSCIAEngineer(SCIA.OpenAPI.Environment.GuiMode.ShowWindowShow))
                {
                    throw new InvalidOperationException(
                        $"RunSCIAEngineer returned false. {LoadedAdmAccessor.GetOpenApiErrorDetails(environment)}");
                }

                LoadedAdmAccessor.EnsureNoOpenApiErrors(environment, "starting SCIA Engineer");
                return environment;
            }
            catch
            {
                environment.Dispose();
                throw;
            }
        }

        private static AnalysisModel OpenProjectModel(ProjectManager projectManager, SCIA.OpenAPI.Environment environment, string esaPath)
        {
            Console.WriteLine($"[SCIA] Opening ESA: {esaPath}");
            if (!projectManager.OpenExistingProject(esaPath))
            {
                throw new InvalidOperationException(
                    $"OpenProject returned null for '{esaPath}'. {LoadedAdmAccessor.GetOpenApiErrorDetails(environment)}");
            }

            Console.WriteLine("[SCIA] RefreshModel_FromSCIAEngineer begin.");
            projectManager.Project.Model.RefreshModel_FromSCIAEngineer();
            LoadedAdmAccessor.EnsureNoOpenApiErrors(environment, "refreshing the open ESA from SCIA Engineer into ADM");

            AnalysisModel model = LoadedAdmAccessor.GetLoadedAnalysisModel(projectManager.Project.Model);
            if (model.Count == 0)
            {
                throw new InvalidOperationException("RefreshModel_FromSCIAEngineer completed, but the loaded ADM is empty.");
            }

            Console.WriteLine($"[ADM] Loaded {model.Count} objects from the open ESA.");
            return model;
        }

        /// <summary>
        /// Adds the test section, nodes and beams in dependency order. The optional hinge is sent on its own so that its rejection
        /// is reported with an explicit message and no refresh or save follows.
        /// </summary>
        private static void AddRoundtripObjects(Structure structure, AnalysisModel loaded, RoundtripPayload payload)
        {
            List<IAnalysisObject> baseObjects = payload.NewObjects
                .Where(item => !ReferenceEquals(item, payload.PointHinge))
                .ToList();
            StructureAdmWriter.AddStaged(
                structure,
                loaded,
                StructureAdmWriter.SplitIntoDependencyStages(baseObjects),
                "adding the roundtrip test section, nodes and beams");
            ModelValidation.Validate(loaded, "checking the ADM after adding the test section, nodes and beams");

            if (payload.PointHinge == null)
            {
                return;
            }

            try
            {
                StructureAdmWriter.AddStaged(
                    structure,
                    loaded,
                    new IAnalysisObject[] { payload.PointHinge },
                    "adding the sample point hinge");
                ModelValidation.Validate(loaded, "checking the ADM after adding the sample point hinge");
            }
            catch (Exception exception)
            {
                string details = loaded.Errors is { Count: > 0 } ? ModelValidation.FormatErrors(loaded) : exception.Message;
                throw new InvalidOperationException(
                    $"--with-hinge was requested, but ADM validation/API rejected the sample point-release representation RelConnectsStructuralMember on beam '{payload.ExistingSectionBeam.Name}' ({payload.ExistingSectionBeam.Id}). No alternative hinge mapping was attempted; RefreshModel_ToSCIAEngineer and ESA save were skipped. {details}",
                    exception);
            }
        }

        private void PrintModifySummary(AnalysisModel model, RoundtripPayload payload)
        {
            var testSection = payload.TestSection;
            Console.WriteLine($"Existing material: {payload.ExistingMaterial.Name} [{payload.ExistingMaterial.Id}]");
            Console.WriteLine($"Existing cross-section: {payload.ExistingSection.Name} [{payload.ExistingSection.Id}]");
            if (payload.SourceBeam != null)
            {
                Console.WriteLine($"Existing cross-section source beam: {payload.SourceBeam.Name} [{payload.SourceBeam.Id}]");
            }

            Console.WriteLine(
                $"Added test cross-section: {testSection.Name} [{testSection.Id}], rectangle {testSection.Parameters[0].Millimeters:0.###} mm high x {testSection.Parameters[1].Millimeters:0.###} mm wide, material '{payload.ExistingMaterial.Name}' [{payload.ExistingMaterial.Id}]");

            foreach (var node in payload.Nodes)
            {
                Console.WriteLine($"Added node: {node.Name} [{node.Id}]");
            }

            Console.WriteLine(
                $"Added beam using existing section: {payload.ExistingSectionBeam.Name} [{payload.ExistingSectionBeam.Id}], section '{payload.ExistingSection.Name}' [{payload.ExistingSection.Id}]");
            Console.WriteLine(
                $"Added beam using test section: {payload.RectangularSectionBeam.Name} [{payload.RectangularSectionBeam.Id}], section '{testSection.Name}' [{testSection.Id}]");

            if (payload.PointHinge == null)
            {
                Console.WriteLine("Point hinge: not requested.");
            }
            else
            {
                Console.WriteLine(
                    $"Added point hinge: {payload.PointHinge.Name} [{payload.PointHinge.Id}], member '{payload.ExistingSectionBeam.Name}' [{payload.ExistingSectionBeam.Id}]");
            }

            Console.WriteLine(
                $"ADM totals: {model.Count} objects, {CountOf<StructuralMaterial>(model)} materials, {CountOf<StructuralCrossSection>(model)} cross-sections, {CountOf<StructuralPointConnection>(model)} nodes, {CountOf<StructuralCurveMember>(model)} beams, {CountOf<RelConnectsStructuralMember>(model)} point hinges");
            Console.WriteLine($"Output ESA saved: {_options.OutputPath}");
            Console.WriteLine("No calculation was run.");
        }

        private static int CountOf<T>(AnalysisModel model)
        {
            return model.OfType<T>().Count();
        }

        private void DeleteUnsavedCopy()
        {
            if (!_copyCreated || _outputSaved || !File.Exists(_options.OutputPath))
            {
                return;
            }

            if (TryDeleteFile(_options.OutputPath, out string error))
            {
                Console.Error.WriteLine($"Removed the unsaved working copy: {_options.OutputPath}");
                return;
            }

            Console.Error.WriteLine(
                $"Warning: could not remove the unsaved working copy '{_options.OutputPath}' ({error}). Delete it before running again, because --output must not exist.");
        }

        private static void DeleteInspectionCopy(string copyPath)
        {
            if (!TryDeleteFile(copyPath, out string error))
            {
                Console.Error.WriteLine($"Warning: could not remove the hidden inspection copy '{copyPath}' ({error}). Delete it manually.");
            }
        }

        private static bool TryDeleteFile(string path, out string error)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    error = null;
                    return true;
                }
                catch (IOException) when (attempt < DeleteAttempts)
                {
                    // SCIA Engineer may still hold the file for a moment after the project is closed.
                    Thread.Sleep(DeleteRetryMilliseconds);
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    return false;
                }
            }
        }

        private static void DeleteApplicationTempDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Warning: could not remove the OpenAPI working directory '{path}': {exception.Message}");
            }
        }
    }
}
