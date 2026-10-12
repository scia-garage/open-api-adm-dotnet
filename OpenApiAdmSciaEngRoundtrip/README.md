# OpenApiAdmSciaEngRoundtrip

Console application (.NET Framework 4.8) that round-trips test objects through the SCIA OpenAPI/ADM interface on an **existing** ESA project that you supply. It:

1. Opens a copy of the project in SCIA Engineer and loads its ADM model.
2. Reuses a cross-section and material that already exist in that model.
3. Adds a 300 x 500 mm rectangular cross-section, four nodes and two beams, and optionally a point hinge.
4. Saves the result as a separate ESA file.

It never runs a calculation, and the input file is never modified.

## How it differs from OpenAPIAndADMDemo

| | OpenAPIAndADMDemo | OpenApiAdmSciaEngRoundtrip |
|---|---|---|
| Starting project | Empty project template created by the demo | Existing ESA passed with `--input` |
| Written file | The template project | A separate copy (`--output`); `--inspect-only` writes no ESA, only a hidden temporary copy that is deleted |
| Model | Complete model built in ADM and sent | Test objects added to the loaded model in dependency stages |
| Calculation | Runs and reads results | Never |

## Prerequisites

- Windows with SCIA Engineer 2027 installed. The installation folder is read from `HKCU\Software\SCIA\ESA\2027\Admin\Dir` (`SYSTEM`) unless you pass `--scia-root`. The folder must contain `OpenAPI_dll`.
- .NET Framework 4.8 developer pack and the .NET SDK, to build net48 projects.
- An input ESA: any populated project with at least one beam that has a cross-section and a material. No sample ESA is committed; supply your own.
- Write access to the folder of `--input`: the default output is written there, and `--inspect-only` creates a hidden copy there.

## Build

```powershell
dotnet build OpenApiAdmSciaEngRoundtrip\OpenApiAdmSciaEngRoundtrip.csproj -c Release
```

The SCIA Engineer folder defaults to `C:\Program Files\SCIA\Engineer2027\` (see `Directory.Build.props`). Override it when SCIA Engineer is installed elsewhere:

```powershell
dotnet build OpenApiAdmSciaEngRoundtrip\OpenApiAdmSciaEngRoundtrip.csproj -c Release -p:SciaEngineerDir="D:\SCIA\Engineer2027\"
```

The executable is `OpenApiAdmSciaEngRoundtrip\bin\Release\net48\OpenApiAdmSciaEngRoundtrip.exe`.

## Run

Replace the paths with your own project. The examples assume the repository root as the current folder.

```powershell
$exe = ".\OpenApiAdmSciaEngRoundtrip\bin\Release\net48\OpenApiAdmSciaEngRoundtrip.exe"

# Copy with the test objects, saved next to the input as MyProject_OpenApiRoundtrip.esa
& $exe --input "C:\Projects\MyProject.esa"

# Same, with a point hinge and an explicit output path
& $exe --input "C:\Projects\MyProject.esa" --with-hinge --output "C:\Projects\MyProject_Hinge.esa"

# Verify saved outputs without saving anything
& $exe --inspect-only --input "C:\Projects\MyProject_OpenApiRoundtrip.esa"
& $exe --inspect-only --expect-hinge --input "C:\Projects\MyProject_Hinge.esa"

# Show the options and exit codes
& $exe --help
```

## Options

| Option | Default | Meaning |
|---|---|---|
| `--input <file.esa>` | required | Existing project to read. It is never modified. |
| `--output <file.esa>` | `<input name>_OpenApiRoundtrip.esa` next to the input | New copy to write. Must not exist and must differ from the input. Not allowed with `--inspect-only`. |
| `--with-hinge` | off | Also add a point hinge on the beam that uses the existing cross-section. Not allowed with `--inspect-only`. |
| `--inspect-only` | off | Verify the test objects in a hidden copy of `--input` (created next to it and deleted afterwards), closing the copy without saving. `--input` is only read. |
| `--expect-hinge` | off | With `--inspect-only`: require a hinge connected to one of the two test beams. |
| `--scia-version <version>` | `2027` | Registry version used to find the SCIA Engineer installation. |
| `--scia-root <dir>` | registry `SYSTEM` value | SCIA Engineer installation folder. Must contain `OpenAPI_dll`. |
| `--kill-orphans` | off | **Destructive.** Kills every running `SciaEngineer` and `EsaStartupScreen` process, not only orphaned ones, then waits up to 15 s for the SCIA Engineer processes to exit. `SciaTools.ThirdParty.Business.ThirdPartyConnector` is never killed: if it is still running, the run stops with exit code 3 and you must end it manually. |
| `--help`, `-h` | | Show the usage and exit. |

Exit codes: `0` success; `1` failure (SCIA or ADM error, validation failure, save not confirmed, missing SCIA Engineer installation, input changed during `--inspect-only`); `2` usage or input error (missing or invalid input, existing output, missing `--scia-root` folder); `3` SCIA Engineer is already running. Without `--kill-orphans`, exit code 3 terminates no process. With `--kill-orphans`, the `SciaEngineer` and `EsaStartupScreen` processes are killed before the check, and `ThirdPartyConnector` is never killed.

Argument checks, file checks and the installation check (`--scia-root` or the registry value, and `OpenAPI_dll\SCIA.OpenAPI.dll`) run first and do not touch SCIA Engineer processes. `--kill-orphans` acts only after them, so it never kills a process for invalid input or a missing installation.

## What it modifies

Modify mode writes only into the copy:

- one new cross-section `OpenApiRoundtrip_Rectangle_300x500`: a parametric rectangle, 500 mm high and 300 mm wide, that uses the material of an existing cross-section in the model;
- four point nodes `OpenApiRoundtrip_N1` to `N4` forming two parallel 3 m beam lines that start 2 m beyond the largest node X, at the largest node Z. The first line is centred on the node Y range and the second is 2 m further in Y;
- beam `OpenApiRoundtrip_Beam_ExistingSection` on the existing cross-section and beam `OpenApiRoundtrip_Beam_Rectangle` on the new section, both 3 m long;
- with `--with-hinge`: point hinge `OpenApiRoundtrip_PointHinge` (`RelConnectsStructuralMember`) on the beam that uses the existing cross-section.

Existing objects are referenced, not changed. A name that is already taken receives a `_NNN` suffix.

## Save semantics

- The copy is created before SCIA Engineer starts. The input is only read.
- The copy is written only by `ProjectManager.CloseProject(SaveMode.SaveChangesYes)`, which must return `true`. SCIA.OpenAPI 27 has no SaveAs, so this is the only way the changes persist.
- A failure before a confirmed save closes the project with `SaveChangesNo` and deletes the unsaved copy. If the copy stays locked, the deletion is retried for up to 20 s; then a warning names the file, which must be deleted before running again.
- `--inspect-only` never gives the input to SCIA Engineer. It copies the input to a hidden file next to it named `.<name>.inspect-<guid>.esa`, opens the copy, closes it with `SaveChangesNo` (which discards unsaved changes) and deletes it. The input's SHA-256 is compared before and after, after the copy has been deleted, and a change is a failure (exit code 1), also when the inspection itself failed. No output ESA is created. The hidden copy needs write access to the input folder. If it cannot be deleted, a warning names it; if the process is terminated, it can remain and must be deleted by hand.
- The per-run temp folder under the application folder is deleted at the end of every run; a warning is printed if that fails.

## Known limitations

- **National Annex prompt.** On open, SCIA Engineer may ask whether to copy non-annex settings into the new annex. Answer **No**. Answering Yes triggered a SCIA assertion during testing.
- **Staged adds.** A full-model `TrySynchronizeModel` failed on a populated test project. The objects are therefore added with staged `Structure.CreateAdmObject` calls (section, nodes, beams, then the hinge), each checked, followed by `RefreshModel_ToSCIAEngineer`.
- **Reflection.** SCIA.OpenAPI 27.0.42.64 has no public accessor for the populated `AnalysisModel`. `LoadedAdmAccessor` reads it through the private `Structure.ServiceWrapper` field and the public `Client` property. This is tied to that SCIA.OpenAPI version.
- **Hinge.** `--with-hinge` uses the sample point-release representation. If ADM or SCIA rejects it, the run stops with an explicit message and nothing is saved. No alternative mapping is attempted.
- **No calculation** is run.

## Source layout

- `Program.cs`: entry point, exit codes, assembly resolver and STA thread.
- `CliOptions.cs`: option parsing, validation and usage text.
- `RoundtripSession.cs`: SCIA Engineer lifecycle for modify and inspect modes.
- `OpenApiAdmSciaEngRoundtrip.csproj`: references the shared `SciaApi.*` projects. The SCIA assemblies are referenced with `Private=False` and loaded from the installation at run time.
