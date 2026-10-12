using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using CSInfrastructure.Events;
using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel.StructuralElements;
using SciaApi.AdmModeling.Roundtrip;
using SciaApi.Infrastructure;
using SciaTools.ThirdParty.SDK.ADM;
using SciaTools.ThirdParty.SDK.ADM.Integration;
using SciaTools.ThirdParty.SDK.ADM.Models;
using SciaTools.ThirdParty.SDK.Commands;

namespace SciaApiThirdPartySciaEngRoundtrip
{
    /// <summary>
    /// One ThirdParty SDK session. It launches SCIA Engineer on a working copy, then either sends the roundtrip objects
    /// and saves them to the output ESA, or only verifies them in inspect-only mode. Only this class uses SDK and ADM types.
    /// </summary>
    internal sealed class ThirdPartySession
    {
        private const string ApplicationName = "SciaApi ThirdParty SCIA Engineer roundtrip";
        private const string ObjectNamePrefix = "ThirdPartyRoundtrip";
        private const string RuntimeFolderName = "ThirdPartyRuntime";
        private const string ThirdPartyLinkFolderName = "ThirdPartyLink";
        private const string SessionMarkerFileName = "MarkerFile.txt";
        private const int SessionTimeoutSeconds = 30;
        private const int SavedOutputTimeoutSeconds = 10;

        private readonly CliOptions _options;
        private readonly Guid _applicationIdentifier = Guid.NewGuid();
        private readonly string _communicationDirectory;
        private readonly object _shutdownLock = new object();
        private string _workingCopyPath;
        private ThirdPartySdkAdmBootstrapper _bootstrapper;
        private IThirdPartySdkAnalysisModelService _service;
        private bool _launchAttempted;
        private bool _shutdownConfirmed;
        private bool _cleanupStarted;
        private volatile bool _cancelRequested;

        private ThirdPartySession(CliOptions options)
        {
            _options = options;
            _communicationDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                RuntimeFolderName,
                _applicationIdentifier.ToString("N"));
        }

        private string ThirdPartyLinkFolder => Path.Combine(_options.SciaTempPath, ThirdPartyLinkFolderName);

        public static void Run(CliOptions options)
        {
            var session = new ThirdPartySession(options);
            Console.CancelKeyPress += session.OnCancelKeyPress;
            try
            {
                session.Execute();
            }
            finally
            {
                Console.CancelKeyPress -= session.OnCancelKeyPress;
            }
        }

        private void Execute()
        {
            try
            {
                VerifySdkAssemblies();
                EnsureDirectoryIsWritable(_options.SciaTempPath, "SCIA temp folder");
                EnsureDirectoryIsWritable(AppDomain.CurrentDomain.BaseDirectory, "ThirdParty application folder");
                if (!_options.InspectOnly)
                {
                    EnsureOutputIsAvailable();
                }

                CreateWorkingCopy();
                PrintSetup();
                CreateCommunicationDirectory();
                ThrowIfCancelRequested();
                LaunchSciaEngineer();
                RegisterApplication();
                ThrowIfCancelRequested();
                ProcessModel();
            }
            finally
            {
                FinishSession();
            }
        }

        private void VerifySdkAssemblies()
        {
            AssemblyName[] compiledReferences = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
            foreach (string assemblyName in CliOptions.RequiredSdkAssemblyNames)
            {
                AssemblyName compiled = compiledReferences.FirstOrDefault(
                    reference => string.Equals(reference.Name, assemblyName, StringComparison.OrdinalIgnoreCase));
                if (compiled == null)
                {
                    continue;
                }

                AssemblyName runtime = AssemblyName.GetAssemblyName(FindSdkAssembly(assemblyName));
                if (!HasSameIdentity(compiled, runtime))
                {
                    throw new InvalidOperationException(
                        "The SCIA SDK assembly does not match this build. Built against '" + compiled.FullName +
                        "', found '" + runtime.FullName + "'. Rebuild against the same SCIA Engineer installation that is used at run time.");
                }
            }

            var checkedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pendingNames = new Queue<string>(CliOptions.RequiredSdkAssemblyNames);
            while (pendingNames.Count > 0)
            {
                string assemblyName = pendingNames.Dequeue();
                string path = FindSdkAssembly(assemblyName);
                if (path == null || IsFrameworkAssembly(assemblyName) || !checkedNames.Add(assemblyName))
                {
                    continue;
                }

                foreach (AssemblyName reference in Assembly.ReflectionOnlyLoadFrom(path).GetReferencedAssemblies())
                {
                    pendingNames.Enqueue(reference.Name);
                    string referencePath = FindSdkAssembly(reference.Name);
                    if (referencePath == null || !IsStrongNamed(reference))
                    {
                        continue;
                    }

                    AssemblyName actual = AssemblyName.GetAssemblyName(referencePath);
                    if (!HasSameIdentity(reference, actual))
                    {
                        throw new InvalidOperationException(
                            "The SCIA SDK installation is inconsistent: '" + assemblyName + "' requires '" + reference.FullName +
                            "', but '" + referencePath + "' is '" + actual.FullName +
                            "'. Pass --scia-root to an installation whose dll_calatrava matches the SDK assemblies.");
                    }
                }
            }
        }

        private string FindSdkAssembly(string assemblyName)
        {
            return new[] { _options.SciaRoot, _options.DllCalatravaPath }
                .Select(folder => Path.Combine(folder, assemblyName + ".dll"))
                .FirstOrDefault(File.Exists);
        }

        private void CreateWorkingCopy()
        {
            string anchorPath = _options.OutputPath ?? _options.InputPath;
            _workingCopyPath = Path.Combine(
                Path.GetDirectoryName(anchorPath),
                "." + Path.GetFileNameWithoutExtension(anchorPath) + ".working-" + Guid.NewGuid().ToString("N") + ".esa");
            EsaWorkingCopy.CreateCopy(_options.InputPath, _workingCopyPath);
            File.SetAttributes(_workingCopyPath, File.GetAttributes(_workingCopyPath) | FileAttributes.Hidden);
        }

        private void PrintSetup()
        {
            Console.WriteLine("[INFO] Original ESA, copied and never opened by SCIA: " + _options.InputPath);
            Console.WriteLine("[INFO] Working ESA copy (hidden): " + _workingCopyPath);
            Console.WriteLine(_options.InspectOnly
                ? "[INFO] Mode: inspect-only; no output ESA is written."
                : "[INFO] Output ESA (Save As): " + _options.OutputPath);
            Console.WriteLine("[INFO] SCIA root: " + _options.SciaRoot);
            Console.WriteLine("[INFO] SDK dependencies: " + _options.DllCalatravaPath);
            Console.WriteLine("[INFO] SCIA temp folder: " + _options.SciaTempPath);
            if (!_options.InspectOnly)
            {
                Console.WriteLine("[INFO] Optional point hinge: " + (_options.WithHinge ? "enabled" : "disabled"));
            }

            Console.WriteLine("[INFO] Application identifier: " + _applicationIdentifier);
        }

        private void CreateCommunicationDirectory()
        {
            Directory.CreateDirectory(Path.Combine(_communicationDirectory, "Read"));
            Directory.CreateDirectory(Path.Combine(_communicationDirectory, "Write"));
            Directory.CreateDirectory(Path.Combine(_communicationDirectory, "Temp"));
        }

        private void LaunchSciaEngineer()
        {
            HashSet<string> existingSessions = GetSciaSessionDirectories();
            DateTime launchStartedUtc = DateTime.UtcNow;
            _bootstrapper = new ThirdPartySdkAdmBootstrapper();
            _launchAttempted = true;
            try
            {
                _bootstrapper.LaunchSciaEngineer(
                    _options.SciaTempPath,
                    _options.SciaRoot,
                    QuoteCommandLineArgument(_workingCopyPath));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "SCIA ThirdParty startup failed. Confirm the SCIA temp folder is the one configured in SCIA Engineer and that the ThirdParty connector can start.",
                    exception);
            }

            string sessionDirectory = WaitForFreshSciaSession(existingSessions, launchStartedUtc);
            Console.WriteLine("[STATUS] SCIA launched with the separate working copy.");
            Console.WriteLine("[STATUS] Fresh SCIA ThirdParty session: " + sessionDirectory);
        }

        private void RegisterApplication()
        {
            var registration = new RegisterCommand(
                _applicationIdentifier,
                ApplicationName,
                _communicationDirectory,
                Assembly.GetEntryAssembly().Location);
            var configuration = new ThirdPartySdkAdmConfiguration(_applicationIdentifier);
            try
            {
                _service = _bootstrapper.RegisterApplication(registration, _options.SciaTempPath, configuration);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "ThirdParty application registration failed. Confirm the SCIA ThirdPartyLink session and communication folders were created.",
                    exception);
            }

            if (_service == null)
            {
                throw new InvalidOperationException("ThirdParty SDK registration returned no analysis-model service.");
            }

            _service.OnLogEvent += OnSdkLogEvent;
            Console.WriteLine("[STATUS] ThirdParty SDK application registered.");
        }

        private void ProcessModel()
        {
            AnalysisModel currentModel = _service.GetAnalysisModelFromSciaEngineer();
            if (currentModel == null)
            {
                throw new InvalidOperationException("GetAnalysisModelFromSciaEngineer returned null.");
            }

            Console.WriteLine("[STATUS] Read ADM model; object count: " + currentModel.Count);

            if (_options.InspectOnly)
            {
                InspectModel(currentModel);
            }
            else
            {
                ModifyModel(currentModel);
            }
        }

        private void InspectModel(AnalysisModel model)
        {
            RoundtripVerificationResult verification = RoundtripVerifier.Verify(model, _options.ExpectHinge);
            Console.WriteLine(verification.Summary);
            TryCloseProject();
            ShutdownSciaEngineer();
            Console.WriteLine("[SUCCESS] Inspection finished; the working copy was closed without saving and no output ESA was written.");
            Console.WriteLine("No calculation was run.");
        }

        private void ModifyModel(AnalysisModel currentModel)
        {
            if (currentModel.ProjectInformation == null || currentModel.ModelInformation == null)
            {
                throw new InvalidOperationException(
                    "The read ADM model does not contain both ProjectInformation and ModelInformation; refusing to send a partial model that would cause SDK-generated defaults.");
            }

            RoundtripPayload payload = RoundtripScenarioBuilder.Build(currentModel, _options.WithHinge, ObjectNamePrefix);
            SendPartialModel(payload);
            SaveAsOutput();
            TryCloseProject();
            ShutdownSciaEngineer();
            PrintSummary(payload);
        }

        private void SendPartialModel(RoundtripPayload payload)
        {
            string hingeContext = _options.WithHinge
                ? " The optional point-hinge object was included and may be invalid or unsupported."
                : string.Empty;

            bool sent;
            try
            {
                sent = _service.SendPartialAnalysisModelToSciaEngineer(payload.ScratchModel);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "SendPartialAnalysisModelToSciaEngineer threw an exception." + hingeContext + " " + exception.Message,
                    exception);
            }

            if (!sent)
            {
                throw new InvalidOperationException("SendPartialAnalysisModelToSciaEngineer returned false." + hingeContext);
            }

            Console.WriteLine("[STATUS] Partial ADM send succeeded.");
        }

        private void SaveAsOutput()
        {
            ThrowIfCancelRequested();
            EnsureOutputIsAvailable();
            var saveCommand = new SaveProjectCommand(_applicationIdentifier)
            {
                SaveProject = false,
                SaveProjectAs = _options.OutputPath
            };
            if (!_service.SendCommandToSciaEngineer(saveCommand))
            {
                throw new InvalidOperationException("SCIA did not confirm saving the separate ESA output.");
            }

            WaitForSavedOutput();
            Console.WriteLine("[STATUS] Save As succeeded: " + _options.OutputPath);
        }

        private void TryCloseProject()
        {
            if (_service.SendCommandToSciaEngineer(CreateCloseCommand()))
            {
                Console.WriteLine("[STATUS] Project closed without saving further changes.");
            }
            else
            {
                Console.Error.WriteLine("[WARN] CloseProject was not enabled; continuing to shut down SCIA Engineer without saving.");
            }
        }

        private void ShutdownSciaEngineer()
        {
            lock (_shutdownLock)
            {
                if (_shutdownConfirmed)
                {
                    return;
                }

                if (!_service.SendCommandToSciaEngineer(CreateShutdownCommand()))
                {
                    throw new InvalidOperationException("SCIA did not confirm shutdown.");
                }

                _shutdownConfirmed = true;
                Console.WriteLine("[STATUS] SCIA shut down cleanly.");
            }
        }

        private void PrintSummary(RoundtripPayload payload)
        {
            Console.WriteLine("[ADM] Existing material: " + Describe(payload.ExistingMaterial));
            Console.WriteLine("[ADM] Existing cross-section: " + Describe(payload.ExistingSection));
            Console.WriteLine(payload.SourceBeam != null
                ? "[ADM] Section/material pair taken from curve member: " + Describe(payload.SourceBeam)
                : "[ADM] No curve member used a section/material pair; a standalone cross-section was reused.");
            Console.WriteLine(
                "[ADM] Added test cross-section: " + Describe(payload.TestSection) +
                ", " + payload.TestSection.Parameters[0].Millimeters + " x " + payload.TestSection.Parameters[1].Millimeters +
                " mm, material '" + payload.ExistingMaterial.Name + "'");
            foreach (StructuralPointConnection node in payload.Nodes)
            {
                Console.WriteLine("[ADM] Added node: " + Describe(node));
            }

            Console.WriteLine(
                "[ADM] Added beam using existing section: " + Describe(payload.ExistingSectionBeam) +
                ", section " + Describe(payload.ExistingSection));
            Console.WriteLine(
                "[ADM] Added beam using test section: " + Describe(payload.RectangularSectionBeam) +
                ", section " + Describe(payload.TestSection));
            Console.WriteLine(payload.PointHinge != null
                ? "[ADM] Added point hinge: " + Describe(payload.PointHinge) + ", member " + Describe(payload.ExistingSectionBeam)
                : "[ADM] Point hinge: not requested.");
            Console.WriteLine("[ADM] Partial model object count (references included): " + payload.ScratchModel.Count);
            Console.WriteLine("[SUCCESS] Separate ESA output saved: " + _options.OutputPath);
            Console.WriteLine("No calculation was run.");
        }

        private void FinishSession()
        {
            CleanUpOnce();
            DisposeSdkObjects();

            if (_shutdownConfirmed || !_launchAttempted)
            {
                TryDeleteFile(_workingCopyPath);
                TryDeleteDirectory(_communicationDirectory);
                return;
            }

            Console.Error.WriteLine(
                "[WARN] SCIA shutdown was not confirmed. The working ESA copy and the ThirdParty communication folder were kept: " +
                _workingCopyPath + " ; " + _communicationDirectory);
            if (_service == null)
            {
                Console.Error.WriteLine("[WARN] Registration did not complete, so no shutdown command could be sent.");
            }

            WarnAboutRunningSciaProcesses();
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            _cancelRequested = true;
            Console.Error.WriteLine("[WARN] Cancel requested: the run stops at its next checkpoint; a registered SCIA Engineer is closed without saving and shut down (best effort).");
            CleanUpOnce();
        }

        private void CleanUpOnce()
        {
            lock (_shutdownLock)
            {
                if (_cleanupStarted || _shutdownConfirmed || _service == null)
                {
                    return;
                }

                _cleanupStarted = true;
                _shutdownConfirmed = TryCloseAndShutdown();
            }
        }

        private void ThrowIfCancelRequested()
        {
            if (_cancelRequested)
            {
                throw new OperationCanceledException("The run was cancelled by the user.");
            }
        }

        private static void WarnAboutRunningSciaProcesses()
        {
            try
            {
                IReadOnlyList<string> running = SciaProcessManager.GetRunningSciaProcessDescriptions();
                Console.Error.WriteLine(running.Count == 0
                    ? "[WARN] No SCIA Engineer process is running now."
                    : "[WARN] End exactly these processes yourself; this tool never terminates processes: " + string.Join(", ", running) + ".");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("[WARN] Could not list the running SCIA Engineer processes: " + exception.Message);
            }
        }

        private bool TryCloseAndShutdown()
        {
            try
            {
                Console.WriteLine("[STATUS] Best-effort project close: " + _service.SendCommandToSciaEngineer(CreateCloseCommand()));
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("[WARN] Best-effort project close failed: " + exception.Message);
            }

            try
            {
                bool shutdown = _service.SendCommandToSciaEngineer(CreateShutdownCommand());
                Console.WriteLine("[STATUS] Best-effort SCIA shutdown: " + shutdown);
                return shutdown;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("[WARN] Best-effort SCIA shutdown failed: " + exception.Message);
                return false;
            }
        }

        private void DisposeSdkObjects()
        {
            if (_service != null)
            {
                _service.OnLogEvent -= OnSdkLogEvent;
                try
                {
                    _service.Dispose();
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("[WARN] Disposing the ThirdParty SDK service failed: " + exception.Message);
                }
            }

            if (_bootstrapper != null)
            {
                try
                {
                    _bootstrapper.Dispose();
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine("[WARN] Disposing the ThirdParty SDK bootstrapper failed: " + exception.Message);
                }
            }
        }

        private CloseProjectCommand CreateCloseCommand()
        {
            return new CloseProjectCommand(_applicationIdentifier)
            {
                SaveProject = false,
                SaveProjectAs = null
            };
        }

        private ShutdownCommand CreateShutdownCommand()
        {
            return new ShutdownCommand(_applicationIdentifier)
            {
                SaveProject = false,
                SaveProjectAs = null
            };
        }

        private void EnsureOutputIsAvailable()
        {
            if (File.Exists(_options.OutputPath) || Directory.Exists(_options.OutputPath))
            {
                throw new IOException("The output already exists and is never overwritten: " + _options.OutputPath);
            }
        }

        private void WaitForSavedOutput()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(SavedOutputTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(_options.OutputPath) && new FileInfo(_options.OutputPath).Length > 0)
                {
                    return;
                }

                Thread.Sleep(200);
            }

            throw new IOException("SCIA reported a successful save, but no non-empty ESA output appeared: " + _options.OutputPath);
        }

        private HashSet<string> GetSciaSessionDirectories()
        {
            var sessions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(ThirdPartyLinkFolder))
            {
                foreach (string sessionDirectory in Directory.GetDirectories(ThirdPartyLinkFolder))
                {
                    sessions.Add(Path.GetFullPath(sessionDirectory));
                }
            }

            return sessions;
        }

        private string WaitForFreshSciaSession(ISet<string> previousSessions, DateTime launchStartedUtc)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(SessionTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                ThrowIfCancelRequested();
                if (Directory.Exists(ThirdPartyLinkFolder))
                {
                    DirectoryInfo freshSession = Directory.GetDirectories(ThirdPartyLinkFolder)
                        .Select(path => new DirectoryInfo(path))
                        .Where(session => !previousSessions.Contains(Path.GetFullPath(session.FullName)) &&
                                          session.CreationTimeUtc >= launchStartedUtc.AddSeconds(-2))
                        .OrderByDescending(session => session.CreationTimeUtc)
                        .FirstOrDefault(session => File.Exists(Path.Combine(session.FullName, SessionMarkerFileName)));
                    if (freshSession != null)
                    {
                        return freshSession.FullName;
                    }
                }

                Thread.Sleep(250);
            }

            throw new InvalidOperationException(
                "SCIA did not create a fresh ThirdPartyLink session with " + SessionMarkerFileName + " within " + SessionTimeoutSeconds +
                " s. Confirm the SCIA temp folder is the one configured in SCIA Engineer; registration was not attempted.");
        }

        private static void EnsureDirectoryIsWritable(string path, string description)
        {
            string probe = Path.Combine(path, ".thirdparty-roundtrip-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(probe);
                Directory.Delete(probe);
            }
            catch (Exception exception)
            {
                throw new IOException("Cannot write to the " + description + ": " + path, exception);
            }
        }

        private static string QuoteCommandLineArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static bool IsFrameworkAssembly(string assemblyName)
        {
            return assemblyName == "System" ||
                   assemblyName.StartsWith("System.", StringComparison.Ordinal) ||
                   assemblyName.StartsWith("Microsoft.", StringComparison.Ordinal) ||
                   assemblyName.StartsWith("Presentation", StringComparison.Ordinal) ||
                   assemblyName == "mscorlib" ||
                   assemblyName == "netstandard" ||
                   assemblyName == "WindowsBase" ||
                   assemblyName == "Accessibility";
        }

        private static bool IsStrongNamed(AssemblyName assemblyName)
        {
            return KeyToken(assemblyName).Length > 0;
        }

        private static bool HasSameIdentity(AssemblyName expected, AssemblyName actual)
        {
            return expected.Version == actual.Version && KeyToken(expected) == KeyToken(actual);
        }

        private static string KeyToken(AssemblyName assemblyName)
        {
            return BitConverter.ToString(assemblyName.GetPublicKeyToken() ?? new byte[0]);
        }

        private static string Describe(IAnalysisObject item)
        {
            return item.Name + " [" + item.Id + "]";
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // SCIA may still hold the file for a moment after confirming shutdown.
            const int attempts = 40;
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    return;
                }
                catch (IOException exception)
                {
                    if (attempt == attempts)
                    {
                        Console.Error.WriteLine("[WARN] Could not remove the working ESA copy " + path + ": " + exception.Message);
                        return;
                    }

                    Thread.Sleep(500);
                }
                catch (UnauthorizedAccessException exception)
                {
                    Console.Error.WriteLine("[WARN] Could not remove the working ESA copy " + path + ": " + exception.Message);
                    return;
                }
            }
        }

        private static void TryDeleteDirectory(string path)
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
                Console.Error.WriteLine("[WARN] Could not remove the ThirdParty communication folder " + path + ": " + exception.Message);
            }
        }

        private static void OnSdkLogEvent(object sender, LogEvent logEvent)
        {
            Console.WriteLine(
                "[SDK " + logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") + "] " + logEvent.Source + " " + logEvent.Message);
        }
    }
}
