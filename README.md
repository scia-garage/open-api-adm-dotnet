# SCIA OpenAPI and ADM Demo Project

This is an example project demonstrating how to use SCIA Engineer's OpenAPI and Analysis Data Model (ADM)
to programmatically:

1.  Create a structural analysis model
2.  Run linera FEM calculation
3.  Read the FEM calculation results

## 🎯 Purpose

This example project is designed for both **structural engineers** who want to learn how to automate SCIA
Engineer workflows using C# and the OpenAPI, as well as professional software developers who wants to use
SCIA as a background calculation engine for teir projects. It provides a complete working example that
creates a 3D structural model, runs calculations, and extracts results programmatically.

## 🏗️ What This Demo Creates

The example creates a **FEM structure** using Analysis Data Model (ADM) featuring:

-   **Materials**: Steel (S355) and Concrete (C30/37)
-   **Cross-sections**: HEA260 steel profiles and 600x300mm concrete sections
-   **Structural Elements**:

    +   5 steel columns (C1-C5) connecting ground to top level
    +   3 steel beams (B1-B3) at the top level
    +   2 concrete slabs

        *   top slab (S1) with an opening O1
        *   bottom slab (S2) with a region qith a different thickness

    +   1 concrete wall (W1)
    +   Slab opening (O1) in the top slab
    +   Variable thickness region in bottom slab

-   **Supports**:

    +   Fixed point supports (PS1-PS4) with free X-rotation at the bottom of columns C1-C4
    +   Fixed point support (PS5) with at the bottom of column C5
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

1.  [**SCIA Engineer 25.0**](https://www.scia.net/en/scia-engineer/downloads)

    > [!Note]
    > If SCIA Engineer is not installed in the default location under `C:\Program Files\SCIA\Engineer25.0\`
    > you have to adapt the DLL paths in _OpenAPIAndADMDemo.csproj_ file!

2.  **.NET Framework 4.8 targeting pack**

    ```shell
    winget install Microsoft.DotNet.Framework.DeveloperPack_4.8
    ```

3.  **Microsoft Visual Studio 2022**

    ```shell
    winget install Microsoft.VisualStudio.2022.Community
    ```

    Or **VS Code** with C# extension

    ```shell
    winget install Microsoft.VisualStudioCode
    ```

### System Requirements

See the [system requirements of SCIA Engineer](https://www.scia.net/en/support/faq/scia-engineer/installation/system-requirements).

## 🚀 Quick Start

### 1. Clone the Repository

```bash
git clone https://github.com/your-username/open_api_adm_dotnet.git
cd open_api_adm_dotnet
```

### 2. Verify SCIA Engineer Installation

Ensure SCIA Engineer is installed at the default location:

```shell
cd "C:\Program Files\SCIA\Engineer25.0"
```

> [!Note]
> If installed elsewhere, you'll need to update the paths in `OpenAPIAndADMDemo.csproj` (see [Configuration](#configuration) section).

### 3. Build the Project

```bash
# Using dotnet CLI
dotnet build OpenAPIAndADMDemo.sln

# Or using Visual Studio
# Open the .sln file and build with Ctrl+Shift+B
```

### 4. Run the Demo

```bash
# From command line
dotnet run --project OpenAPIAndADMDemo

# Or using Visual Studio
# Set OpenAPIAndADMDemo as startup project and press F5
```

### 5. Expected Workflow

1.  Console application starts and initializes SCIA Engineer environment
2.  SCIA Engineer opens automatically
3.  A new empty project is created
4.  The demo builds the complete structural model using ADM
5.  Model is sent to SCIA Engineer (you'll see the 3D structure)
6.  **Press any key** when prompted to run the calculation
7.  Linear analysis is performed
8.  Results are extracted and displayed in the console
9.  **Press any key** to close SCIA Engineer

## 📁 Project Structure

```txt
OpenAPIAndADMDemo/
├── Program.cs                    # Main entry point
├── Configuration/
│   └── ModelConstants.cs         # Version and configuration settings
├── Infrastructure/
│   ├── SciaEnvironmentManager.cs # Manger for getting the SCIA installation and temp paths
│   ├── SciaProcessManager.cs     # Manager for killing orpan runs of SCIA Engineer
│   ├── ProjectManager.cs         # Managing ESA project: creating empty template, opening and closing project in SCIA
│   └── SciaAssemblyResolver.cs   # DLL loading and resolution
├── ModelBuilding/
│   ├── IModelBuilder.cs          # Builder pattern interface
│   ├── ModelDirector.cs          # Orchestrates ADM model creation
│   ├── ProjectInformationBuilder.cs # Project metadata
│   ├── MaterialBuilder.cs        # Defining materials
│   ├── CrossSectionBuilder.cs    # Creating 1D-member cross-sections
│   ├── GeometryBuilder.cs        # It's where FEM model geometry is created
│   ├── SupportBuilder.cs         # Defining supports
│   ├── HingeBuilder.cs           # Defining hinges
│   ├── LoadCaseBuilder.cs        # Load case definitions
│   ├── LoadBuilder.cs            # Defining point, line and surface loads
│   └── LoadCombinationBuilder.cs # Load combination definitions
└── Results/
    └── ResultsManager.cs         # Results extraction and display
```

## 🛠️ Configuration

### SCIA Engineer Version Configuration

The project is configured for SCIA Engineer 25.0. To use a different version:

1.  **Update ModelConstants.cs**:

    ```csharp
    public const string SciaVersion = "24.1"; // Change to your version
    ```

2.  **Update .csproj file** - Replace all instances of `Engineer25.0` with your version:

    ```xml
    <HintPath>C:\Program Files\SCIA\Engineer24.1\OpenAPI_dll\SCIA.OpenAPI.dll</HintPath>
    ```

### Custom Installation Path

If SCIA Engineer is installed in a non-standard location, update the paths in `OpenAPIAndADMDemo.csproj`
for **each** `<Reference>` item:

```xml
<!-- Change this base path -->
<HintPath>C:\Custom\Path\SCIA\Engineer25.0\OpenAPI_dll\SCIA.OpenAPI.dll</HintPath>
```

## 🧪 Experimenting Guide

### Adding New Materials

In ModelDirector.cs, the call `.SetupDefaultMaterials()` create some basic materials.
Replace it with something like:

```csharp
.AddMaterial("Steel_S275", MaterialType.Steel, "S275", 210, 80, 0.3, 7850);
```

### Creating different Geometry

TO build a different structure geometry, modify the calls of `GeometryBuilder` in ModelDirector.cs:

```csharp
.AddNode("NewNode", x, y, z);
.AddLineMember("NewBeam", "StartNode", "EndNode", "CrossSection", Member1DType.Beam, "BeamsLayer");
```

The same applies for supports, hinges, load cases, loads and load combinations: modify
the calls of `SupportBuilder`, `HingeBuilder`, `LoadCaseBuilder`, `LoadBuilder` and `LoadCombinationBuilder`
respectively.

### Extracting Additional Results

The results are read in Program.cs using `ResultsManager`. Add additional calls to extract
mode results. Remember to call `PrintAllResults` at the end

```csharp
// In ResultsManager.cs or create new methods
ReadMemberStresses("Beam B1 : Stresses : Load case LC1", "LC1", "B1");
```

## 🚨 Troubleshooting

### Common Issues

```shell
2025-01-23 12:34:56.7890|ERROR|SCIA.OpenAPI.AdmToAdmServiceWrapper|Server start timeout. SCIA Engineer Application must be terminated manually!
```

SCIA Engineer didn't started within a timeout.

-   Close the demo
-   After some time, SCIA Engineer will eventually start. Close it.
-   Try again

### Debug Mode

To see detailed logging, modify `Program.cs`:
```csharp
Console.WriteLine("Debug: Model building step completed");
```

## 📚 Additional Resources

### SCIA Documentation

-   [SCIA OpenAPI and ADM Documentation](https://help.scia.net/api)
-   [SCIA Engineer User Manual](https://help.scia.net)

## 📝 License

This project is provided under the MIT License. See LICENSE file for details.

**Note**: This demo requires a valid SCIA Engineer license. The demo code is free to use, but SCIA
Engineer software licensing terms apply.

## ⚠️ Disclaimer

This is an educational demonstration project. Always verify results with manual calculations or
alternative methods before using in production structural design. The authors are not responsible
for any design decisions made based on this code.
