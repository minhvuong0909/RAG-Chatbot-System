using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;

namespace RagChatbotSystem.Business.Services
{
    public class CreditPurchaseService : ICreditPurchaseService
    {
        private readonly AppDbContext _db;
        private readonly ICreditService _creditService;

        public CreditPurchaseService(AppDbContext db, ICreditService creditService)
        {
            _db = db;
            _creditService = creditService;
        }

        public async Task<IReadOnlyList<CreditPackageDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default)
        {
            return await _db.CreditPackages.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .Select(ToPackageDtoExpression())
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<CreditPackageDto>> GetPackagesAsync(CancellationToken cancellationToken = default)
        {
            return await _db.CreditPackages.AsNoTracking()
                .OrderBy(p => p.DisplayOrder)
                .Select(ToPackageDtoExpression())
                .ToListAsync(cancellationToken);
        }

        public async Task<CreditPurchaseDto> CreatePurchaseAsync(Guid userId, Guid packageId, CancellationToken cancellationToken = default)
        {
            var package = await _db.CreditPackages.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == packageId && p.IsActive, cancellationToken)
                ?? throw new InvalidOperationException("Credit package was not found or is inactive.");

            var purchase = new CreditPurchase
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PackageId = package.Id,
                BaseCredits = package.BaseCredits,
                BonusCredits = package.BonusCredits,
                TotalCredits = package.TotalCredits,
                Amount = package.Price,
                Currency = package.Currency,
                Status = CreditPurchaseStatus.PENDING,
                PaymentProvider = "PAYOS",
                CreatedAt = DateTime.UtcNow
            };

            _db.CreditPurchases.Add(purchase);
            await _db.SaveChangesAsync(cancellationToken);
            return ToDto(purchase);
        }

        public async Task<CreditPurchaseDto> CompletePurchaseAsync(Guid purchaseId, string? providerReference = null, Guid? completedByUserId = null, CancellationToken cancellationToken = default)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var purchase = await _db.CreditPurchases
                .FirstOrDefaultAsync(p => p.Id == purchaseId, cancellationToken)
                ?? throw new InvalidOperationException("Credit purchase was not found.");

            if (purchase.Status == CreditPurchaseStatus.COMPLETED)
            {
                return ToDto(purchase);
            }

            if (purchase.Status != CreditPurchaseStatus.PENDING)
            {
                throw new InvalidOperationException("Only pending purchases can be completed.");
            }

            purchase.Status = CreditPurchaseStatus.COMPLETED;
            purchase.CompletedAt = DateTime.UtcNow;
            purchase.ProviderReference = providerReference;
            purchase.CreatedByUserId = completedByUserId ?? purchase.CreatedByUserId;

            await _creditService.AddPaidCreditsAsync(
                purchase.UserId,
                purchase.TotalCredits,
                completedByUserId,
                $"Completed credit purchase {purchase.Id}.",
                purchase.PackageId,
                cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToDto(purchase);
        }

        public async Task<CreditPurchaseDto> AttachPaymentLinkAsync(Guid purchaseId, long orderCode, string paymentLinkId, string checkoutUrl, CancellationToken cancellationToken = default)
        {
            var purchase = await _db.CreditPurchases.FirstOrDefaultAsync(p => p.Id == purchaseId, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy giao dịch mua Credit.");

            if (purchase.Status != CreditPurchaseStatus.PENDING)
            {
                throw new InvalidOperationException("Chỉ có thể tạo liên kết thanh toán cho giao dịch đang chờ.");
            }

            purchase.PaymentProvider = "PAYOS";
            purchase.ProviderOrderCode = orderCode;
            purchase.ProviderReference = paymentLinkId;
            purchase.CheckoutUrl = checkoutUrl;
            await _db.SaveChangesAsync(cancellationToken);
            return ToDto(purchase);
        }

        public async Task<CreditPurchaseDto?> GetPurchaseByOrderCodeAsync(long orderCode, CancellationToken cancellationToken = default)
        {
            return await _db.CreditPurchases.AsNoTracking()
                .Where(p => p.ProviderOrderCode == orderCode)
                .Select(ToPurchaseDtoExpression())
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<CreditPurchaseDto> CompletePayOsPurchaseAsync(long orderCode, decimal paidAmount, string providerReference, CancellationToken cancellationToken = default)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var purchase = await _db.CreditPurchases
                .FirstOrDefaultAsync(p => p.ProviderOrderCode == orderCode, cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy giao dịch PayOS.");

            if (!string.Equals(purchase.PaymentProvider, "PAYOS", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Giao dịch không thuộc PayOS.");
            }

            if (purchase.Amount != paidAmount)
            {
                throw new InvalidOperationException("Số tiền PayOS không khớp với đơn hàng.");
            }

            if (purchase.Status == CreditPurchaseStatus.COMPLETED)
            {
                return ToDto(purchase);
            }

            var completedAt = DateTime.UtcNow;
            var affected = await _db.CreditPurchases
                .Where(p => p.Id == purchase.Id && p.Status == CreditPurchaseStatus.PENDING)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Status, CreditPurchaseStatus.COMPLETED)
                    .SetProperty(p => p.CompletedAt, completedAt)
                    .SetProperty(p => p.ProviderReference, providerReference), cancellationToken);

            if (affected == 0)
            {
                _db.ChangeTracker.Clear();
                var current = await _db.CreditPurchases.AsNoTracking().FirstAsync(p => p.Id == purchase.Id, cancellationToken);
                if (current.Status == CreditPurchaseStatus.COMPLETED) return ToDto(current);
                throw new InvalidOperationException("Giao dịch PayOS không còn ở trạng thái chờ thanh toán.");
            }

            await _creditService.AddPaidCreditsAsync(
                purchase.UserId,
                purchase.TotalCredits,
                null,
                $"Nạp Credit qua PayOS, mã đơn {orderCode}.",
                purchase.PackageId,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return ToDto(await _db.CreditPurchases.AsNoTracking().FirstAsync(p => p.Id == purchase.Id, cancellationToken));
        }

        public async Task<bool> CancelPayOsPurchaseAsync(long orderCode, CancellationToken cancellationToken = default)
        {
            var affected = await _db.CreditPurchases
                .Where(p => p.ProviderOrderCode == orderCode && p.Status == CreditPurchaseStatus.PENDING)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.Status, CreditPurchaseStatus.CANCELLED), cancellationToken);
            return affected > 0;
        }

        public async Task<CreditPurchaseDto> CreateManualTopUpAsync(Guid userId, int paidCredits, decimal amount, string currency, Guid createdByUserId, string note, CancellationToken cancellationToken = default)
        {
            if (paidCredits <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(paidCredits), "Paid credits must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(note))
            {
                throw new ArgumentException("A note is required for manual top-ups.", nameof(note));
            }

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var purchase = new CreditPurchase
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PackageId = null,
                BaseCredits = paidCredits,
                BonusCredits = 0,
                TotalCredits = paidCredits,
                Amount = Math.Max(0, amount),
                Currency = string.IsNullOrWhiteSpace(currency) ? "VND" : currency.Trim().ToUpperInvariant(),
                Status = CreditPurchaseStatus.COMPLETED,
                PaymentProvider = "ADMIN_MANUAL",
                ProviderReference = null,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                CreatedByUserId = createdByUserId
            };

            _db.CreditPurchases.Add(purchase);
            await _creditService.AddPaidCreditsAsync(userId, paidCredits, createdByUserId, note, null, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToDto(purchase);
        }

        public async Task<IReadOnlyList<CreditPurchaseDto>> GetPurchasesAsync(Guid? userId = null, int limit = 100, CancellationToken cancellationToken = default)
        {
            var query = _db.CreditPurchases.AsNoTracking();
            if (userId.HasValue)
            {
                query = query.Where(p => p.UserId == userId.Value);
            }

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .Take(Math.Clamp(limit, 1, 500))
                .Select(ToPurchaseDtoExpression())
                .ToListAsync(cancellationToken);
        }

        private static System.Linq.Expressions.Expression<Func<CreditPackage, CreditPackageDto>> ToPackageDtoExpression()
        {
            return p => new CreditPackageDto(
                p.Id,
                p.Name,
                p.Description,
                p.BaseCredits,
                p.BonusCredits,
                p.TotalCredits,
                p.Price,
                p.Currency,
                p.IsActive,
                p.DisplayOrder);
        }

        private static CreditPurchaseDto ToDto(CreditPurchase purchase)
        {
            return new CreditPurchaseDto(
                purchase.Id,
                purchase.UserId,
                purchase.PackageId,
                purchase.BaseCredits,
                purchase.BonusCredits,
                purchase.TotalCredits,
                purchase.Amount,
                purchase.Currency,
                purchase.Status,
                purchase.PaymentProvider,
                purchase.ProviderReference,
                purchase.CreatedAt,
                purchase.CompletedAt,
                purchase.CreatedByUserId,
                purchase.ProviderOrderCode,
                purchase.CheckoutUrl);
        }

        private static System.Linq.Expressions.Expression<Func<CreditPurchase, CreditPurchaseDto>> ToPurchaseDtoExpression()
        {
            return p => new CreditPurchaseDto(
                p.Id,
                p.UserId,
                p.PackageId,
                p.BaseCredits,
                p.BonusCredits,
                p.TotalCredits,
                p.Amount,
                p.Currency,
                p.Status,
                p.PaymentProvider,
                p.ProviderReference,
                p.CreatedAt,
                p.CompletedAt,
                p.CreatedByUserId,
                p.ProviderOrderCode,
                p.CheckoutUrl);
        }
    }
}
