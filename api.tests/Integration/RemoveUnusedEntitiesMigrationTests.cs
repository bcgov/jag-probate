using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Probate.Db.Models;

namespace Probate.Api.Tests.Integration;

public class RemoveUnusedEntitiesMigrationTests
{
    private const string PreviousMigration = "20261029220356_DropChefsSubmissionIdFromSubmissions";
    private const string CleanupMigration = "20261126222217_RemoveUnusedEntities";

    [PostgresFact]
    public async Task FreshDatabase_AppliesAllMigrations()
    {
        await using var schema = await TestSchema.CreateAsync();
        await using var db = schema.CreateContext();

        await db.Database.MigrateAsync();

        foreach (var table in new[] { "cases", "documents", "users" })
            Assert.False(await schema.TableExistsAsync(table));
        foreach (var table in new[] { "submissions", "step_data", "data_protection_keys" })
            Assert.True(await schema.TableExistsAsync(table));
        Assert.Contains(CleanupMigration, await db.Database.GetAppliedMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task EmptyTables_AreRemoved_AndActiveDataSurvivesUpgradeAndRollback()
    {
        await using var schema = await TestSchema.CreateAsync();
        await using var db = schema.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        var submission = new Submission { ApplicantName = "Test", CreatedBy = "test-user" };
        submission.StepDataEntries.Add(new StepData { FormId = "test-form", Data = "answers" });
        db.Submissions.Add(submission);
        db.DataProtectionKeys.Add(new DataProtectionKey { FriendlyName = "test", Xml = "key" });
        await db.SaveChangesAsync();

        await migrator.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        foreach (var table in new[] { "cases", "documents", "users" })
            Assert.False(await schema.TableExistsAsync(table));
        db.ChangeTracker.Clear();
        Assert.Equal("answers", (await db.StepData.SingleAsync()).Data);
        Assert.Equal("Test", (await db.Submissions.SingleAsync()).ApplicantName);
        Assert.Equal("key", (await db.DataProtectionKeys.SingleAsync()).Xml);

        await migrator.MigrateAsync(PreviousMigration);
        foreach (var table in new[] { "cases", "documents", "users" })
            Assert.True(await schema.TableExistsAsync(table));
        Assert.Equal("answers", (await db.StepData.SingleAsync()).Data);
        Assert.Equal("key", (await db.DataProtectionKeys.SingleAsync()).Xml);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO cases (case_number, title, created_at) VALUES ('1', 'test', now());
            INSERT INTO documents (case_id, file_name, file_size, created_at)
            SELECT id, 'test', 1, now() FROM cases;
            DELETE FROM cases;
            """
        );
        Assert.Equal(0L, await schema.RowCountAsync("documents"));
    }

    [PostgresTheory]
    [InlineData("cases")]
    [InlineData("documents")]
    [InlineData("users")]
    public async Task PopulatedTable_StopsMigrationWithoutDeletingData(string table)
    {
        await using var schema = await TestSchema.CreateAsync();
        await using var db = schema.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        await InsertRowAsync(db, table);

        var error = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());
        Assert.Contains("Cannot remove unused tables", error.MessageText);
        foreach (var name in new[] { "cases", "documents", "users" })
            Assert.True(await schema.TableExistsAsync(name));
        Assert.Equal(1L, await schema.RowCountAsync(table));
        Assert.DoesNotContain(CleanupMigration, await db.Database.GetAppliedMigrationsAsync());
    }

    [PostgresTheory]
    [InlineData("cases")]
    [InlineData("documents")]
    [InlineData("users")]
    [InlineData("all")]
    public async Task MissingTables_AreIgnored(string missingTable)
    {
        await using var schema = await TestSchema.CreateAsync();
        await using var db = schema.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await DropTableAsync(db, missingTable);

        await migrator.MigrateAsync();

        foreach (var table in new[] { "cases", "documents", "users" })
            Assert.False(await schema.TableExistsAsync(table));
        Assert.Contains(CleanupMigration, await db.Database.GetAppliedMigrationsAsync());

        await migrator.MigrateAsync(PreviousMigration);
        foreach (var table in new[] { "cases", "documents", "users" })
            Assert.True(await schema.TableExistsAsync(table));
    }

    [PostgresTheory]
    [InlineData("cases", "users")]
    [InlineData("documents", "cases")]
    [InlineData("users", "documents")]
    public async Task MissingTable_DoesNotBypassPopulatedTableGuard(
        string missingTable,
        string populatedTable
    )
    {
        await using var schema = await TestSchema.CreateAsync();
        await using var db = schema.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await DropTableAsync(db, missingTable);
        await InsertRowAsync(db, populatedTable);

        var error = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());

        Assert.Contains("Cannot remove unused tables", error.MessageText);
        Assert.False(await schema.TableExistsAsync(missingTable));
        Assert.Equal(1L, await schema.RowCountAsync(populatedTable));
        foreach (var table in new[] { "cases", "documents", "users" })
        {
            if (table != missingTable)
                Assert.True(await schema.TableExistsAsync(table));
        }
        Assert.DoesNotContain(CleanupMigration, await db.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    public async Task TablesInAnotherSearchPathSchema_AreNotTouched()
    {
        await using var schema = await TestSchema.CreateAsync();
        await using var otherSchema = await TestSchema.CreateAsync();
        await using var otherDb = otherSchema.CreateContext();
        await otherDb.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE cases (id integer PRIMARY KEY);
            CREATE TABLE documents (id integer PRIMARY KEY);
            CREATE TABLE users (id integer PRIMARY KEY);
            INSERT INTO users VALUES (1);
            """
        );

        await using var db = schema.CreateContext(otherSchema);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await DropTableAsync(db, "all");

        await migrator.MigrateAsync();

        foreach (var table in new[] { "cases", "documents", "users" })
        {
            Assert.False(await schema.TableExistsAsync(table));
            Assert.True(await otherSchema.TableExistsAsync(table));
        }
        Assert.Equal(1L, await otherSchema.RowCountAsync("users"));
    }

    [PostgresFact]
    public async Task TableCreatedAfterCheck_IsNotDroppedWithoutBeingChecked()
    {
        await using var schema = await TestSchema.CreateAsync();
        await using var db = schema.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await DropTableAsync(db, "documents");

        await using var blocker = schema.CreateConnection();
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (
            var command = new NpgsqlCommand(
                "LOCK TABLE users IN ACCESS EXCLUSIVE MODE",
                blocker,
                transaction
            )
        )
            await command.ExecuteNonQueryAsync();

        var migrationTask = migrator.MigrateAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using var waitingCommand = new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1 FROM pg_locks
                    WHERE relation = 'users'::regclass
                      AND mode = 'AccessExclusiveLock'
                      AND NOT granted
                )
                """,
                blocker,
                transaction
            );
            while (!(bool)(await waitingCommand.ExecuteScalarAsync(timeout.Token))!)
            {
                if (migrationTask.IsCompleted)
                    throw new InvalidOperationException(
                        "Migration did not wait for the table lock."
                    );
                await Task.Delay(50, timeout.Token);
            }

            await using var creator = schema.CreateContext();
            await creator.Database.ExecuteSqlRawAsync(
                "CREATE TABLE documents (id integer PRIMARY KEY); INSERT INTO documents VALUES (1);"
            );
        }
        finally
        {
            await transaction.RollbackAsync();
            await migrationTask;
        }

        Assert.False(await schema.TableExistsAsync("cases"));
        Assert.False(await schema.TableExistsAsync("users"));
        Assert.True(await schema.TableExistsAsync("documents"));
        Assert.Equal(1L, await schema.RowCountAsync("documents"));
        Assert.Contains(CleanupMigration, await db.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    public async Task IdempotentScript_IgnoresMissingTable_AndRunsTwice()
    {
        await using var schema = await TestSchema.CreateAsync();
        await using var db = schema.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await DropTableAsync(db, "users");
        var script = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

        await db.Database.ExecuteSqlRawAsync(script);
        await db.Database.ExecuteSqlRawAsync(script);

        foreach (var table in new[] { "cases", "documents", "users" })
            Assert.False(await schema.TableExistsAsync(table));
        Assert.Contains(CleanupMigration, await db.Database.GetAppliedMigrationsAsync());
    }

    private static Task InsertRowAsync(ProbateDbContext db, string table)
    {
        var sql = table switch
        {
            "cases" =>
                "INSERT INTO cases (case_number, title, created_at) VALUES ('1', 'test', now())",
            "documents" => """
                ALTER TABLE documents DROP CONSTRAINT fk_documents_cases_case_id;
                INSERT INTO documents (case_id, file_name, file_size, created_at)
                VALUES (1, 'test', 1, now());
                """,
            "users" => """
                INSERT INTO users (first_name, last_name, email, is_active, created_at)
                VALUES ('test', 'user', 'test@example.invalid', true, now())
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
        return db.Database.ExecuteSqlRawAsync(sql);
    }

    private static Task DropTableAsync(ProbateDbContext db, string table)
    {
        var sql = table switch
        {
            "cases" => """
                ALTER TABLE documents DROP CONSTRAINT fk_documents_cases_case_id;
                DROP TABLE cases;
                """,
            "documents" => "DROP TABLE documents",
            "users" => "DROP TABLE users",
            "all" => "DROP TABLE documents; DROP TABLE users; DROP TABLE cases;",
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
        return db.Database.ExecuteSqlRawAsync(sql);
    }

    private sealed class TestSchema : IAsyncDisposable
    {
        private readonly string _name = $"migration_test_{Guid.NewGuid():N}";
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
                Environment.GetEnvironmentVariable("PROBATE_MIGRATION_TEST_CONNECTION_STRING")!
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

        public ProbateDbContext CreateContext(TestSchema? otherSchema = null)
        {
            var connectionString = new NpgsqlConnectionStringBuilder(_connectionString);
            if (otherSchema != null)
                connectionString.SearchPath = $"{_name},{otherSchema._name}";
            return new ProbateDbContext(
                new DbContextOptionsBuilder<ProbateDbContext>()
                    .UseNpgsql(connectionString.ConnectionString)
                    .UseSnakeCaseNamingConvention()
                    .Options
            );
        }

        public NpgsqlConnection CreateConnection() => new(_connectionString);

        public async Task<bool> TableExistsAsync(string table)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT to_regclass(@name) IS NOT NULL",
                connection
            );
            command.Parameters.AddWithValue("name", table);
            return (bool)(await command.ExecuteScalarAsync())!;
        }

        public async Task<long> RowCountAsync(string table)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"SELECT count(*) FROM \"{table}\"",
                connection
            );
            return (long)(await command.ExecuteScalarAsync())!;
        }

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

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("PROBATE_MIGRATION_TEST_CONNECTION_STRING")
            )
        )
            Skip =
                "Set PROBATE_MIGRATION_TEST_CONNECTION_STRING to an isolated PostgreSQL test database.";
    }
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (
            string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("PROBATE_MIGRATION_TEST_CONNECTION_STRING")
            )
        )
            Skip =
                "Set PROBATE_MIGRATION_TEST_CONNECTION_STRING to an isolated PostgreSQL test database.";
    }
}
