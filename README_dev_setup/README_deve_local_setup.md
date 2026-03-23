------------------------------------------------------------
	# SETUP ADM, SAF, API, SCIA FOR LOCAL DEVELOPMENT
------------------------------------------------------------

# Repositories
`StructuralAnalysisFormat-SDK` https://github.com/StructuralAnalysisFormat/StructuralAnalysisFormat-SDK/commits/deve/osi/7dof/
- no dependency on other repos

`BIM-API_ModelExchanger` https://github.com/Allplan-GmbH/BIM-API_ModelExchanger
- dependency on StructuralAnalysisFormat-SDK

`BIM-API_SciaTools` https://github.com/Allplan-GmbH/BIM-API_SciaTools
- dependency on BIM-API_ModelExchanger

`SCIA_Engineer_SEN` https://github.com/Allplan-GmbH/SCIA_Engineer_SEN
- dependency on BIM-API_SciaTools and BIM-API_ModelExchanger (only when using ADM and API)

`open-api-adm-dotnet` https://github.com/scia-garage/open-api-adm-dotnet
- example / testing App for Open API and ADM

# INFO
`open-api-adm-dotnet` project is starting `SCIA_Engineer_SEN`.
When you want to debug `SCIA_Engineer_SEN` attach to `SCIA_Engineer_SEN` process for debuging when SCIA already started and you see the window, not only splash screen,
otherwise it slows down the starting process and open_api fails to attach to SCIA.

# REPLACE NuGet packages with local repos (for development)

	## StructuralAnalysisFormat-SDK repo
 This repo has zero dependencies on other repos — no NuGet packages, no project references, no path references. It's a standalone repo that the other two consume.

	## BIM-API_ModelExchanger repo
Look into the two files `Directory.Build.targets` and `NETStandard2.0.props` and modify the path to StructuralAnalysisFormat-SDK to fit the real path on your PC.
Than copy the two files into `your\path\to\repo\BIM-API_ModelExchanger\ModelExchangerService\.`.
To revert: Delete both files.
  ┌───────────────────────────┬────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
  │ File                      │ Purpose                                                                                                                    │
  ├───────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ Directory.Build.targets   │ Replaces 3 SAF NuGet packages with ProjectReferences to D:\git\SAF-SDK_1\StructuralAnalysisFormat-SDK (8 projects total)   │
  ├───────────────────────────┼────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ NETStandard2.0.props      │ Redirect stub fixing $(SolutionDir) paths for SAF projects built from ModelExchanger                                       │
  └───────────────────────────┴────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘

Package mapping:
   - StructuralAnalysisFormat → 6 projects (Infrastructure, DataAccess.*)
   - StructuralAnalysisFormat.Bootstrappers.SimpleInjector4 → 1 project
   - StructuralAnalysisFormat.Tests.Infrastructure → 1 project

	## in SciaTools repo
Look into the files `Directory.Build.targets`, `NETStandard2.0.props` and `ModelExchanger.NETStandard2.0.props`
and modify the path to StructuralAnalysisFormat-SDK to fit the real path on your PC.
Than copy the `Directory.Build.targets` and `NETStandard2.0.props` files to path `your\path\to\repo\SciaTools\.`.
And copy the `ModelExchanger.NETStandard2.0.props` file to path `your\path\to\repo\SciaTools\Common.`.
files swaps ModelExchanger NuGet packages for direct ProjectReferences to your local repo at build time. Here's how:

When you build SciaTools, MSBuild compiles the ModelExchanger source code directly from `D:\git\ModelExchanger_1\BIM-API_ModelExchanger\` instead of downloading pre-built NuGet packages.

  This means:
   - Changes you make in ModelExchanger are immediately visible in SciaTools builds — no need to publish a new NuGet package
   - All other NuGet dependencies (Newtonsoft.Json, SimpleInjector, etc.) still come from NuGet as usual
   - To go back to NuGet packages, just delete `Directory.Build.targets` and `Common\ModelExchanger.NETStandard2.0.props`

when you delete `Directory.Build.targets`, `NETStandard2.0.props` and `ModelExchanger.NETStandard2.0.props` than it loads NuGet packages again.
Delete those three files and run dotnet restore — SciaTools will go back to downloading ModelExchanger NuGet packages (version 1.16.5) as before.

  8 NuGet packages → 38 project references:
   - CSInfrastructure → 1 project
   - Infrastructure.Geometry → 1 project
   - ModelExchanger.AnalysisDataModel → 6 projects
   - ModelExchanger.AnalysisDataModel.DataBuilders → 2 projects
   - ModelExchanger.Scia → 7 projects
   - ModelExchanger.Excel → 3 projects
   - ModelExchanger.Json → 3 projects
   - ModelExchanger.BimPlus → 15 projects
   
	## SCIA_Engineer_SEN repo
Look into the file `StructuralToolkitPostBuild.Local.ps1` and modify the path to `$SciaToolsBin` to fit your local repository path.
Than copy the fie to path `your\path\to\repo\SEN\A\Externals\C#\XEP_ExternalLibs\.`.

What it does: Replaces NuGet download with local builds — copies DLLs from `your\path\to\repo\SciaTools\bin\Debug\dll_calatrava\`
(which already includes ModelExchanger DLLs via the ProjectReferences we set up).

To activate it, change the PostBuildEvent in `\SEN\A\Externals\C#\XEP_ExternalLibs\XEP_ExternalLibs.16.00.csproj` from: `StructuralToolkitPostBuild.ps1`
to: `StructuralToolkitPostBuild.Local.ps1`
   
Prerequisites: Build first StructuralAnalysisFormat-SDK, than BIM-API_ModelExchanger, than BIM-API_SciaTools.

To revert: Switch the PostBuildEvent back to `StructuralToolkitPostBuild.ps1`.

	## open_api_adm_dotnet repo
Look into these three files
`\open-api-adm-dotnet\OpenAPIAndADMDemo\Properties\launchSettings.json`
`\open-api-adm-dotnet\OpenAPIAndADMDemo\OpenAPIAndADMDemo.csproj`
`\open-api-adm-dotnet\OpenAPIAndADMDemo\Infrastructure\SciaEnvironmentManager.cs`
And modify the path to your local repositories (replace all ocurrences of "path\to\your\repo" or "path\\to\\your\\repo").

These files includes paths to BIM-API_ModelExchanger, SCIA_Engineer_SEN and BIM-API_SciaTools repos -> need to be modified
