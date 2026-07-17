using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.DataAccess.Data;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Tests;

public class StatisticsServiceTests
{
    [Fact]
    public async Task GetTopSubjectsByQuestionCountAsync_ReturnsSubjectsOrderedByQuestionCount()
    {
        await using var context = CreateContext();
        var (subjectA, subjectB, studentA, studentB) = SeedUsageData(context);

        var service = new StatisticsService(new UnitOfWork(context));

        var topSubjects = await service.GetTopSubjectsByQuestionCountAsync();

        Assert.Equal(2, topSubjects.Count);
        Assert.Equal(subjectB, topSubjects[0].DatasetId);
        Assert.Equal("PRN222", topSubjects[0].DatasetName);
        Assert.Equal(8, topSubjects[0].TotalQueriesCount);
        Assert.Equal(2, topSubjects[0].ActiveUsersCount);

        Assert.Equal(subjectA, topSubjects[1].DatasetId);
        Assert.Equal(3, topSubjects[1].TotalQueriesCount);
        Assert.Contains(topSubjects[0].DatasetId, new[] { subjectA, subjectB });
        Assert.NotEqual(studentA, studentB);
    }

    [Fact]
    public async Task StatisticsMethods_ApplyDatasetScope_ForTeacherAssignedSubjects()
    {
        await using var context = CreateContext();
        var (subjectA, subjectB, _, _) = SeedUsageData(context);
        var service = new StatisticsService(new UnitOfWork(context));
        var teacherScope = new[] { subjectA };

        var summary = await service.GetTokenUsageSummaryAsync(teacherScope);
        var daily = await service.GetDailyTokenUsageAsync(datasetIds: teacherScope);
        var leaderboard = await service.GetUserLeaderboardAsync(datasetIds: teacherScope);
        var topSubjects = await service.GetTopSubjectsByQuestionCountAsync(datasetIds: teacherScope);

        Assert.Equal(300, summary.TotalTokensUsed);
        Assert.Equal(3, summary.TotalQueriesCount);
        Assert.All(topSubjects, subject => Assert.Equal(subjectA, subject.DatasetId));
        Assert.DoesNotContain(topSubjects, subject => subject.DatasetId == subjectB);
        Assert.Equal(3, daily.Sum(d => d.QueryCount));
        Assert.Single(leaderboard);
        Assert.Equal(3, leaderboard[0].TotalQueriesCount);
    }

    [Fact]
    public async Task GetStudentEngagementByQuestionCountAsync_ReturnsOnlyStudentsInsideDatasetScope()
    {
        await using var context = CreateContext();
        var (subjectA, subjectB, studentA, studentB) = SeedUsageData(context);
        var service = new StatisticsService(new UnitOfWork(context));

        var engagement = await service.GetStudentEngagementByQuestionCountAsync(datasetIds: new[] { subjectB });

        Assert.Equal(2, engagement.Count);
        Assert.Equal(studentA, engagement[0].UserId);
        Assert.Equal(5, engagement[0].TotalQueriesCount);
        Assert.Equal(studentB, engagement[1].UserId);
        Assert.Equal(3, engagement[1].TotalQueriesCount);
        Assert.DoesNotContain(engagement, student => student.TotalQueriesCount == 8);

        var scopedToSubjectA = await service.GetStudentEngagementByQuestionCountAsync(datasetIds: new[] { subjectA });
        Assert.Single(scopedToSubjectA);
        Assert.Equal(studentA, scopedToSubjectA[0].UserId);
        Assert.Equal(3, scopedToSubjectA[0].TotalQueriesCount);
    }

    [Fact]
    public async Task LearningActivityMethods_ExcludeTeacherUsage()
    {
        await using var context = CreateContext();
        var (subjectA, _, _, _) = SeedUsageData(context);
        var teacher = context.Users.Single(user => user.Role == "Teacher");
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        context.UserTokenUsages.Add(new UserTokenUsage
        {
            Id = Guid.NewGuid(),
            UserId = teacher.UserId,
            DatasetId = subjectA,
            Date = today,
            TokenCount = 999,
            QueryCount = 20
        });
        await context.SaveChangesAsync();

        var service = new StatisticsService(new UnitOfWork(context));

        var activeStudents = await service.GetActiveStudentCountAsync(new[] { subjectA });
        var subjectActivity = await service.GetSubjectLearningActivityByQuestionCountAsync(datasetIds: new[] { subjectA });

        Assert.Equal(1, activeStudents);
        Assert.Single(subjectActivity);
        Assert.Equal(3, subjectActivity[0].TotalQueriesCount);
        Assert.Equal(1, subjectActivity[0].ActiveUsersCount);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TestAppDbContext(options);
    }

    private static (Guid SubjectA, Guid SubjectB, Guid StudentA, Guid StudentB) SeedUsageData(AppDbContext context)
    {
        var teacherId = Guid.NewGuid();
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();
        var subjectA = Guid.NewGuid();
        var subjectB = Guid.NewGuid();
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        context.Users.AddRange(
            NewUser(teacherId, "teacher@example.edu", "teacher", "Teacher"),
            NewUser(studentA, "student-a@example.edu", "student-a", "Student"),
            NewUser(studentB, "student-b@example.edu", "student-b", "Student"));

        context.Datasets.AddRange(
            new Dataset
            {
                DatasetId = subjectA,
                Name = "EXE101",
                CreatedBy = teacherId,
                IsApproved = true,
                IsPublic = true
            },
            new Dataset
            {
                DatasetId = subjectB,
                Name = "PRN222",
                CreatedBy = teacherId,
                IsApproved = true,
                IsPublic = true
            });

        context.UserTokenUsages.AddRange(
            new UserTokenUsage
            {
                Id = Guid.NewGuid(),
                UserId = studentA,
                DatasetId = subjectA,
                Date = today,
                TokenCount = 300,
                QueryCount = 3
            },
            new UserTokenUsage
            {
                Id = Guid.NewGuid(),
                UserId = studentA,
                DatasetId = subjectB,
                Date = today,
                TokenCount = 500,
                QueryCount = 5
            },
            new UserTokenUsage
            {
                Id = Guid.NewGuid(),
                UserId = studentB,
                DatasetId = subjectB,
                Date = today,
                TokenCount = 250,
                QueryCount = 3
            });

        context.SaveChanges();
        return (subjectA, subjectB, studentA, studentB);
    }

    private static User NewUser(Guid userId, string email, string username, string role)
    {
        return new User
        {
            UserId = userId,
            FullName = username,
            Email = email,
            Username = username,
            PasswordHash = "hash",
            Role = role,
            IsApproved = true
        };
    }

    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<VectorRecord>().Ignore(v => v.Embedding);
        }
    }
}
