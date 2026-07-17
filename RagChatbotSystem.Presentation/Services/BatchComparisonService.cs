using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Services
{
    public class BatchComparisonService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BatchComparisonService> _logger;
        
        private readonly object _lock = new();
        private BatchJobStatus _currentJob = new();

        public BatchComparisonService(IServiceScopeFactory scopeFactory, ILogger<BatchComparisonService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public BatchJobStatus GetStatus()
        {
            lock (_lock)
            {
                return new BatchJobStatus
                {
                    IsRunning = _currentJob.IsRunning,
                    TotalQuestions = _currentJob.TotalQuestions,
                    ProcessedQuestions = _currentJob.ProcessedQuestions,
                    FailedQuestions = _currentJob.FailedQuestions,
                    CurrentQuestion = _currentJob.CurrentQuestion
                };
            }
        }

        public bool StartBatch(Guid datasetId, List<string> questions, List<string> providers, Guid userId)
        {
            lock (_lock)
            {
                if (_currentJob.IsRunning) return false;

                _currentJob = new BatchJobStatus
                {
                    IsRunning = true,
                    TotalQuestions = questions.Count,
                    ProcessedQuestions = 0,
                    FailedQuestions = 0,
                    CurrentQuestion = "Bắt đầu khởi chạy..."
                };
            }

            _ = Task.Run(async () =>
            {
                for (int i = 0; i < questions.Count; i++)
                {
                    var question = questions[i].Trim();
                    if (string.IsNullOrWhiteSpace(question)) continue;

                    lock (_lock)
                    {
                        _currentJob.CurrentQuestion = question;
                    }

                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var comparisonService = scope.ServiceProvider.GetRequiredService<IModelComparisonService>();
                        
                        // Execute the comparison (retrieves, calls LLMs, scores, and persists to DB)
                        await comparisonService.CompareAsync(datasetId, question, providers, userId);

                        lock (_lock)
                        {
                            _currentJob.ProcessedQuestions++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing batch question: {Question}", question);
                        lock (_lock)
                        {
                            _currentJob.ProcessedQuestions++;
                            _currentJob.FailedQuestions++;
                        }
                    }

                    // A brief 2-second sleep between questions to avoid hitting rate limits (e.g. 429 Too Many Requests)
                    await Task.Delay(2000);
                }

                lock (_lock)
                {
                    _currentJob.IsRunning = false;
                    _currentJob.CurrentQuestion = "Hoàn thành";
                }
            });

            return true;
        }
    }

    public class BatchJobStatus
    {
        public bool IsRunning { get; set; }
        public int TotalQuestions { get; set; }
        public int ProcessedQuestions { get; set; }
        public int FailedQuestions { get; set; }
        public string CurrentQuestion { get; set; } = string.Empty;
    }
}
