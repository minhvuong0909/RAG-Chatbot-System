using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IModelComparisonService
    {
        IReadOnlyList<string> AvailableProviders { get; }

        Task<ModelComparisonRunResultDto> CompareAsync(
            Guid datasetId,
            string question,
            IReadOnlyList<string> providerKeys,
            CancellationToken cancellationToken = default);
    }
}
