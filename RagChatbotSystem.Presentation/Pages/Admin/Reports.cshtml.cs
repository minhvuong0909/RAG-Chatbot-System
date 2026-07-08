using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Pages.Admin
{
    [Authorize(Roles = "Admin,Teacher")]
    public class ReportsModel : PageModel
    {
        private readonly IStatisticsService _statisticsService;

        public ReportsModel(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        public TokenUsageSummaryDto Summary { get; set; } = null!;
        public List<DailyTokenUsageDto> DailyUsage { get; set; } = new();
        public List<TopDocumentUsageDto> TopDocuments { get; set; } = new();
        public List<UserTokenUsageLeaderboardDto> Leaderboard { get; set; } = new();

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            Summary = await _statisticsService.GetTokenUsageSummaryAsync(cancellationToken);
            DailyUsage = await _statisticsService.GetDailyTokenUsageAsync(7, cancellationToken);
            TopDocuments = await _statisticsService.GetTopDocumentsUsageAsync(5, cancellationToken);
            Leaderboard = await _statisticsService.GetUserLeaderboardAsync(10, cancellationToken);
        }
    }
}
