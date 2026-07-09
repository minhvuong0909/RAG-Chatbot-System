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

namespace RagChatbotSystem.Presentation.Pages.Teacher
{
    [Authorize(Roles = "Teacher")]
    public class AnalyticsModel : PageModel
    {
        private readonly IDatasetService _datasetService;
        private readonly IStatisticsService _statisticsService;

        public AnalyticsModel(IDatasetService datasetService, IStatisticsService statisticsService)
        {
            _datasetService = datasetService;
            _statisticsService = statisticsService;
        }

        public IReadOnlyList<DatasetDto> AssignedSubjects { get; private set; } = Array.Empty<DatasetDto>();
        public TokenUsageSummaryDto Summary { get; private set; } = new();
        public List<DailyTokenUsageDto> DailyUsage { get; private set; } = new();
        public List<TopSubjectUsageDto> TopSubjects { get; private set; } = new();
        public List<TopDocumentUsageDto> TopDocuments { get; private set; } = new();
        public List<StudentLearningEngagementDto> StudentEngagement { get; private set; } = new();
        public int ActiveStudentsCount { get; private set; }
        public int MaxDailyQuestions { get; private set; }
        public int MaxCitationCount { get; private set; }

        public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out var teacherId))
            {
                return Challenge();
            }

            AssignedSubjects = await _datasetService.GetDatasetsForUserAsync(teacherId, "Teacher", cancellationToken);
            var assignedSubjectIds = AssignedSubjects.Select(subject => subject.DatasetId).ToArray();

            Summary = await _statisticsService.GetTokenUsageSummaryAsync(assignedSubjectIds, cancellationToken);
            DailyUsage = await _statisticsService.GetDailyTokenUsageAsync(7, assignedSubjectIds, cancellationToken);
            ActiveStudentsCount = await _statisticsService.GetActiveStudentCountAsync(assignedSubjectIds, cancellationToken);
            TopSubjects = await _statisticsService.GetSubjectLearningActivityByQuestionCountAsync(5, assignedSubjectIds, cancellationToken);
            TopDocuments = await _statisticsService.GetTopDocumentsUsageAsync(5, assignedSubjectIds, cancellationToken);
            StudentEngagement = await _statisticsService.GetStudentEngagementByQuestionCountAsync(10, assignedSubjectIds, cancellationToken);

            MaxDailyQuestions = DailyUsage.Count == 0 ? 0 : DailyUsage.Max(day => day.QueryCount);
            MaxCitationCount = TopDocuments.Count == 0 ? 0 : TopDocuments.Max(document => document.CitationCount);

            return Page();
        }
    }
}
