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

namespace RagChatbotSystem.Presentation.Pages.Admin.ModelComparison
{
    [Authorize(Roles = "Admin,Teacher")]
    public class HistoryModel : PageModel
    {
        // Container chạy giờ UTC nên .ToLocalTime() không quy đổi đúng giờ Việt Nam — quy đổi tường minh như TokenUsageService đã làm.
        private static readonly TimeZoneInfo VietnamTz = ResolveVietnamTimeZone();

        private readonly IModelComparisonService _modelComparisonService;

        public HistoryModel(IModelComparisonService modelComparisonService)
        {
            _modelComparisonService = modelComparisonService;
        }

        public static string FormatVietnamTime(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTz).ToString("dd/MM/yyyy HH:mm");

        private static TimeZoneInfo ResolveVietnamTimeZone()
        {
            foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "UTC+07", "UTC+07");
        }

        public IReadOnlyList<ModelComparisonRunSummaryDto> Runs { get; private set; } = Array.Empty<ModelComparisonRunSummaryDto>();
        public ModelComparisonStatsDto Stats { get; private set; } = new(Array.Empty<ModelComparisonProviderStatDto>());

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Challenge();
            }

            Runs = await _modelComparisonService.GetHistoryAsync(userId, role, cancellationToken);
            Stats = await _modelComparisonService.GetStatsAsync(userId, role, cancellationToken);
            return Page();
        }
    }
}
