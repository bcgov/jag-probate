using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Probate.Api.Services;
using Probate.Db.Models;

namespace Probate.Api.Tests.Services;

public class SubmissionServiceTests
{
    [Fact]
    public async Task FinalizeSubmissionAsync_StoresCompiledData_AndMarksSubmitted()
    {
        await using var db = CreateDbContext();
        var submission = CreateSubmission();
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.FinalizeSubmissionAsync(submission.PublicId, "{\"a\":1}");

        var stored = await db.Submissions.SingleAsync();
        Assert.Equal("{\"a\":1}", stored.SubmissionData);
        Assert.Equal("submitted", stored.Status);
        Assert.NotNull(stored.LastFiledAt);
        Assert.Equal("submitted", result.Status);
    }

    [Fact]
    public async Task FinalizeSubmissionAsync_UnknownId_Throws()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.FinalizeSubmissionAsync(Guid.NewGuid(), "{}")
        );
    }

    [Fact]
    public async Task FinalizeSubmissionAsync_SoftDeletedSubmission_Throws()
    {
        await using var db = CreateDbContext();
        var submission = CreateSubmission();
        submission.DeletedAt = DateTime.UtcNow;
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.FinalizeSubmissionAsync(submission.PublicId, "{}")
        );
    }

    [Fact]
    public async Task DeleteSubmissionAsync_SetsDeletedAt_AndIsIdempotent()
    {
        await using var db = CreateDbContext();
        var submission = CreateSubmission();
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.DeleteSubmissionAsync(submission.PublicId);
        var firstDeletedAt = (await db.Submissions.SingleAsync()).DeletedAt;
        await service.DeleteSubmissionAsync(submission.PublicId);

        Assert.NotNull(firstDeletedAt);
        Assert.Equal(firstDeletedAt, (await db.Submissions.SingleAsync()).DeletedAt);
    }

    [Fact]
    public async Task DeleteSubmissionAsync_UnknownId_Throws()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteSubmissionAsync(Guid.NewGuid())
        );
    }

    private static ProbateDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProbateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ProbateDbContext(options);
    }

    private static SubmissionService CreateService(ProbateDbContext db) =>
        new(db, NullLogger<SubmissionService>.Instance);

    private static Submission CreateSubmission() =>
        new()
        {
            PublicId = Guid.NewGuid(),
            ApplicantName = string.Empty,
            CreatedBy = "test-user",
            Status = "draft",
        };
}
