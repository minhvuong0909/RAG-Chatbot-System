using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Pages.Admin.ModelComparison
{
    [Authorize(Roles = "Admin,Teacher")]
    public class RunDetailModel : PageModel
    {
        private readonly IDatasetService _datasetService;
        private readonly IBenchmarkEvaluationService _evaluationService;

        public RunDetailModel(IDatasetService datasetService, IBenchmarkEvaluationService evaluationService)
        {
            _datasetService = datasetService;
            _evaluationService = evaluationService;
        }

        public Guid DatasetId { get; private set; }
        public EvaluationRunDetailDto? RunDetail { get; private set; }

        public async Task<IActionResult> OnGetAsync(Guid datasetId, Guid runId)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Challenge();
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

            if (!await _datasetService.CanManageDatasetAsync(userId, role, datasetId)) return Forbid();

            DatasetId = datasetId;
            RunDetail = await _evaluationService.GetRunDetailAsync(runId);

            if (RunDetail == null) return NotFound("Evaluation run not found.");

            return Page();
        }
    }
}
