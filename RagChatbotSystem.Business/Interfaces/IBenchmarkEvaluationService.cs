using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IBenchmarkEvaluationService
    {
        Task<IReadOnlyList<EvaluationProfileDto>> GetProfilesAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<EvaluationRunSummaryDto>> GetRunsAsync(Guid datasetId, CancellationToken cancellationToken = default);
        Task<EvaluationImportResult> ImportRunnerReportAsync(Guid datasetId, Guid userId, string reportJson, CancellationToken cancellationToken = default);
    }
}
