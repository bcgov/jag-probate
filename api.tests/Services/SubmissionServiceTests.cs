using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Probate.Api.Infrastructure.Authentication;
using Probate.Api.Services;
using Probate.Db.Models;

namespace Probate.Api.Tests.Services;

public class SubmissionServiceTests
{
    private static readonly SubmissionOwner Owner = new("test-user-id", "test-user");

    [Fact]
    public async Task FinalizeSubmissionAsync_StoresCompiledData_AndMarksReadyToPrint()
    {
        await using var db = CreateDbContext();
        var submission = CreateSubmission();
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.FinalizeSubmissionAsync(submission.Id, Owner, "{\"a\":1}");

        var stored = await db.Submissions.SingleAsync();
        Assert.Equal("{\"a\":1}", stored.SubmissionData);
        Assert.Equal(SubmissionStatus.ReadyToPrint, stored.Status);
        // Not eFiled yet – the application must stay editable/deletable.
        Assert.Null(stored.LastFiledAt);
        Assert.Equal(SubmissionStatus.ReadyToPrint, result.Status);
    }

    [Fact]
    public async Task FinalizeSubmissionAsync_UnknownId_Throws()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.FinalizeSubmissionAsync(-1, Owner, "{}")
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
            service.FinalizeSubmissionAsync(submission.Id, Owner, "{}")
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

        await service.DeleteSubmissionAsync(submission.PublicId, Owner);
        var firstDeletedAt = (await db.Submissions.SingleAsync()).DeletedAt;
        await service.DeleteSubmissionAsync(submission.PublicId, Owner);

        Assert.NotNull(firstDeletedAt);
        Assert.Equal(firstDeletedAt, (await db.Submissions.SingleAsync()).DeletedAt);
    }

    [Fact]
    public async Task DeleteSubmissionAsync_UnknownId_Throws()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteSubmissionAsync(Guid.NewGuid(), Owner)
        );
    }

    private static ProbateDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProbateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ProbateDbContext(options);
    }

    private static SubmissionService CreateService(ProbateDbContext db) => new(db);

    private static Submission CreateSubmission() =>
        new()
        {
            PublicId = Guid.NewGuid(),
            ApplicantName = string.Empty,
            CreatedBy = "test-user",
            Status = "draft",
        };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("TEST-USER")]
    public async Task Listing_UsesExactUserIdOrNonblankUnclaimedLegacyUsername(
        string? legacyUsername
    )
    {
        await using var db = CreateDbContext();
        var current = CreateSubmission();
        current.CreatedByUserId = Owner.UserId;
        current.CreatedBy = "old-username";
        var legacy = CreateSubmission();
        var foreignId = CreateSubmission();
        foreignId.CreatedByUserId = "other-id";
        var foreignName = CreateSubmission();
        foreignName.CreatedBy = "other-name";
        var deleted = CreateSubmission();
        deleted.CreatedByUserId = Owner.UserId;
        deleted.DeletedAt = DateTime.UtcNow;
        var blank = CreateSubmission();
        blank.CreatedBy = "";
        var missing = CreateSubmission();
        missing.CreatedBy = null;
        db.Submissions.AddRange(current, legacy, foreignId, foreignName, deleted, blank, missing);
        await db.SaveChangesAsync();

        var rows = await CreateService(db)
            .GetSubmissionsByUserAsync(new SubmissionOwner(Owner.UserId, legacyUsername));
        Assert.Equal(current.PublicId, Assert.Single(rows).PublicId);
        Assert.Null(legacy.CreatedByUserId);
    }

    [Fact]
    public async Task Listing_ReturnsIdAndLegacyRows_ClaimsOnlyMatchingUnclaimedRows()
    {
        await using var db = CreateDbContext();
        var current = CreateSubmission();
        current.CreatedByUserId = Owner.UserId;
        current.CreatedBy = "renamed";
        var legacy = CreateSubmission();
        var foreign = CreateSubmission();
        foreign.CreatedByUserId = "other-id";
        var blankId = CreateSubmission();
        blankId.CreatedByUserId = "";
        var foreignUsername = CreateSubmission();
        foreignUsername.CreatedBy = "other-username";
        var deleted = CreateSubmission();
        deleted.DeletedAt = DateTime.UtcNow;
        db.Submissions.AddRange(current, legacy, foreign, blankId, foreignUsername, deleted);
        await db.SaveChangesAsync();

        var rows = await CreateService(db).GetSubmissionsByUserAsync(Owner);
        Assert.Equal(
            new[] { current.PublicId, legacy.PublicId }.Order(),
            rows.Select(s => s.PublicId).Order()
        );
        Assert.Equal(Owner.UserId, legacy.CreatedByUserId);
        Assert.Equal("test-user", legacy.CreatedBy);
        Assert.Equal("other-id", foreign.CreatedByUserId);
        Assert.Null(deleted.CreatedByUserId);

        var denied = await CreateService(db)
            .GetSubmissionByIdAsync(legacy.PublicId, new SubmissionOwner("other-id", "test-user"));
        Assert.Null(denied);
        Assert.Equal(Owner.UserId, legacy.CreatedByUserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentLegacyClaims_OnlyWinningIdCanProceed(bool sameUser)
    {
        var options = new DbContextOptionsBuilder<ProbateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var row = CreateSubmission();
        await using (var seed = new ProbateDbContext(options))
        {
            seed.Submissions.Add(row);
            await seed.SaveChangesAsync();
        }
        var pause = new PauseClaimInterceptor();
        await using var first = new ProbateDbContext(
            new DbContextOptionsBuilder<ProbateDbContext>(options).AddInterceptors(pause).Options
        );
        var pendingRead = CreateService(first).GetSubmissionByIdAsync(row.PublicId, Owner);
        await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var winningId = sameUser ? Owner.UserId : "winning-id";
        try
        {
            await using var second = new ProbateDbContext(options);
            Assert.NotNull(
                await CreateService(second)
                    .GetSubmissionByIdAsync(
                        row.PublicId,
                        new SubmissionOwner(winningId, Owner.LegacyUsername)
                    )
            );
        }
        finally
        {
            pause.Resume.TrySetResult();
        }
        var result = await pendingRead;
        Assert.Equal(sameUser, result != null);
        await using var stored = new ProbateDbContext(options);
        var claimed = await stored.Submissions.SingleAsync();
        Assert.Equal(winningId, claimed.CreatedByUserId);
        Assert.Equal(Owner.LegacyUsername, claimed.CreatedBy);
    }

    [Fact]
    public async Task ConcurrentListClaim_LoserListExcludesRowClaimedByAnotherId()
    {
        var options = new DbContextOptionsBuilder<ProbateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var row = CreateSubmission();
        await using (var seed = new ProbateDbContext(options))
        {
            seed.Submissions.Add(row);
            await seed.SaveChangesAsync();
        }
        var pause = new PauseClaimInterceptor();
        await using var first = new ProbateDbContext(
            new DbContextOptionsBuilder<ProbateDbContext>(options).AddInterceptors(pause).Options
        );
        var pendingList = CreateService(first).GetSubmissionsByUserAsync(Owner);
        await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var second = new ProbateDbContext(options);
            Assert.Single(
                await CreateService(second)
                    .GetSubmissionsByUserAsync(
                        new SubmissionOwner("winning-id", Owner.LegacyUsername)
                    )
            );
        }
        finally
        {
            pause.Resume.TrySetResult();
        }
        Assert.Empty(await pendingList);
        await using var stored = new ProbateDbContext(options);
        Assert.Equal("winning-id", (await stored.Submissions.SingleAsync()).CreatedByUserId);
    }

    internal sealed class PauseClaimInterceptor : SaveChangesInterceptor
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            Started.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
            return result;
        }
    }
}
