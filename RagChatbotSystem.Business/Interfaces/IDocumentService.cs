using System;
using System.Threading.Tasks;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface IDocumentService
    {
        Task<Document?> ProcessAndIndexDocumentAsync(Guid datasetId, Guid userId, string fileName, string rawText);
        Task<bool> DeleteDocumentAsync(Guid documentId);
    }
}
