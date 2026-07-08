using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;
using Xunit;

namespace RagChatbotSystem.Tests
{
    public class TokenUsageTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGenericRepository<UserTokenUsage>> _mockUsageRepository;
        private readonly Mock<IGenericRepository<SystemSetting>> _mockSettingRepository;
        private readonly TokenUsageService _tokenUsageService;
        private readonly Guid _userId;
        private readonly Guid _datasetId;
        private readonly List<UserTokenUsage> _usages;
        private readonly List<SystemSetting> _settings;

        public TokenUsageTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUsageRepository = new Mock<IGenericRepository<UserTokenUsage>>();
            _mockSettingRepository = new Mock<IGenericRepository<SystemSetting>>();

            _usages = new List<UserTokenUsage>();
            _settings = new List<SystemSetting>();

            // Setup repository behavior
            _mockUsageRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<UserTokenUsage, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<UserTokenUsage, bool>> predicate, CancellationToken ct) => 
                {
                    var compiled = predicate.Compile();
                    return _usages.Where(compiled).ToList();
                });

            _mockUsageRepository.Setup(r => r.AddAsync(It.IsAny<UserTokenUsage>(), It.IsAny<CancellationToken>()))
                .Callback<UserTokenUsage, CancellationToken>((usage, ct) => _usages.Add(usage))
                .Returns(Task.CompletedTask);

            _mockUsageRepository.Setup(r => r.Update(It.IsAny<UserTokenUsage>()))
                .Callback<UserTokenUsage>(usage => 
                {
                    var existing = _usages.FirstOrDefault(u => u.Id == usage.Id);
                    if (existing != null)
                    {
                        existing.TokenCount = usage.TokenCount;
                        existing.QueryCount = usage.QueryCount;
                    }
                });

            _mockSettingRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _settings);

            // Bind repository getters to unit of work
            _mockUnitOfWork.Setup(u => u.Repository<UserTokenUsage>()).Returns(_mockUsageRepository.Object);
            _mockUnitOfWork.Setup(u => u.Repository<SystemSetting>()).Returns(_mockSettingRepository.Object);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            _tokenUsageService = new TokenUsageService(_mockUnitOfWork.Object);
            _userId = Guid.NewGuid();
            _datasetId = Guid.NewGuid();
        }

        [Fact]
        public async Task RecordUsageAsync_SavesNewDailyUsageCorrectly()
        {
            // Act
            await _tokenUsageService.RecordUsageAsync(_userId, _datasetId, 1000);

            // Assert
            Assert.Single(_usages);
            Assert.Equal(_datasetId, _usages[0].DatasetId);
            Assert.Equal(1000, _usages[0].TokenCount);
            Assert.Equal(1, _usages[0].QueryCount);
        }

        [Fact]
        public async Task RecordUsageAsync_AccumulatesDailyUsageCorrectly()
        {
            // Act
            await _tokenUsageService.RecordUsageAsync(_userId, _datasetId, 1000);
            await _tokenUsageService.RecordUsageAsync(_userId, _datasetId, 1500);

            // Assert
            Assert.Single(_usages);
            Assert.Equal(2500, _usages[0].TokenCount);
            Assert.Equal(2, _usages[0].QueryCount);
        }

        [Fact]
        public async Task IsLimitExceededAsync_ReturnsFalse_WhenNoUsageFound()
        {
            // Act
            var exceeded = await _tokenUsageService.IsLimitExceededAsync(_userId);

            // Assert
            Assert.False(exceeded);
        }

        [Fact]
        public async Task IsLimitExceededAsync_ReturnsFalse_WhenUsageIsUnderLimit()
        {
            // Arrange
            _settings.Add(new SystemSetting { DailyTokenLimit = 5000 });
            await _tokenUsageService.RecordUsageAsync(_userId, _datasetId, 4000);

            // Act
            var exceeded = await _tokenUsageService.IsLimitExceededAsync(_userId);

            // Assert
            Assert.False(exceeded);
        }

        [Fact]
        public async Task IsLimitExceededAsync_ReturnsTrue_WhenUsageIsOverOrEqualLimit()
        {
            // Arrange
            _settings.Add(new SystemSetting { DailyTokenLimit = 5000 });
            await _tokenUsageService.RecordUsageAsync(_userId, _datasetId, 5000);

            // Act
            var exceeded = await _tokenUsageService.IsLimitExceededAsync(_userId);

            // Assert
            Assert.True(exceeded);
        }
    }
}
