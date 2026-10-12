# SciaApiThirdPartySciaEngRoundtrip

Standalone .NET Framework 4.8 console project that runs an ADM roundtrip test on an existing SCIA Engineer 2027 project through the ThirdParty SDK. It reads the project, adds two test beams and an optional point hinge, saves the result as a new ESA, and shuts SCIA Engineer down. No calculation is run.

## Relation to the other roundtrip projects

- `OpenApiAdmSciaEngRoundtrip` (OpenAPI route) and this project share the scenario, verifier and infrastructure from `SciaApi.AdmModeling` and `SciaApi.Infrastructure`. Only the transport differs.
- This project uses the ThirdParty SDK (`SciaTools.ThirdParty.SDK`, `SciaTools.ThirdParty.SDK.ADM`, from `dll_calatrava`). It does not reference `SciaApi.OpenApi` or `SCIA.OpenAPI`.
- Rules taken from the Third Party SDK ADM help and followed here:
  - The first SDK command after registration is `GetAnalysisModelFromSciaEngineer`. The model is always read before anything is sent.
  - The change is sent as a partial ADM model (`SendPartialAnalysisModelToSciaEngineer`). The partial model includes the read `ProjectInformation` and `ModelInformation`.
  - The help names SAF/xlsx as the exchange format. This tool does not write SAF or xlsx files; the ESA is written by SCIA's own Save As.

## Prerequisites

- SCIA Engineer 2027 (new UI) with `SciaEngineer.exe`. The SCIA installation folder and temp folder are read from `HKCU\Software\SCIA\ESA\2027\Admin\Dir` (`SYSTEM` and `Temp`) unless overridden.
- The `dll_calatrava` folder of that installation with the SDK assemblies and `SciaTools.ThirdParty.Business.ThirdPartyConnector.exe`.
- .NET Framework 4.8.
- An input ESA project supplied by you. No ESA file is committed to this repository.

## Build

From the repository root `C:\Data\GIT\API\open-api-adm-dotnet`:

```powershell
dotnet build SciaApiThirdPartySciaEngRoundtrip\SciaApiThirdPartySciaEngRoundtrip.csproj -c Release
```

The build references the SDK assemblies from `$(SciaDllCalatravaDir)` (`Directory.Build.props`, default `C:\Program Files\SCIA\Engineer2027\dll_calatrava\`; override with `-p:SciaEngineerDir=...`). It fails if a required file is missing.

## Run

```powershell
.\SciaApiThirdPartySciaEngRoundtrip\bin\Release\net48\SciaApiThirdPartySciaEngRoundtrip.exe --input C:\Projects\Model.esa
.\SciaApiThirdPartySciaEngRoundtrip\bin\Release\net48\SciaApiThirdPartySciaEngRoundtrip.exe --input C:\Projects\Model.esa --output C:\Projects\Model_hinge.esa --with-hinge
.\SciaApiThirdPartySciaEngRoundtrip\bin\Release\net48\SciaApiThirdPartySciaEngRoundtrip.exe --input C:\Projects\Model_hinge.esa --inspect-only --expect-hinge
```

Close SCIA Engineer before running. The first run writes `Model_ThirdPartyRoundtrip.esa` next to the input.

## Options

| Option | Meaning |
| --- | --- |
| `--input <file.esa>` | Required. Existing ESA project. It is copied and SCIA never opens the original. |
| `--output <file.esa>` | New ESA written by Save As. Default: `<input name>_ThirdPartyRoundtrip.esa` next to the input. Never overwritten and never equal to the input. The folder is created if needed. |
| `--scia-version <year>` | SCIA Engineer registry version (default `2027`). |
| `--scia-root <dir>` | SCIA Engineer installation folder (default: registry `SYSTEM` value). It must contain `SciaEngineer.exe` and `dll_calatrava`. |
| `--scia-temp <dir>` | SCIA temp folder. It must exist and be writable (default: registry `Temp` value). |
| `--with-hinge` | Also add a point hinge to the beam that uses the existing section. Cannot be combined with `--inspect-only`. |
| `--inspect-only` | Open a hidden working copy, verify the test objects with the read-back verifier, then close and shut down without saving. Writes no ESA. Cannot be combined with `--output` or `--with-hinge`. |
| `--expect-hinge` | Only with `--inspect-only`: require a hinge on one of the two test beams. |
| `--help` | Show usage and exit. |

Exit codes: `0` success, `1` failure, `2` usage or input error, `3` SCIA Engineer, `EsaStartupScreen` or the ThirdParty connector is already running.

## What the run does

1. Checks the arguments, the input, the output rules and the SCIA folders. Checks that the SDK assemblies in the selected `dll_calatrava` match the references this build was compiled against, and that the strong-named references among the SDK assemblies are consistent. Any mismatch stops the run before SCIA starts.
2. Refuses to run (exit 3) while `SciaEngineer.exe`, `EsaStartupScreen.exe` or the ThirdParty connector is open. This project never terminates a process.
3. Copies the input to a hidden working copy (`.<name>.working-<guid>.esa`) next to the output, or next to the input for `--inspect-only`.
4. Launches SCIA Engineer on the working copy with the SDK, waits up to 30 s for a fresh `ThirdPartyLink` session with `MarkerFile.txt`, and registers the application.
5. Reads the model, builds the roundtrip scenario (names start with `ThirdPartyRoundtrip_`), and sends the partial model. The scenario is a 300 x 500 mm rectangular section, four nodes, a beam using the existing section, a beam using the new section, and the optional hinge.
6. Save As writes the output ESA and waits up to 10 s for a non-empty file. Close is then sent; SCIA disables Close after Save As, so a refused close prints a warning and the run continues. Shutdown must be confirmed.
7. Deletes the working copy and the `ThirdPartyRuntime\<guid>` folder (next to the executable) after a confirmed shutdown. If shutdown is not confirmed, both are kept, and a warning lists their paths and the SCIA Engineer processes that are still running. End exactly those processes yourself; the tool never terminates a process.

Save, close and shutdown all use `SaveProject = false`; Save As also sets `SaveProjectAs` to the output path.

During the ThirdParty session, Ctrl+C or Ctrl+Break does not terminate the process. The run stops at its next checkpoint: before SCIA starts, while it waits for the ThirdParty session, before the model is processed, and before Save As. If the application is registered, the handler immediately sends a best-effort close without saving and a shutdown; step 7 then applies. When a checkpoint stops the run, it prints `[CANCELLED]` and exits with code `1`.

## Limitations

- The SDK launches SCIA Engineer in standalone (`-ExternalLink`) mode. This is not the SCIA "Custom Check" start with a configuration argument.
- The project refuses to run while SCIA Engineer is open. There are no kill or cleanup options.
- Ctrl+C sends its close and shutdown commands from the console handler thread, so they can overlap an SDK call that is still running on the main thread. The SDK's thread safety has not been verified.
- The partial send is used for the additive test (new objects only). It is not proof that arbitrary in-place edits of existing objects work.
- A SAF 2.3 export logs that `EccentricityEy` and `EccentricityEz` are configured only for SAF 1.0.0–1.0.5 and are skipped. The test beams use default eccentricities.
- The SCIA temp folder comes from the registry unless `--scia-temp` is given. It must match the folder SCIA Engineer uses, or no ThirdPartyLink session appears within 30 s.
- If SCIA Engineer shows the National Annex prompt "copy non-annex settings", answer No.
- No calculation is run.
