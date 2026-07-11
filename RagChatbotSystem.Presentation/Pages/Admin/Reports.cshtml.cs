using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Pages.Admin
{
    [Authorize(Roles = "Admin,Teacher")]
    public class ReportsModel : PageModel
    {
        private readonly IStatisticsService _statisticsService;
        private readonly IDatasetService _datasetService;

        public ReportsModel(IStatisticsService statisticsService, IDatasetService datasetService)
        {
            _statisticsService = statisticsService;
            _datasetService = datasetService;
        }

        public TokenUsageSummaryDto Summary { get; set; } = null!;
        public List<DailyTokenUsageDto> DailyUsage { get; set; } = new();
        public List<TopDocumentUsageDto> TopDocuments { get; set; } = new();
        public List<UserTokenUsageLeaderboardDto> Leaderboard { get; set; } = new();
        public CreditReportDto CreditReport { get; set; } = null!;
        public string ReportScopeLabel { get; set; } = "Tất cả môn học";

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var currentUserId))
            {
                return Challenge();
            }

            IReadOnlyCollection<Guid>? datasetScope = null;
            if (role == "Teacher")
            {
                var assignedDatasets = await _datasetService.GetDatasetsForUserAsync(currentUserId, role, cancellationToken);
                datasetScope = assignedDatasets.Select(d => d.DatasetId).ToArray();
                ReportScopeLabel = datasetScope.Count == 0
                    ? "Chỉ các môn học được phân công - chưa có môn học được phân công"
                    : $"Chỉ các môn học được phân công ({datasetScope.Count})";
            }

            Summary = await _statisticsService.GetTokenUsageSummaryAsync(datasetScope, cancellationToken);
            DailyUsage = await _statisticsService.GetDailyTokenUsageAsync(7, datasetScope, cancellationToken);
            TopDocuments = await _statisticsService.GetTopDocumentsUsageAsync(5, datasetScope, cancellationToken);
            Leaderboard = await _statisticsService.GetUserLeaderboardAsync(10, datasetScope, cancellationToken);
            CreditReport = await _statisticsService.GetCreditReportAsync(7, datasetScope, cancellationToken);
            return Page();
        }
    }
}
