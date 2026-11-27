using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Probate.Api.Infrastructure.Authentication;
using Probate.Api.Services;
using Probate.Api.Tests.Services;
using Probate.Db.Models;

namespace Probate.Api.Tests.Integration;

public class SubmissionOwnerMigrationTests
{
    private const string PreviousMigration = "20261029220356_DropChefsSubmissionIdFromSubmissions";

    [OwnershipPostgresFact]
    public async Task Upgrade_PreservesLegacyData_StoresIdOnlyDraft_AndBlocksUnsafeRollback()
    {
        await using var schema = await TestSchema.CreateAsync();
        await using var db = schema.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        var publicId = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync(
            $$"""
            INSERT INTO submissions (public_id, applicant_name, created_by, status, submission_data, created_at)
            VALUES ({{publicId}}, 'Legacy', 'alice', 'draft', '{}', now())
            """
        );
        await migrator.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        var row = await db.Submissions.SingleAsync();
        Assert.Equal("alice", row.CreatedBy);
        Assert.Null(row.CreatedByUserId);
        Assert.Equal("{}", row.SubmissionData);

        var service = new SubmissionService(db);
        var draft = await service.CreateDraftSubmissionAsync(
            new SubmissionOwner("alice-id", "alice")
        );
        var stored = await db.Submissions.SingleAsync(s => s.PublicId == draft.PublicId);
        Assert.Null(stored.CreatedBy);
        Assert.Equal("alice-id", stored.CreatedByUserId);
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            migrator.MigrateAsync(PreviousMigration)
        );
        db.ChangeTracker.Clear();
        Assert.Equal(
            "alice-id",
            (await db.Submissions.SingleAsync(s => s.PublicId == draft.PublicId)).CreatedByUserId
        );
        Assert.Equal(
            "alice",
            (await db.Submissions.SingleAsync(s => s.PublicId == publicId)).CreatedBy
        );

        var script = migrator.GenerateScript(PreviousMigration);
        Assert.Contains("ALTER TABLE submissions ALTER COLUMN created_by DROP NOT NULL", script);
        Assert.Contains("ADD created_by_user_id character varying(255)", script);
        Assert.Contains("CREATE INDEX ix_submissions_created_by_user_id", script);
        Assert.DoesNotContain("UPDATE submissions", script);
        Assert.DoesNotContain("users", script);
        Assert.DoesNotContain("CREATE UNIQUE INDEX", script);
    }

    [OwnershipPostgresFact]
    public async Task ClaimSql_UsesExactGuardAndOwnershipPredicate_ConcurrentLoserCannotAccess()
    {
        await using var schema = await TestSchema.CreateAsync();
        await using (var db = schema.CreateContext())
        {
            await db.Database.MigrateAsync();
            db.Submissions.Add(
                new Submission
                {
                    ApplicantName = "Legacy",
                    CreatedBy = "alice",
                    Status = "draft",
                }
            );
            await db.SaveChangesAsync();
        }

        var pause = new SubmissionServiceTests.PauseClaimInterceptor();
        var capture = new ClaimCommandInterceptor();
        await using var first = schema.CreateContext(pause, capture);
        var id = await first.Submissions.Select(s => s.PublicId).SingleAsync();
        var owner = new SubmissionOwner("first-id", "alice");
        var query = first
            .Submissions.Where(SubmissionOwnership.OwnedBy(owner))
            .Where(s => s.PublicId == id && s.DeletedAt == null)
            .ToQueryString();
        Assert.Contains("created_by_user_id", query);
        Assert.Contains("IS NULL", query);
        Assert.Contains("created_by", query);
        Assert.Contains(" OR ", query);
        Assert.Contains("deleted_at IS NULL", query);
        var idOnlyQuery = first
            .Submissions.Where(SubmissionOwnership.OwnedBy(new SubmissionOwner("first-id")))
            .ToQueryString();
        Assert.DoesNotContain(" OR ", idOnlyQuery);

        var losingRead = new SubmissionService(first).GetSubmissionByIdAsync(id, owner);
        await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var second = schema.CreateContext();
            Assert.NotNull(
                await new SubmissionService(second).GetSubmissionByIdAsync(
                    id,
                    new SubmissionOwner("winner-id", "alice")
                )
            );
        }
        finally
        {
            pause.Resume.TrySetResult();
        }
        Assert.Null(await losingRead);
        var update = Assert.Single(capture.Updates);
        var where = update[(update.IndexOf("WHERE", StringComparison.Ordinal))..];
        Assert.Contains("created_by_user_id IS NULL", where);
        Assert.Contains("created_by =", where);
        Assert.Contains("id =", where);
        Assert.Equal("first-id", capture.OwnerUserId);
        Assert.Equal("alice", capture.LegacyUsername);
        await using var stored = schema.CreateContext();
        var row = await stored.Submissions.SingleAsync();
        Assert.Equal("winner-id", row.CreatedByUserId);
        Assert.Equal("alice", row.CreatedBy);
        Assert.Null(
            await new SubmissionService(stored).GetSubmissionByIdAsync(
                id,
                new SubmissionOwner("different-id", "alice")
            )
        );
    }

    [OwnershipPostgresFact]
    public async Task ListAfterConcurrentPartialClaim_ClaimsRemainingRowsAndOmitsLostRow()
    {
        await using var schema = await TestSchema.CreateAsync();
        await using (var db = schema.CreateContext())
        {
            await db.Database.MigrateAsync();
            db.Submissions.AddRange(
                new Submission
                {
                    PublicId = Guid.NewGuid(),
                    ApplicantName = "Claimed elsewhere",
                    CreatedBy = "alice",
                    Status = "draft",
                },
                new Submission
                {
                    PublicId = Guid.NewGuid(),
                    ApplicantName = "Still available",
                    CreatedBy = "alice",
                    Status = "draft",
                }
            );
            await db.SaveChangesAsync();
        }

        var pause = new SubmissionServiceTests.PauseClaimInterceptor();
        await using var first = schema.CreateContext(pause);
        var publicIds = await first
            .Submissions.OrderBy(s => s.Id)
            .Select(s => s.PublicId)
            .ToListAsync();
        var pendingList = new SubmissionService(first).GetSubmissionsByUserAsync(
            new SubmissionOwner("first-id", "alice")
        );
        await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var second = schema.CreateContext();
            Assert.NotNull(
                await new SubmissionService(second).GetSubmissionByIdAsync(
                    publicIds[0],
                    new SubmissionOwner("winner-id", "alice")
                )
            );
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        var listed = await pendingList;
        var remaining = Assert.Single(listed);
        Assert.Equal(publicIds[1], remaining.PublicId);
        await using var stored = schema.CreateContext();
        Assert.Equal(
            "winner-id",
            (await stored.Submissions.SingleAsync(s => s.PublicId == publicIds[0])).CreatedByUserId
        );
        Assert.Equal(
            "first-id",
            (await stored.Submissions.SingleAsync(s => s.PublicId == publicIds[1])).CreatedByUserId
        );
    }

    [OwnershipPostgresFact]
    public async Task DuplicatePublicIdClaimRace_RetriesAnotherOwnedCandidate()
    {
        await using var schema = await TestSchema.CreateAsync();
        var publicId = Guid.Empty;
        await using (var db = schema.CreateContext())
        {
            await db.Database.MigrateAsync();
            db.Submissions.AddRange(
                new Submission
                {
                    Id = 10,
                    PublicId = publicId,
                    ApplicantName = "First row",
                    CreatedBy = "alice",
                    Status = "draft",
                },
                new Submission
                {
                    Id = 20,
                    PublicId = publicId,
                    ApplicantName = "Second row",
                    CreatedBy = "alice",
                    Status = "draft",
                }
            );
            await db.SaveChangesAsync();
        }

        var pause = new SubmissionServiceTests.PauseClaimInterceptor();
        await using var first = schema.CreateContext(pause);
        var pendingRead = new SubmissionService(first).GetSubmissionByIdAsync(
            publicId,
            new SubmissionOwner("first-id", "alice")
        );
        await pause.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var second = schema.CreateContext();
            Assert.NotNull(
                await new SubmissionService(second).GetSubmissionByIdAsync(
                    publicId,
                    new SubmissionOwner("winner-id", "alice")
                )
            );
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        Assert.Equal("Second row", (await pendingRead)?.ApplicantName);
        await using var stored = schema.CreateContext();
        Assert.Equal(
            "winner-id",
            (await stored.Submissions.SingleAsync(s => s.Id == 10)).CreatedByUserId
        );
        Assert.Equal(
            "first-id",
            (await stored.Submissions.SingleAsync(s => s.Id == 20)).CreatedByUserId
        );
    }

    [OwnershipPostgresFact]
    public async Task DuplicatePublicId_FinalizationUsesSamePrimaryKeyForCompileAndWrite()
    {
        await using var schema = await TestSchema.CreateAsync();
        var publicId = Guid.Empty;
        await using (var db = schema.CreateContext())
        {
            await db.Database.MigrateAsync();
            db.Submissions.AddRange(
                new Submission
                {
                    Id = 10,
                    PublicId = publicId,
                    ApplicantName = "First row",
                    CreatedBy = "alice",
                    Status = "draft",
                },
                new Submission
                {
                    Id = 20,
                    PublicId = publicId,
                    ApplicantName = "Second row",
                    CreatedBy = "alice",
                    Status = "draft",
                }
            );
            db.StepData.AddRange(
                new StepData
                {
                    SubmissionId = 10,
                    FormId = "step1",
                    FormVersion = "1",
                    Data = "{\"selected\":\"first\"}",
                },
                new StepData
                {
                    SubmissionId = 20,
                    FormId = "step1",
                    FormVersion = "1",
                    Data = "{\"selected\":\"second\"}",
                }
            );
            await db.SaveChangesAsync();
        }

        var owner = new SubmissionOwner("alice-id", "alice");
        await using (var db = schema.CreateContext())
        {
            var compiled = await new StepDataService(
                db,
                NullLogger<StepDataService>.Instance
            ).GetCompiledSubmissionDataAsync(publicId, owner);
            await new SubmissionService(db).FinalizeSubmissionAsync(
                compiled.SubmissionId,
                owner,
                compiled.Data
            );
        }

        await using var stored = schema.CreateContext();
        var finalized = await stored.Submissions.SingleAsync(s => s.Id == 10);
        var untouched = await stored.Submissions.SingleAsync(s => s.Id == 20);
        Assert.Contains("\"selected\":\"first\"", finalized.SubmissionData);
        Assert.Equal(SubmissionStatus.ReadyToPrint, finalized.Status);
        Assert.Null(untouched.SubmissionData);
        Assert.Equal(SubmissionStatus.Draft, untouched.Status);
    }

    [OwnershipPostgresFact]
    public async Task DuplicatePublicId_ConcurrentDeleteCannotRedirectFinalization()
    {
        await using var schema = await TestSchema.CreateAsync();
        var publicId = Guid.Empty;
        await using (var db = schema.CreateContext())
        {
            await db.Database.MigrateAsync();
            db.Submissions.AddRange(
                new Submission
                {
                    Id = 10,
                    PublicId = publicId,
                    ApplicantName = "First row",
                    CreatedBy = "alice",
                    Status = "draft",
                },
                new Submission
                {
                    Id = 20,
                    PublicId = publicId,
                    ApplicantName = "Second row",
                    CreatedBy = "alice",
                    Status = "draft",
                }
            );
            db.StepData.AddRange(
                new StepData
                {
                    SubmissionId = 10,
                    FormId = "step1",
                    FormVersion = "1",
                    Data = "{\"selected\":\"first\"}",
                },
                new StepData
                {
                    SubmissionId = 20,
                    FormId = "step1",
                    FormVersion = "1",
                    Data = "{\"selected\":\"second\"}",
                }
            );
            await db.SaveChangesAsync();
        }

        var owner = new SubmissionOwner("alice-id", "alice");
        CompiledSubmissionData compiled;
        await using (var db = schema.CreateContext())
        {
            compiled = await new StepDataService(
                db,
                NullLogger<StepDataService>.Instance
            ).GetCompiledSubmissionDataAsync(publicId, owner);
        }

        Assert.Equal(10, compiled.SubmissionId);
        Assert.Contains("\"selected\":\"first\"", compiled.Data);

        await using (var db = schema.CreateContext())
        {
            var selected = await db.Submissions.SingleAsync(s => s.Id == compiled.SubmissionId);
            selected.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        await using (var db = schema.CreateContext())
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                new SubmissionService(db).FinalizeSubmissionAsync(
                    compiled.SubmissionId,
                    owner,
                    compiled.Data
                )
            );
        }

        await using var stored = schema.CreateContext();
        var deleted = await stored.Submissions.SingleAsync(s => s.Id == 10);
        var untouched = await stored.Submissions.SingleAsync(s => s.Id == 20);
        Assert.NotNull(deleted.DeletedAt);
        Assert.Null(deleted.SubmissionData);
        Assert.Null(untouched.SubmissionData);
        Assert.Equal(SubmissionStatus.Draft, untouched.Status);
    }

    private sealed class ClaimCommandInterceptor : DbCommandInterceptor
    {
        public List<string> Updates { get; } = new();
        public string? OwnerUserId { get; private set; }
        public string? LegacyUsername { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            if (command.CommandText.StartsWith("UPDATE submissions", StringComparison.Ordinal))
            {
                Updates.Add(command.CommandText);
                // Synthetic test values only; verify bound data as well as SQL shape.
                OwnerUserId = command
                    .Parameters.Cast<DbParameter>()
                    .Select(p => p.Value)
                    .OfType<string>()
                    .Single(s => s == "first-id");
                LegacyUsername = command
                    .Parameters.Cast<DbParameter>()
                    .Select(p => p.Value)
                    .OfType<string>()
                    .Single(s => s == "alice");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestSchema : IAsyncDisposable
    {
        private readonly string _name = $"owner_test_{Guid.NewGuid():N}";
        private readonly string _adminConnectionString;
        private readonly string _connectionString;

        private TestSchema(string connectionString)
        {
            _adminConnectionString = connectionString;
            _connectionString = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = _name,
            }.ConnectionString;
        }

        public static async Task<TestSchema> CreateAsync()
        {
            var schema = new TestSchema(
                Environment.GetEnvironmentVariable("PROBATE_OWNERSHIP_TEST_CONNECTION_STRING")!
            );
            await using var connection = new NpgsqlConnection(schema._adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"CREATE SCHEMA \"{schema._name}\"",
                connection
            );
            await command.ExecuteNonQueryAsync();
            return schema;
        }

        public ProbateDbContext CreateContext(params IInterceptor[] interceptors) =>
            new(
                new DbContextOptionsBuilder<ProbateDbContext>()
                    .UseNpgsql(_connectionString)
                    .UseSnakeCaseNamingConvention()
                    .AddInterceptors(interceptors)
                    .Options
            );

        public async ValueTask DisposeAsync()
        {
            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"DROP SCHEMA \"{_name}\" CASCADE",
                connection
            );
            await command.ExecuteNonQueryAsync();
        }
    }
}

public sealed class OwnershipPostgresFactAttribute : FactAttribute
{
    public OwnershipPostgresFactAttribute()
    {
        if (
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("PROBATE_OWNERSHIP_TEST_CONNECTION_STRING")
            )
        )
            Skip =
                "Set PROBATE_OWNERSHIP_TEST_CONNECTION_STRING to an isolated PostgreSQL test database.";
    }
}
