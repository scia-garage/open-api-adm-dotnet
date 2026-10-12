# SCIA OpenAPI and ADM Demo Project

This is an example project demonstrating how to use SCIA Engineer's OpenAPI and Analysis Data Model (ADM)
to programmatically:

1.  Create a structural analysis model
2.  Run linear FEM calculation
3.  Read the FEM calculation results

## 🎯 Purpose

This example project is designed for both **structural engineers** who want to learn how to automate SCIA
Engineer workflows using C# and the OpenAPI, as well as professional software developers who want to use
SCIA as a background calculation engine for their projects. It provides a complete working example that
creates a 3D structural model, runs calculations, and extracts results programmatically.

## 🏗️ What This Demo Creates

The example utilizes Analysis Data Model (ADM) and Open API to create a **FEM structure** in SCIA Engineer
featuring:

-   **Materials**: Steel (S355) and Concrete (C30/37)
-   **Cross-sections**: HEA260 steel profile and 600x300mm concrete sections
-   **Structural Elements**:

    +   5 steel columns (C1-C5) connecting ground to top level
    +   3 steel beams (B1-B3) at the top level
    +   2 concrete slabs: top (S1) and bottom (S2)

    +   1 concrete wall (W1)
    +   Slab opening (O1) in the top slab
    +   Region with a different thickness in bottom slab

-   **Supports**:

    +   Fixed point supports (PS1-PS4) with free X-rotation at the bottom of columns C1-C4
    +   Fixed point support (PS5) at the bottom of column C5
    +   Surface support (SS1) under the bottom slab with elastic soil parameters

-   **Hinges**:

    +   Point hinges at the start and end of the beam B3 with free Y-rotation
    +   Linear hinges at the bottom and top of the wall

-   **Loads**:

    +   3 load cases (_LC1-LC3_)
    +   Line loads (forces and moments) on columns and beams in load case 1 (_LC1_)
    +   Surface load on top slab and point loads on column C5 in load case 2 (_LC2_)
    +   One load combination _LComb1_

After creating the structure, the app sends it to SCIA Engineer and starts a linear FEM
calculation. Then, results are read and printed in the console.

## 📋 Prerequisites

### Required Software

1.  [**SCIA Engineer 2027**](https://www.scia.net/en/scia-engineer/downloads) (the version used for testing; the registry key
    `HKEY_CURRENT_USER\Software\SCIA\ESA\2027` must exist, i.e. SCIA Engineer must have been started at least once)

2.  **.NET Framework 4.8 developer pack**
    +   Download from: <https://dotnet.microsoft.com/download/dotnet-framework/net48>
    +   Or use winget: `winget install Microsoft.DotNet.Framework.DeveloperPack_4.8`

3.  IDE

    a.  **Microsoft Visual Studio 2022**

    +   Download from: <https://visualstudio.microsoft.com/downloads>

    +   Or use winget: `winget install Microsoft.VisualStudio.2022.Community`

    b.  Or **VS Code** with C# extension

    +   Download from: <https://code.visualstudio.com/download>

    +   or use winget: `winget install Microsoft.VisualStudioCode`

### System Requirements

See the [system requirements of SCIA Engineer](https://www.scia.net/en/support/faq/scia-engineer/installation/system-requirements).

## 🚀 Quick Start

### 1. Clone the Repository

```shell
git clone https://github.com/scia-garage/open-api-adm-dotnet.git
cd open-api-adm-dotnet
```

> [!NOTE]
> All CLI commands we show below need to be run in this directory

### 2. Verify SCIA Engineer Installation

Ensure SCIA Engineer is installed at the default location:

```shell
if exist "C:\Program Files\SCIA\Engineer2027" (echo OK) else (echo Not found)
```

> [!NOTE]
> If SCIA Engineer is not installed under this location, pass the installation folder to the build, e.g.
> `dotnet build open_api_adm_dotnet.sln -p:SciaEngineerDir="D:\SCIA\Engineer2027\"`
> (see [configuration](#️-configuration)).

### 3. Open the project

-   With VS-Code

    +   Open VS-Code and go to _File -> Open Folder_ and open the _open-api-adm-dotnet_ folder  
        OR in the CLI type: `code .`
    +   Wait a couple of seconds for the pop-up about installing extensions recommended for this
        project to pop up or open the _Extensions_ tab (CTRL+E) and type `@recommended`
        in the search field
    +   Install all recommended extensions

-   With Visual Studio

    Simply open the solution file _open_api_adm_dotnet.sln_

### 4. Build the Project

-   Using CLI

    ```shell
    dotnet build open_api_adm_dotnet.sln
    ```

-   Or using VS-Code

    The default build task is defined in the _.vscode/tasks.json_.  
    Just go to: "Terminal -> Run Build Task" or Press Ctrl+Shift+B

-   Or using Visual Studio

    Build with Ctrl+Shift+B

The binaries are created in `OpenAPIAndADMDemo/bin/Debug/net48`

> [!NOTE]
> When the console input is redirected (for example in automated runs), the demo skips the "press any key" prompts.

### 5. Test the demo application

```shell
dotnet run --project OpenAPIAndADMDemo
```

#### Expected Workflow

1.  Console application starts and initializes SCIA Engineer environment
2.  SCIA Engineer opens automatically
3.  A new empty project is created
4.  The demo builds the complete structural model using ADM
5.  Model is sent to SCIA Engineer (you'll see the 3D structure)
6.  **Press any key** when prompted to run the calculation
7.  Linear analysis is performed
8.  Results are extracted and displayed in the console
9.  **Press any key** to close SCIA Engineer

### 6. Debugging

-   Using VS-Code

    The debug configuration is defined in _.vscode/launch.json_. You only need to:

    +   Set a breakpoint anywhere in the code
    +   Press F5

-   Using Visual Studio

    +   Set a breakpoint anywhere in the code
    +   Press F5

## 🔁 Roundtrip examples: modifying an existing ESA project

Besides the demo above (which creates a new model in an empty project), two further console applications show
how to **open an existing project, load it as an ADM model, modify it, send it back and save it as a new ESA file**.
Both add the same test objects: a beam using a cross-section that already exists in the project, a new
300x500 mm rectangular cross-section (reusing the project's material), a second beam using it, and optionally a
point hinge (`--with-hinge`). Neither runs a calculation, and the input project is never modified.

| Project | API | Details |
|---|---|---|
| [OpenApiAdmSciaEngRoundtrip](OpenApiAdmSciaEngRoundtrip/README.md) | SCIA OpenAPI + ADM | Starts SCIA Engineer, opens a copy of your ESA, refreshes the ADM from SCIA, adds the objects, refreshes SCIA from ADM and saves on close |
| [SciaApiThirdPartySciaEngRoundtrip](SciaApiThirdPartySciaEngRoundtrip/README.md) | Third Party SDK ADM | Launches SCIA Engineer with a copy of your ESA, reads the ADM through the SDK, sends a partial ADM and uses Save As |

No sample ESA is included in this repository. Use any populated project that contains at least one beam with a
cross-section and a material, for example:

```shell
dotnet build open_api_adm_dotnet.sln -c Release
OpenApiAdmSciaEngRoundtrip\bin\Release\net48\OpenApiAdmSciaEngRoundtrip.exe --input C:\Projects\MyProject.esa --with-hinge
SciaApiThirdPartySciaEngRoundtrip\bin\Release\net48\SciaApiThirdPartySciaEngRoundtrip.exe --input C:\Projects\MyProject.esa
```

Close SCIA Engineer before running them. Both can re-open their output with `--inspect-only` to verify it.

> [!NOTE]
> Projects saved with an older SCIA Engineer version make SCIA ask whether to convert the data (the converted copy
> cannot be opened by the older version), and some projects trigger the question whether to copy the non-annex
> settings from the previous National Annex. Always work on copies. Answering **No** to the annex question was the
> safe choice in our tests.

## 📁 Project Structure

```txt
OpenAPIAndADMDemo/
├── Program.cs                    # Main entry point
├── Configuration/
│   └── ModelConstants.cs         # Version and configuration settings
├── ModelBuilding/
│   └── ModelDirector.cs          # Orchestrates ADM model creation with the builders from SciaApi.AdmModeling
└── Results/
    └── ResultsManager.cs         # Results extraction and display
```

The infrastructure and the builder classes are shared with the roundtrip examples in the following libraries:

```txt
SciaApi.Infrastructure/           # No SCIA dependency: usable before any SCIA assembly is loaded
├── SciaEnvironmentManager.cs     # Getting the SCIA installation and temp paths (registry key of the version or explicit root)
├── SciaProcessManager.cs         # Detecting running SCIA processes, killing orphan runs
├── SciaAssemblyResolver.cs       # DLL loading and resolution
└── EsaWorkingCopy.cs             # Safe copy of an input ESA, SHA-256
SciaApi.AdmModeling/              # Pure ADM, no SCIA.OpenAPI
├── ModelBuilding/                # Builder pattern classes (IModelBuilder, MaterialBuilder, GeometryBuilder, ...)
└── Roundtrip/                    # ExistingModelCatalog, RoundtripScenarioBuilder, RoundtripVerifier, ModelValidation
SciaApi.OpenApi/                  # SCIA.OpenAPI glue
├── ProjectManager.cs             # Managing ESA project: creating empty template or opening an existing project, closing it
├── StructureAdmWriter.cs         # Adding ADM objects to the open project (CreateAdmObject)
└── LoadedAdmAccessor.cs          # Access to the ADM model loaded from SCIA (OpenAPI has no public accessor)
SciaApi.AdmModeling.SelfCheck/    # Offline check of the builders and roundtrip helpers (no SCIA started)
```

## 🛠️ Configuration

### SCIA Engineer Version Configuration

The project is configured for SCIA Engineer 2027, installed in `C:\Program Files\SCIA\Engineer2027\`.
The installation folder is defined once in _Directory.Build.props_ as `SciaEngineerDir` and used by all projects.
To use a different version or location:

1.  **Update ModelConstants.cs** - the version is the name of the registry key
    `HKEY_CURRENT_USER\Software\SCIA\ESA\<version>`:

    ```csharp
    public const string SciaVersion = "2027"; // Change to your version
    ```

2.  **Build with the installation folder** (trailing backslash optional), or change the default in _Directory.Build.props_:

    ```shell
    dotnet build open_api_adm_dotnet.sln -p:SciaEngineerDir="C:\Program Files\SCIA\Engineer2027\"
    ```

The roundtrip examples take `--scia-version <version>` and optionally `--scia-root <folder>` on the command line.

### Custom Installation Path

If SCIA Engineer is installed in a non-standard location, pass `-p:SciaEngineerDir=...` to the build as shown
above and, for the roundtrip examples, `--scia-root` at run time. Individual `<HintPath>` items no longer need
to be edited.

## 🧪 Experimenting Guide

### Adding New Materials

In ModelDirector.cs, the call `.SetupDefaultMaterials()` creates some basic materials.
Replace it with something like:

```csharp
.AddMaterial("Steel_S275", MaterialType.Steel, "S275", 210, 80, 0.3, 7850);
```

### Creating Different Geometry

To build a structural analysis model with a different geometry, modify the calls of `GeometryBuilder` in ModelDirector.cs:

```csharp
.AddNode("NewNode", x, y, z);
.AddLineMember("NewBeam", "StartNode", "EndNode", "CrossSection", Member1DType.Beam, "BeamsLayer");
```

The same applies for supports, hinges, load cases, loads and load combinations: modify
the calls of `SupportBuilder`, `HingeBuilder`, `LoadCaseBuilder`, `LoadBuilder` and `LoadCombinationBuilder`
respectively.

### Extracting Additional Results

The results are read in Program.cs using `ResultsManager`. Add additional calls to extract
more results. Remember to call `PrintAllResults` at the end

```csharp
ReadMemberStresses("Beam B1 : Stresses : Load case LC1", "LC1", "B1");
```

## 🚨 Troubleshooting

### Server timeout

```shell
2025-01-23 12:34:56.7890|ERROR|SCIA.OpenAPI.AdmToAdmServiceWrapper|Server start timeout. SCIA Engineer Application must be terminated manually!
```

SCIA Engineer didn't start within a timeout.

-   Close the demo
-   After some time, SCIA Engineer will eventually start. Close it.
-   Restart the demo

### Other issues

If you are still not able to start the example, it might help moving the .exe file to the SCIA
installation folder (by default `C:\Program Files\SCIA\Engineer25.0` )

## 📚 Additional Resources

### SCIA Documentation

-   [SCIA OpenAPI and ADM Documentation](https://help.scia.net/api)
-   [SCIA Engineer User Manual](https://help.scia.net)

## 📝 License

This project is provided under the MIT License. See LICENSE file for details.

> [!NOTE]
> This demo requires a valid SCIA Engineer license. The demo code is free to use, but SCIA
> Engineer software licensing terms apply.

## ⚠️ Disclaimer

This is an educational demonstration project. Always verify results with manual calculations or
alternative methods before using in production structural design. The authors are not responsible
for any design decisions made based on this code.
