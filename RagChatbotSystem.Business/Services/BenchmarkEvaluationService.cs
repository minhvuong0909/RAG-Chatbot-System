using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Business.Services
{
    /// <summary>Imports auditable output produced by the isolated Python batch runner.</summary>
    public class BenchmarkEvaluationService : IBenchmarkEvaluationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public BenchmarkEvaluationService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task<IReadOnlyList<EvaluationProfileDto>> GetProfilesAsync(CancellationToken cancellationToken = default)
            => await _unitOfWork.Repository<EvaluationProfile>().GetQueryable().AsNoTracking()
                .OrderBy(profile => profile.Name)
                .Select(profile => new EvaluationProfileDto(profile.Id, profile.Name, profile.Slug, profile.ChunkingStrategy,
                    profile.ChunkSize, profile.ChunkOverlap, profile.EmbeddingModel, profile.TopK, profile.IsEnabled))
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<EvaluationRunSummaryDto>> GetRunsAsync(Guid datasetId, CancellationToken cancellationToken = default)
        {
            var runs = await _unitOfWork.Repository<EvaluationRun>().GetQueryable().AsNoTracking()
                .Where(run => run.DatasetId == datasetId)
                .Include(run => run.BenchmarkDefinition)
                .Include(run => run.EvaluationProfile)
                .Include(run => run.Results)
                .OrderByDescending(run => run.CreatedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            return runs.Select(run => new EvaluationRunSummaryDto(run.Id, run.BenchmarkDefinition.Name, run.BenchmarkDefinition.Version,
                run.EvaluationProfile.Name, run.ModelName, run.Status, run.TotalQuestions, run.CompletedQuestions,
                Average(run.Results, result => result.ContextPrecision), Average(run.Results, result => result.ContextRecall),
                Average(run.Results, result => result.Faithfulness), Average(run.Results, result => result.AnswerRelevancy),
                run.CreatedAt, run.CompletedAt)).ToList();
        }

        public async Task<EvaluationRunDetailDto?> GetRunDetailAsync(Guid runId, CancellationToken cancellationToken = default)
        {
            var run = await _unitOfWork.Repository<EvaluationRun>().GetQueryable().AsNoTracking()
                .Where(r => r.Id == runId)
                .Include(r => r.BenchmarkDefinition)
                .Include(r => r.EvaluationProfile)
                .Include(r => r.Results).ThenInclude(res => res.BenchmarkQuestion)
                .FirstOrDefaultAsync(cancellationToken);

            if (run == null) return null;

            var resultDtos = run.Results
                .OrderBy(r => r.BenchmarkQuestion.SortOrder)
                .Select(r => new EvaluationResultDetailDto(
                    r.Id, r.BenchmarkQuestion.SortOrder, r.BenchmarkQuestion.Question, r.BenchmarkQuestion.ReferenceAnswer,
                    r.BenchmarkQuestion.IsHoldout, r.Answer, r.Status, r.ErrorMessage, r.ContextPrecision, r.ContextRecall,
                    r.Faithfulness, r.AnswerRelevancy, r.RetrievalLatencyMs, r.GenerationLatencyMs))
                .ToList();

            return new EvaluationRunDetailDto(run.Id, run.BenchmarkDefinition.Name, run.BenchmarkDefinition.Version,
                run.EvaluationProfile.Name, run.ModelName, run.Status, run.TotalQuestions, run.CompletedQuestions,
                run.CreatedAt, run.CompletedAt, resultDtos);
        }

        public async Task<EvaluationImportResult> ImportRunnerReportAsync(Guid datasetId, Guid userId, string reportJson, CancellationToken cancellationToken = default)
        {
            using var document = JsonDocument.Parse(reportJson);
            var root = document.RootElement;
            var benchmarkName = RequiredString(root, "benchmark_name");
            var benchmarkVersion = RequiredString(root, "benchmark_version");
            var profileSlug = RequiredString(root, "profile_id");
            var modelName = RequiredString(root, "model");
            var results = root.GetProperty("results");
            if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
                throw new InvalidOperationException("Runner report must contain at least one result.");
            if (root.TryGetProperty("dataset_id", out var reportDatasetId) && Guid.TryParse(reportDatasetId.GetString(), out var parsedId) && parsedId != datasetId)
                throw new InvalidOperationException("The uploaded report belongs to a different dataset.");

            var definitions = _unitOfWork.Repository<BenchmarkDefinition>();
            var questions = _unitOfWork.Repository<BenchmarkQuestion>();
            var profiles = _unitOfWork.Repository<EvaluationProfile>();
            var runs = _unitOfWork.Repository<EvaluationRun>();
            var evaluationResults = _unitOfWork.Repository<EvaluationResult>();
            var evidence = _unitOfWork.Repository<EvaluationEvidence>();

            var definition = await definitions.GetQueryable()
                .Include(item => item.Questions)
                .FirstOrDefaultAsync(item => item.DatasetId == datasetId && item.Name == benchmarkName && item.Version == benchmarkVersion, cancellationToken);
            if (definition == null)
            {
                definition = new BenchmarkDefinition { Id = Guid.NewGuid(), DatasetId = datasetId, CreatedByUserId = userId, Name = benchmarkName, Version = benchmarkVersion,
                    Description = "Imported from the auditable Python RAG benchmark runner.", IsLocked = true };
                await definitions.AddAsync(definition, cancellationToken);
            }

            var profile = await profiles.GetQueryable().FirstOrDefaultAsync(item => item.Slug == profileSlug, cancellationToken);
            if (profile == null)
            {
                profile = new EvaluationProfile { Id = Guid.NewGuid(), Slug = profileSlug, Name = profileSlug, EmbeddingModel = "Configured by RAG API profile", ChunkingStrategy = "external-index", TopK = 10 };
                await profiles.AddAsync(profile, cancellationToken);
            }

            var run = new EvaluationRun { Id = Guid.NewGuid(), DatasetId = datasetId, BenchmarkDefinitionId = definition.Id, EvaluationProfileId = profile.Id,
                RunByUserId = userId, ProviderKey = "Groq", ModelName = modelName, PromptVersion = root.TryGetProperty("prompt_version", out var prompt) ? prompt.GetString() ?? "rag-v1" : "rag-v1",
                Status = "Running", TotalQuestions = results.GetArrayLength(), StartedAt = DateTime.UtcNow };
            await runs.AddAsync(run, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var knownQuestions = definition.Questions.ToDictionary(question => question.SortOrder);
            var imported = 0;
            var failures = 0;
            foreach (var item in results.EnumerateArray())
            {
                var sortOrder = item.GetProperty("sort_order").GetInt32();
                if (!knownQuestions.TryGetValue(sortOrder, out var question))
                {
                    question = new BenchmarkQuestion { Id = Guid.NewGuid(), BenchmarkDefinitionId = definition.Id, SortOrder = sortOrder,
                        Question = RequiredString(item, "question"), ReferenceAnswer = RequiredString(item, "reference_answer"),
                        RelevantChunkIdsJson = item.TryGetProperty("expected_chunk_ids", out var expected) ? expected.GetRawText() : "[]",
                        IsHoldout = item.TryGetProperty("is_holdout", out var holdout) && holdout.GetBoolean() };
                    await questions.AddAsync(question, cancellationToken);
                    knownQuestions[sortOrder] = question;
                }
                var status = item.TryGetProperty("status", out var statusNode) ? statusNode.GetString() ?? "Failed" : "Failed";
                var result = new EvaluationResult { Id = Guid.NewGuid(), EvaluationRunId = run.Id, BenchmarkQuestionId = question.Id, Status = status,
                    Answer = OptionalString(item, "answer") ?? string.Empty, ErrorMessage = OptionalString(item, "error"),
                    RetrievalLatencyMs = OptionalLong(item, "retrieval_latency_ms"), GenerationLatencyMs = OptionalLong(item, "generation_latency_ms"),
                    InputTokens = OptionalInt(item, "input_tokens"), OutputTokens = OptionalInt(item, "output_tokens"), TotalTokens = OptionalInt(item, "total_tokens"),
                    ContextPrecision = OptionalDouble(item, "context_precision"), ContextRecall = OptionalDouble(item, "context_recall"),
                    Faithfulness = OptionalDouble(item, "faithfulness"), AnswerRelevancy = OptionalDouble(item, "answer_relevancy") };
                await evaluationResults.AddAsync(result, cancellationToken);
                if (item.TryGetProperty("retrieved_chunk_ids", out var retrieved) && retrieved.ValueKind == JsonValueKind.Array)
                {
                    var rank = 0;
                    foreach (var chunk in retrieved.EnumerateArray())
                    {
                        rank++;
                        await evidence.AddAsync(new EvaluationEvidence { Id = Guid.NewGuid(), EvaluationResultId = result.Id, Rank = rank,
                            ChunkId = Guid.TryParse(chunk.GetString(), out var chunkId) ? chunkId : null, Content = string.Empty,
                            MetadataJson = JsonSerializer.Serialize(new { chunkId = chunk.GetString() }) }, cancellationToken);
                    }
                }
                if (status == "Completed") imported++; else failures++;
            }
            run.CompletedQuestions = imported;
            run.Status = failures == 0 ? "Completed" : imported == 0 ? "Failed" : "CompletedWithErrors";
            run.ErrorMessage = failures == 0 ? null : $"{failures} question(s) failed in the imported runner report.";
            run.CompletedAt = DateTime.UtcNow;
            runs.Update(run);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new EvaluationImportResult(run.Id, imported, failures);
        }

        private static double? Average(IEnumerable<EvaluationResult> values, Func<EvaluationResult, double?> selector)
        {
            var metrics = values.Select(selector).Where(value => value.HasValue).Select(value => value!.Value).ToList();
            return metrics.Count == 0 ? null : Math.Round(metrics.Average(), 4);
        }
        private static string RequiredString(JsonElement item, string name) => OptionalString(item, name) is { Length: > 0 } value ? value : throw new InvalidOperationException($"Runner report is missing '{name}'.");
        private static string? OptionalString(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;
        private static int OptionalInt(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;
        private static long OptionalLong(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : 0;
        private static double? OptionalDouble(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null && value.TryGetDouble(out var result) ? result : null;
    }
}
