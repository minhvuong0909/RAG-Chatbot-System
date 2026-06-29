using System;
using System.Threading;
using System.Threading.Tasks;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IDocumentProgressNotifier
    {
        Task NotifyProgressAsync(Guid datasetId, Guid documentId, string status, int percentComplete, CancellationToken cancellationToken = default);
    }
}
