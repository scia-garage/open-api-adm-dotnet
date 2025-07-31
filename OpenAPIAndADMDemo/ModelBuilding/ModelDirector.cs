using ModelExchanger.AnalysisDataModel.Contracts;
using ModelExchanger.AnalysisDataModel.Models;
using ModelExchanger.AnalysisDataModel;
using ModelExchanger.AnalysisDataModel.Enums;
using ModelExchanger.AnalysisDataModel.Subtypes;
using ModelExchanger.AnalysisDataModel.Integration.Bootstrapper;
using SciaTools.Kernel.ModelExchangerExtension.Models.Exchange;

using System;
using System.Collections.Generic;
using SCIA.OpenAPI;
using SciaTools.AdmToAdm.AdmSignalR.Models.ModelModification;
using UnitsNet;

namespace OpenAPIAndADMDemo.ModelBuilding
{
    /// <summary>
    /// Orchestrates the model building process using the builder pattern
    /// </summary>
    public class ModelDirector
    {
        private readonly Structure _model;
        public ModelDirector(Structure model)
        {
            _model = model;
        }

        /// <summary>
        /// Creates a complete structural model using the ADM
        /// </summary>
        public void BuildCompleteModel()
        {
            AnalysisModel admModel = new AnalysisModel();
            AnalysisDataModelBootstrapper admBootstrapper = new AnalysisDataModelBootstrapper();

            using (var scope = admBootstrapper.CreateThreadedScope())
            {
                IAnalysisModelService modelService = scope.GetService<IAnalysisModelService>();
                IAnalysisModelQuery queryService = scope.GetService<IAnalysisModelQuery>();

                // Step 1: Model Information
                var modelInfoBuilder = new ModelInformationBuilder(admModel, modelService, "ExampleModel");
                modelInfoBuilder.Build();

                // Step 2: Materials
                var materialBuilder = new MaterialBuilder(admModel, modelService)
                    .SetupDefaultMaterials();
                materialBuilder.Build();

                // Step 3: Cross-sections
                var crossSectionBuilder = new CrossSectionBuilder(admModel, modelService)
                    .SetupDefaultSections();
                crossSectionBuilder.Build();

                // Step 4: Geometry

                // Model dimensions
                double a = 4.0, b = 5.0, c = 3.0;

                // Opening dimensions for regions and openings
                double lengthOpening = 1.0;
                double widthOpening = 1.0;

                var geometryBuilder = new GeometryBuilder(admModel, modelService)
                    // Bottom level nodes
                    .AddNode("N1", 0, 0, 0)
                    .AddNode("N2", a, 0, 0)
                    .AddNode("N3", a, b, 0)
                    .AddNode("N4", 0, b, 0)
                    .AddNode("N5", 2 * a, 0, 0)
                    
                    // Top level nodes
                    .AddNode("N11", 0, 0, c)
                    .AddNode("N12", a, 0, c)
                    .AddNode("N13", a, b, c)
                    .AddNode("N14", 0, b, c)
                    .AddNode("N15", 2 * a, 0, c)
                    
                    // Columns
                    .AddLineMember("C1", "N1", "N11", "HEA260", Member1DType.Column, "Columns")
                    .AddLineMember("C2", "N2", "N12", "HEA260", Member1DType.Column, "Columns")
                    .AddLineMember("C3", "N3", "N13", "HEA260", Member1DType.Column, "Columns")
                    .AddLineMember("C4", "N4", "N14", "HEA260", Member1DType.Column, "Columns")
                    .AddLineMember("C5", "N5", "N15", "HEA260", Member1DType.Column, "Columns")
                    
                    // Top level beams
                    .AddLineMember("B1", "N11", "N12", "HEA260", Member1DType.Beam, "Beams")
                    .AddLineMember("B2", "N13", "N14", "HEA260", Member1DType.Beam, "Beams")
                    .AddLineMember("B3", "N12", "N15", "HEA260", Member1DType.Beam, "Beams")
                    
                    // Top slab
                    .AddSurfaceMember("S1", new string[] { "N11", "N12", "N13", "N14" }, "Concrete", 0.3, Member2DType.Plate)
                    
                    // Bottom slab
                    .AddSurfaceMember("S2", new string[] { "N1", "N2", "N3", "N4" }, "Concrete", 0.3)
                    
                    // Side wall
                    .AddSurfaceMember("W1", new string[] { "N3", "N4", "N14", "N13" }, "Concrete", 0.3, Member2DType.Wall)
                    
                    // Additional nodes for opening in top slab
                    .AddNode("N101", 0.5 * a - 0.5 * lengthOpening, 0.5 * b - 0.5 * widthOpening, c)
                    .AddNode("N102", 0.5 * a + 0.5 * lengthOpening, 0.5 * b - 0.5 * widthOpening, c)
                    .AddNode("N103", 0.5 * a + 0.5 * lengthOpening, 0.5 * b + 0.5 * widthOpening, c)
                    .AddNode("N104", 0.5 * a - 0.5 * lengthOpening, 0.5 * b + 0.5 * widthOpening, c)
                    
                    // Opening in top slab
                    .AddOpening("O1", "S1", new string[] { "N101", "N102", "N103", "N104" })
                    
                    // Additional nodes for the region with different thickness on bottom slab
                    .AddNode("N111", 0.5 * a - 0.5 * lengthOpening, 0.5 * b - 0.5 * widthOpening, 0)
                    .AddNode("N112", 0.5 * a + 0.5 * lengthOpening, 0.5 * b - 0.5 * widthOpening, 0)
                    .AddNode("N113", 0.5 * a + 0.5 * lengthOpening, 0.5 * b + 0.5 * widthOpening, 0)
                    .AddNode("N114", 0.5 * a - 0.5 * lengthOpening, 0.5 * b + 0.5 * widthOpening, 0)
                    
                    // Region on bottom slab with different thickness
                    .AddRegion("Region", "S2", new string[] { "N111", "N112", "N113", "N114" }, "Concrete", 0.6);
                    
                geometryBuilder.Build();

                // Step 5: Supports
                
                // Define standard constraint values
                var freeRotation = new Constraint<RotationalStiffness?>(
                    ConstraintType.Free, 
                    RotationalStiffness.FromKilonewtonMetersPerRadian(0));

                var fixedRotation = new Constraint<RotationalStiffness?>(
                    ConstraintType.Rigid, 
                    RotationalStiffness.FromKilonewtonMetersPerRadian(1e+10));

                var fixedTranslation = new Constraint<ForcePerLength?>(
                    ConstraintType.Rigid, 
                    ForcePerLength.FromKilonewtonsPerMeter(1e+10));

                var freeXRotationContraint = new PointConstraints
                {
                    RotationX = freeRotation,
                    RotationY = fixedRotation,
                    RotationZ = fixedRotation,
                    TranslationX = fixedTranslation,
                    TranslationY = fixedTranslation,
                    TranslationZ = fixedTranslation
                };
                var allFixedConstraint = new PointConstraints
                {
                    RotationX = fixedRotation,
                    RotationY = fixedRotation,
                    RotationZ = fixedRotation,
                    TranslationX = fixedTranslation,
                    TranslationY = fixedTranslation,
                    TranslationZ = fixedTranslation
                };

                // Add surface support under the bottom slab
                var subsoil = new Subsoil(
                    "Subsoil",
                    SpecificWeight.FromMeganewtonsPerCubicMeter(80.5),
                    SpecificWeight.FromMeganewtonsPerCubicMeter(35.5),
                    SpecificWeight.FromMeganewtonsPerCubicMeter(50),
                    ForcePerLength.FromMeganewtonsPerMeter(15.5),
                    ForcePerLength.FromMeganewtonsPerMeter(10.2)
                );

                var supportBuilder = new SupportBuilder(admModel, modelService)
                    // Add the point supports under the columns
                    .AddPointSupport("PS1", "N1", freeXRotationContraint)
                    .AddPointSupport("PS2", "N2", freeXRotationContraint)
                    .AddPointSupport("PS3", "N3", freeXRotationContraint)
                    .AddPointSupport("PS4", "N4", freeXRotationContraint)
                    .AddPointSupport("PS5", "N5", allFixedConstraint)
                    // Add surface support under the bottom slab
                    .AddSurfaceSupport("SS1", "S2", subsoil);
                    
                supportBuilder.Build();

                // Step 6: Hinges
                
                var pointConstraints = new PointConstraints
                {
                    TranslationX = fixedTranslation,
                    TranslationY = fixedTranslation,
                    TranslationZ = fixedTranslation,
                    RotationX = fixedRotation,
                    RotationY = freeRotation,
                    RotationZ = fixedRotation
                };

                // Define standard constraint values for linear hinges
                var fixedTranslationLine = new Constraint<Pressure?>(
                    ConstraintType.Rigid, 
                    Pressure.FromKilopascals(1e+10));

                var freeRotationLine = new Constraint<RotationalStiffnessPerLength?>(
                    ConstraintType.Free, 
                    RotationalStiffnessPerLength.FromKilonewtonMetersPerRadianPerMeter(0));

                var freeRotationFixedTranslationConstraint = new LinearConstraints
                {
                    TranslationX = fixedTranslationLine,
                    TranslationY = fixedTranslationLine,
                    TranslationZ = fixedTranslationLine,
                    RotationX = freeRotationLine
                };

                var hingeBuilder = new HingeBuilder(admModel, modelService)
                    // Add point hinges to one of the top beams
                    .AddPointHinge("H1", "B3", Position.Both, pointConstraints)
                    // Add linear hinges at the bottom and top of the wall
                    .AddLinearHinge("LH1", "W1", 0, 0, 1, freeRotationFixedTranslationConstraint)
                    .AddLinearHinge("LH2", "W1", 2, 0, 1, freeRotationFixedTranslationConstraint);
                    
                hingeBuilder.Build();

                // Step 7: Load Cases and Groups

                var loadCaseBuilder = new LoadCaseBuilder(admModel, modelService)
                    // Add default load group
                    .AddLoadGroup("LG1", LoadGroupType.Variable, Load.Domestic)
                    // Add load cases to the group
                    .AddLoadCase("LC1", "LG1", ActionType.Variable, LoadCaseType.Static, ModelExchanger.AnalysisDataModel.Enums.Duration.Long)
                    .AddLoadCase("LC2", "LG1", ActionType.Variable, LoadCaseType.Static, ModelExchanger.AnalysisDataModel.Enums.Duration.Long)
                    .AddLoadCase("LC3", "LG1", ActionType.Variable, LoadCaseType.Static, ModelExchanger.AnalysisDataModel.Enums.Duration.Long);
                    
                loadCaseBuilder.Build();

                // Step 8: Loads and Moments
                
                var loadBuilder = new LoadBuilder(admModel, modelService)
                    // Line loads on columns
                    .AddLineForceOnMember("LL_C1", "C1", "LC1", 5.0, ActionDirection.X)
                    .AddLineForceOnMember("LL_C2", "C2", "LC1", -5.0, ActionDirection.Y)
                    .AddLineForceOnMember("LL_C5", "C5", "LC1", -5.0, ActionDirection.Y)
                    // Edge loads on surface S1
                    .AddLineForceOnSurfaceEdge("EL_S1E1", "S1", 0, "LC1", -5.0, ActionDirection.Z)
                    .AddLineForceOnSurfaceEdge("EL_S1E3", "S1", 2, "LC1", -5.0, ActionDirection.Z)
                    // Surface load on S1
                    .AddSurfaceLoad("SL_S1", "S1", "LC2", -7.0, ActionDirection.Z)
                    // Point forces
                    .AddPointForceOnNode("FP_N15", "N15", "LC2", -150.0, ActionDirection.Z)
                    .AddPointForceOnMember("PF_C5", "C5", "LC2", -150.0, ActionDirection.X, 0.5)
                    // Line moment
                    .AddLineMomentOnMember("LM_B3", "B3", "LC1", -5.0, MomentDirection.Mx);
                    
                loadBuilder.Build();

                // Step 9: Load Combinations
                
                // Create a combination with different factors for each load case
                var loadCaseFactors = new List<LoadCombinationBuilder.LoadCaseFactors>
                {
                    new LoadCombinationBuilder.LoadCaseFactors("LC1", 1.0, 1.5),
                    new LoadCombinationBuilder.LoadCaseFactors("LC2", 1.0, 1.35),
                    new LoadCombinationBuilder.LoadCaseFactors("LC3", 1.0, 1.35)
                };

                var loadCombinationBuilder = new LoadCombinationBuilder(admModel, modelService)
                    .AddLoadCombination(
                        "LComb1", 
                        LoadCaseCombinationCategory.AccordingNationalStandard,
                        loadCaseFactors,
                        LoadCaseCombinationStandard.EnUlsSetC);
                        
                loadCombinationBuilder.Build();

                Console.WriteLine("Model building process completed successfully!");
            }
            admModel.EnforceModelValidity();

            // Add all ADM objects to the Structure
            foreach (IAnalysisObject admObject in admModel)
            {
                ResultOfPartialAddToAnalysisModel result = _model.CreateAdmObject(admObject);
                if (result.PartialAddResult.Status != AdmChangeStatus.Ok)
                {
                    Console.WriteLine($"Error adding object {admObject.Name} to model: {result.PartialAddResult.Errors}");
                }
            }
        }
    }
}
