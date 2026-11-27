using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        await db.Database.ExecuteSqlInterpolatedAsync(
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
