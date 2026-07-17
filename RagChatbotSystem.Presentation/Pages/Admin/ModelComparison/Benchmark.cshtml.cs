using System;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Pages.Admin.ModelComparison
{
    [Authorize(Roles = "Admin,Teacher")]
    public class BenchmarkModel : PageModel
    {
        private readonly IDatasetService _datasetService;
        private readonly IBenchmarkEvaluationService _evaluationService;

        public BenchmarkModel(IDatasetService datasetService, IBenchmarkEvaluationService evaluationService)
        {
            _datasetService = datasetService;
            _evaluationService = evaluationService;
        }

        [BindProperty(SupportsGet = true)] public Guid? DatasetId { get; set; }
        [BindProperty] public IFormFile? RunnerReport { get; set; }
        public IReadOnlyList<DatasetDto> Datasets { get; private set; } = Array.Empty<DatasetDto>();
        public IReadOnlyList<EvaluationProfileDto> Profiles { get; private set; } = Array.Empty<EvaluationProfileDto>();
        public IReadOnlyList<EvaluationRunSummaryDto> Runs { get; private set; } = Array.Empty<EvaluationRunSummaryDto>();
        public string? StatusMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!TryGetUser(out var userId, out var role)) return Challenge();
            await LoadAsync(userId, role);
            return Page();
        }

        public async Task<IActionResult> OnPostImportAsync()
        {
            if (!TryGetUser(out var userId, out var role)) return Challenge();
            if (!DatasetId.HasValue || !await _datasetService.CanManageDatasetAsync(userId, role, DatasetId.Value))
                return Forbid();
            if (RunnerReport == null || RunnerReport.Length == 0 || RunnerReport.Length > 10 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(RunnerReport), "Chọn file JSON kết quả runner, tối đa 10 MB.");
                await LoadAsync(userId, role);
                return Page();
            }
            using var reader = new System.IO.StreamReader(RunnerReport.OpenReadStream(), Encoding.UTF8, true);
            try
            {
                var import = await _evaluationService.ImportRunnerReportAsync(DatasetId.Value, userId, await reader.ReadToEndAsync());
                TempData["BenchmarkStatus"] = $"Đã nhập run {import.RunId}: {import.ImportedQuestions} câu hoàn thành, {import.FailedQuestions} câu lỗi.";
                return RedirectToPage(new { DatasetId });
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                ModelState.AddModelError(nameof(RunnerReport), $"Không thể nhập report: {ex.Message}");
                await LoadAsync(userId, role);
                return Page();
            }
        }

        public async Task<IActionResult> OnGetExportAsync(Guid datasetId)
        {
            if (!TryGetUser(out var userId, out var role) || !await _datasetService.CanManageDatasetAsync(userId, role, datasetId)) return Forbid();
            var runs = await _evaluationService.GetRunsAsync(datasetId);
            var csv = new StringBuilder("RunId,Benchmark,Profile,Model,Status,Completed,Total,ContextPrecision,ContextRecall,Faithfulness,AnswerRelevancy,CreatedAtUtc\n");
            foreach (var run in runs)
                csv.Append(string.Join(',', new[] { run.Id.ToString(), Escape($"{run.BenchmarkName} {run.BenchmarkVersion}"), Escape(run.ProfileName), Escape(run.ModelName), run.Status,
                    run.CompletedQuestions.ToString(CultureInfo.InvariantCulture), run.TotalQuestions.ToString(CultureInfo.InvariantCulture), Metric(run.ContextPrecision), Metric(run.ContextRecall), Metric(run.Faithfulness), Metric(run.AnswerRelevancy), run.CreatedAt.ToUniversalTime().ToString("O") })).Append('\n');
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", "rbl-evaluation-runs.csv");
        }

        private async Task LoadAsync(Guid userId, string role)
        {
            Datasets = await _datasetService.GetDatasetsForUserAsync(userId, role);
            Profiles = await _evaluationService.GetProfilesAsync();
            if (DatasetId.HasValue && await _datasetService.CanManageDatasetAsync(userId, role, DatasetId.Value))
                Runs = await _evaluationService.GetRunsAsync(DatasetId.Value);
            StatusMessage = TempData["BenchmarkStatus"] as string;
        }
        private bool TryGetUser(out Guid userId, out string role)
        {
            role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }
        private static string Metric(double? value) => value?.ToString("0.0000", CultureInfo.InvariantCulture) ?? string.Empty;
        private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
