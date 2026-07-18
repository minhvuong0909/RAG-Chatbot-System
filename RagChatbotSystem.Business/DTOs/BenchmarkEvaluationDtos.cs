using System;
using System.Collections.Generic;

namespace RagChatbotSystem.Business.DTOs
{
    public sealed record EvaluationProfileDto(Guid Id, string Name, string Slug, string ChunkingStrategy,
        int ChunkSize, int ChunkOverlap, string EmbeddingModel, int TopK, bool IsEnabled);

    public sealed record EvaluationRunSummaryDto(Guid Id, string BenchmarkName, string BenchmarkVersion,
        string ProfileName, string ModelName, string Status, int TotalQuestions, int CompletedQuestions,
        double? ContextPrecision, double? ContextRecall, double? Faithfulness, double? AnswerRelevancy,
        DateTime CreatedAt, DateTime? CompletedAt);

    public sealed record EvaluationImportResult(Guid RunId, int ImportedQuestions, int FailedQuestions);

    public sealed record EvaluationRunDetailDto(
        Guid Id,
        string BenchmarkName,
        string BenchmarkVersion,
        string ProfileName,
        string ModelName,
        string Status,
        int TotalQuestions,
        int CompletedQuestions,
        DateTime CreatedAt,
        DateTime? CompletedAt,
        IReadOnlyList<EvaluationResultDetailDto> Results
    );

    public sealed record EvaluationResultDetailDto(
        Guid Id,
        int SortOrder,
        string Question,
        string ReferenceAnswer,
        bool IsHoldout,
        string GeneratedAnswer,
        string Status,
        string? ErrorMessage,
        double? ContextPrecision,
        double? ContextRecall,
        double? Faithfulness,
        double? AnswerRelevancy,
        long RetrievalLatencyMs,
        long GenerationLatencyMs
    );
}
