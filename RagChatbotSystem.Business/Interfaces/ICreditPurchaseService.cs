using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RagChatbotSystem.Business.DTOs;

namespace RagChatbotSystem.Business.Interfaces
{
    public interface ICreditPurchaseService
    {
        Task<IReadOnlyList<CreditPackageDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CreditPackageDto>> GetPackagesAsync(CancellationToken cancellationToken = default);
        Task<CreditPurchaseDto> CreatePurchaseAsync(Guid userId, Guid packageId, CancellationToken cancellationToken = default);
        Task<CreditPurchaseDto> CompletePurchaseAsync(Guid purchaseId, string? providerReference = null, Guid? completedByUserId = null, CancellationToken cancellationToken = default);
        Task<CreditPurchaseDto> CreateManualTopUpAsync(Guid userId, int paidCredits, decimal amount, string currency, Guid createdByUserId, string note, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CreditPurchaseDto>> GetPurchasesAsync(Guid? userId = null, int limit = 100, CancellationToken cancellationToken = default);
    }
}
