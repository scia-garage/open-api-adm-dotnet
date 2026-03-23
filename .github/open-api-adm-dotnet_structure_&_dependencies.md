# OpenAPIAndADMDemo — Project Dependency Graph 

The arrow (──►) means dependency.

```
open_api_adm_dotnet.sln
└── OpenAPIAndADMDemo (.csproj, net48)
    ├── Program.cs
    ├── Configuration/
    │   └── ModelConstants.cs
    ├── Infrastructure/
    │   ├── ProjectManager.cs
    │   ├── SciaAssemblyResolver.cs
    │   ├── SciaEnvironmentManager.cs
    │   └── SciaProcessManager.cs
    ├── ModelBuilding/
    │   ├── IModelBuilder.cs
    │   ├── CrossSectionBuilder.cs
    │   ├── GeometryBuilder.cs
    │   ├── HingeBuilder.cs
    │   ├── LoadBuilder.cs
    │   ├── LoadCaseBuilder.cs
    │   ├── LoadCombinationBuilder.cs
    │   ├── MaterialBuilder.cs
    │   ├── ModelDirector.cs
    │   ├── ProjectInformationBuilder.cs
    │   └── SupportBuilder.cs
    ├── Results/
    │   └── ResultsManager.cs
    ├── Properties/
    │   └── Resources.resx
    │
    │── ─ ─ ─  External Dependencies  ─ ─ ─
    │
    ├──▶ [External] CSInfrastructure.dll                                          ← repo: BIM-API_ModelExchanger
    ├──▶ [External] ModelExchanger.AnalysisDataModel.dll                          ← repo: BIM-API_ModelExchanger
    ├──▶ [External] ModelExchanger.AnalysisDataModel.Contracts.dll                ← repo: BIM-API_ModelExchanger
    ├──▶ [External] ModelExchanger.AnalysisDataModel.Implementation.dll           ← repo: BIM-API_ModelExchanger
    ├──▶ [External] ModelExchanger.AnalysisDataModel.Integration.Bootstrapper.dll ← repo: BIM-API_ModelExchanger
    ├──▶ [External] ModelExchanger.Shared.Models.dll                              ← repo: BIM-API_ModelExchanger
    ├──▶ [External] SciaTools.AdmToAdm.AdmSignalR.Models.dll                      ← repo: SciaTools
    ├──▶ [External] SciaTools.Kernel.Common.Contracts.dll                         ← repo: SciaTools
    ├──▶ [External] SciaTools.Kernel.Common.Implementation.dll                    ← repo: SciaTools
    ├──▶ [External] SciaTools.Kernel.Common.Models.dll                            ← repo: SciaTools
    ├──▶ [External] SciaTools.Kernel.ModelExchangerExtension.Contracts.dll        ← repo: SciaTools
    ├──▶ [External] SciaTools.Kernel.ModelExchangerExtension.Integration.dll      ← repo: SciaTools
    ├──▶ [External] SciaTools.Kernel.ModelExchangerExtension.Models.dll           ← repo: SciaTools
    ├──▶ [External] SCIA.OpenAPI.dll                                              ← repo: SEN
	└──▶ [External] UnitsNet                                                      ← repo: comes with Scia.StructuralToolkit.SciaTools from SciaTools but is .NET library
```

## External SCIA Dependencies

| # | Dependency | Source Repo |
|---|-----------|-------------|
| 1 | ModelExchanger.AnalysisDataModel | BIM-API_ModelExchanger |
| 2 | ModelExchanger.AnalysisDataModel.Contracts | BIM-API_ModelExchanger |
| 3 | ModelExchanger.AnalysisDataModel.Implementation | BIM-API_ModelExchanger |
| 4 | ModelExchanger.AnalysisDataModel.Integration.Bootstrapper | BIM-API_ModelExchanger |
| 5 | ModelExchanger.Shared.Models | BIM-API_ModelExchanger |
| 6 | SciaTools.AdmToAdm.AdmSignalR.Models | SEN |
| 7 | SciaTools.Kernel.Common.Contracts | SciaTools |
| 8 | SciaTools.Kernel.Common.Implementation | SciaTools |
| 9 | SciaTools.Kernel.Common.Models | SciaTools |
| 10 | SciaTools.Kernel.ModelExchangerExtension.Contracts | SciaTools |
| 11 | SciaTools.Kernel.ModelExchangerExtension.Integration | SciaTools |
| 12 | SciaTools.Kernel.ModelExchangerExtension.Models | SciaTools |
| 13 | CSInfrastructure | SEN |
| 14 | SCIA.OpenAPI | SEN |

> No references to **StructuralAnalysisFormat-SDK** were found in this repository.
