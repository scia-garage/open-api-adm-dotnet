using System;
using SCIA.OpenAPI.Results;
using SCIA.OpenAPI;
using SCIA.OpenAPI.Utils;
using Results64Enums;

namespace OpenAPIAndADMDemo.Results 
{

    /// <summary>
    /// Manages the results of a SCIA project.
    /// </summary>
    public class ResultsManager : IDisposable
    {
        private Structure _model;
        private readonly ResultsAPI _resultsApi;
        private OpenApiE2EResults _resultStorage;
        private bool _disposed = false;


        public Structure Model => _model;
        public OpenApiE2EResults Results => _resultStorage;

        /// <summary>
        /// Initializes a new instance of the ResultsManager.
        /// </summary>
        /// <param name="projectManager">The ProjectManager instance managing the SCIA project.</param>
        public ResultsManager(Structure model)
        {
            _model = model;
            _resultStorage = new OpenApiE2EResults();
            _resultsApi = _model.InitializeResultsAPI() ?? throw new InvalidOperationException("Failed to initialize ResultsAPI. Did you run the calculation?");
        }
        public Result ReadMemberInternalForces(
            string resultsName,
            string caseName,
            string memberName,
            eDsElementType caseType = eDsElementType.eDsElementType_LoadCase)
        {
            OpenApiE2EResult result = new OpenApiE2EResult(resultsName);

            ResultKey key = new ResultKey
            {
                CaseType = caseType,
                CaseId = _model.FindGuid(caseName),
                EntityType = eDsElementType.eDsElementType_Beam,
                EntityName = memberName,
                Dimension = eDimension.eDim_1D,
                ResultType = eResultType.eFemBeamInnerForces,
                CoordSystem = eCoordSystem.eCoordSys_Local
            };

            result.Result = _resultsApi.LoadResult(key);
            result.ResultKey = key;
            _resultStorage.SetResult(result);

            return result.Result;
        }

        public Result ReadMemberDeformations(
            string resultsName,
            string caseName,
            string memberName,
            eDsElementType caseType = eDsElementType.eDsElementType_LoadCase,
            bool relative = false)
        {
            OpenApiE2EResult result = new OpenApiE2EResult(resultsName);

            ResultKey key = new ResultKey
            {
                CaseType = caseType,
                CaseId = _model.FindGuid(caseName),
                EntityType = eDsElementType.eDsElementType_Beam,
                EntityName = memberName,
                Dimension = eDimension.eDim_1D,
                ResultType = relative ? eResultType.eFemBeamRelativeDeformation : eResultType.eFemBeamDeformation,
                CoordSystem = eCoordSystem.eCoordSys_Local
            };
            result.Result = _resultsApi.LoadResult(key);
            result.ResultKey = key;
            _resultStorage.SetResult(result);
            return result.Result;
        }

        public Result ReadSurfaceInternalForces(
            string resultsName,
            string caseName,
            string surfaceName,
            eDsElementType caseType = eDsElementType.eDsElementType_LoadCase,
            bool extended = false)
        {
            OpenApiE2EResult result = new OpenApiE2EResult(resultsName);

            ResultKey key = new ResultKey
            {
                CaseType = caseType,
                CaseId = _model.FindGuid(caseName),
                EntityType = eDsElementType.eDsElementType_Slab,
                EntityName = surfaceName,
                Dimension = eDimension.eDim_2D,
                ResultType = extended ? eResultType.eFemInnerForces_Extended : eResultType.eFemInnerForces,
                CoordSystem = eCoordSystem.eCoordSys_Local
            };
            result.Result = _resultsApi.LoadResult(key);
            result.ResultKey = key;
            _resultStorage.SetResult(result);
            return result.Result;
        }

        public Result ReadSurfaceDeformations(
            string resultsName,
            string caseName,
            string surfaceName,
            eDsElementType caseType = eDsElementType.eDsElementType_LoadCase)
        {
            OpenApiE2EResult result = new OpenApiE2EResult(resultsName);

            ResultKey key = new ResultKey
            {
                CaseType = caseType,
                CaseId = _model.FindGuid(caseName),
                EntityType = eDsElementType.eDsElementType_Slab,
                EntityName = surfaceName,
                Dimension = eDimension.eDim_2D,
                ResultType = eResultType.eFemDeformations,
                CoordSystem = eCoordSystem.eCoordSys_Local
            };
            result.Result = _resultsApi.LoadResult(key);
            result.ResultKey = key;
            _resultStorage.SetResult(result);
            return result.Result;
        }

        public Result ReadSurfaceStresses(
            string resultsName,
            string caseName,
            string surfaceName,
            eDsElementType caseType = eDsElementType.eDsElementType_LoadCase)
        {
            OpenApiE2EResult result = new OpenApiE2EResult(resultsName);

            ResultKey key = new ResultKey
            {
                CaseType = caseType,
                CaseId = _model.FindGuid(caseName),
                EntityType = eDsElementType.eDsElementType_Slab,
                EntityName = surfaceName,
                Dimension = eDimension.eDim_2D,
                ResultType = eResultType.eFemStress,
                CoordSystem = eCoordSystem.eCoordSys_Local
            };
            result.Result = _resultsApi.LoadResult(key);
            result.ResultKey = key;
            _resultStorage.SetResult(result);
            return result.Result;
        }

        public Result ReadSurfaceStrains(
            string resultsName,
            string caseName,
            string surfaceName,
            eDsElementType caseType = eDsElementType.eDsElementType_LoadCase
        )
        {
            OpenApiE2EResult result = new OpenApiE2EResult(resultsName);

            ResultKey key = new ResultKey
            {
                CaseType = caseType,
                CaseId = _model.FindGuid(caseName),
                EntityType = eDsElementType.eDsElementType_Slab,
                EntityName = surfaceName,
                Dimension = eDimension.eDim_2D,
                ResultType = eResultType.eFemStrains,
                CoordSystem = eCoordSystem.eCoordSys_Local
            };
            result.Result = _resultsApi.LoadResult(key);
            result.ResultKey = key;
            _resultStorage.SetResult(result);
            return result.Result;
        }

        public Result ReadPointSupportReactions(
            string resultsName,
            string caseName,
            string supportName,
            eDsElementType caseType = eDsElementType.eDsElementType_LoadCase
        )
        {
            OpenApiE2EResult result = new OpenApiE2EResult(resultsName);

            ResultKey key = new ResultKey
            {
                CaseType = caseType,
                CaseId = _model.FindGuid(caseName),
                EntityType = eDsElementType.eDsElementType_PointSupportPoint,
                EntityName = supportName,
                Dimension = eDimension.eDim_reactionsPoint,
                ResultType = eResultType.eResultTypeReactionsSupport0D,
                CoordSystem = eCoordSystem.eCoordSys_Local
            };
            result.Result = _resultsApi.LoadResult(key);
            result.ResultKey = key;
            _resultStorage.SetResult(result);
            return result.Result;
        }
        public Result ReadSurfaceContactStresses(
            string resultsName,
            string caseName,
            string supportName,
            eDsElementType caseType = eDsElementType.eDsElementType_LoadCase
        )
        {
            OpenApiE2EResult result = new OpenApiE2EResult(resultsName);

            ResultKey key = new ResultKey
            {
                CaseType = caseType,
                CaseId = _model.FindGuid(caseName),
                EntityType = eDsElementType.eDsElementType_Slab,
                EntityName = supportName,
                Dimension = eDimension.eDim_2D,
                ResultType = eResultType.eFemContactStress,
                CoordSystem = eCoordSystem.eCoordSys_Local
            };
            result.Result = _resultsApi.LoadResult(key);
            result.ResultKey = key;
            _resultStorage.SetResult(result);
            return result.Result;
        }

        public void PrintAllResults()
        {
            foreach (var kvp in _resultStorage.GetAll())
            {
                Console.WriteLine($"----------------------- {kvp.Key} --------------------------------------");
                Console.WriteLine(kvp.Value.Result.GetTextOutput());
            }
        }
    

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
                    _resultsApi?.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~ResultsManager()
        {
            Dispose(false);
        }
    }
}