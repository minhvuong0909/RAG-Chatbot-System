using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Pages
{
    [Authorize(Roles = "Student")]
    public class CreditsModel : PageModel
    {
        private readonly ICreditService _creditService;
        private readonly ICreditPurchaseService _purchaseService;

        public CreditsModel(ICreditService creditService, ICreditPurchaseService purchaseService)
        {
            _creditService = creditService;
            _purchaseService = purchaseService;
        }

        public CreditBalanceDto Balance { get; private set; } = null!;
        public IReadOnlyList<CreditPackageDto> Packages { get; private set; } = Array.Empty<CreditPackageDto>();
        public IReadOnlyList<CreditLedgerDto> Ledger { get; private set; } = Array.Empty<CreditLedgerDto>();
        public IReadOnlyList<CreditPurchaseDto> Purchases { get; private set; } = Array.Empty<CreditPurchaseDto>();

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                return Challenge();
            }

            Balance = await _creditService.GetStudentCreditSummaryAsync(userId, cancellationToken);
            Packages = await _purchaseService.GetActivePackagesAsync(cancellationToken);
            Ledger = await _creditService.GetLedgerAsync(userId, 25, cancellationToken);
            Purchases = await _purchaseService.GetPurchasesAsync(userId, 25, cancellationToken);
            return Page();
        }
    }
}
