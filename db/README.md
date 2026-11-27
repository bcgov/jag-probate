# Database Setup

This directory contains database migrations and setup scripts.

## Entity Framework Core Migrations

### Creating a Migration

From the project root directory:

```bash
dotnet ef migrations add <MigrationName> --project db --startup-project api
```

Example:
```bash
dotnet ef migrations add InitialCreate --project db --startup-project api
```

### Applying Migrations

Migrations are automatically applied when the API starts up. However, you can manually apply them:

```bash
dotnet ef database update --project db --startup-project api
```

### Removing unused tables

The `RemoveUnusedEntities` migration removes the unused `cases`, `documents`,
and `users` tables. Before deploying, check all environments for rows and
external dependencies on these tables. The migration ignores tables already
absent from the current schema. It locks existing tables and fails if any
contain data; because migrations run at startup, this prevents the API from
starting until the data has been reviewed and safely handled.

Back up any data that must be retained before removing it. Rolling back the
migration recreates the empty tables but does not restore their former data.
The migration does not remove submissions, step data, or data-protection keys.

To check which tables are present before deployment:

```sql
SELECT table_name
FROM information_schema.tables
WHERE table_schema = current_schema()
  AND table_name IN ('cases', 'documents', 'users');
```

For each table returned, check its row count, for example
`SELECT count(*) FROM cases;`.

The migration integration tests require a disposable PostgreSQL database. Set
`PROBATE_MIGRATION_TEST_CONNECTION_STRING` to its connection string, then run:

```bash
dotnet test api.tests --filter FullyQualifiedName~RemoveUnusedEntitiesMigrationTests
```

The test user must be able to create and drop schemas. Tests are skipped when
the connection string is not set.

### Reverting Migrations

```bash
dotnet ef database update <PreviousMigrationName> --project db --startup-project api
```

### Removing Last Migration

```bash
dotnet ef migrations remove --project db --startup-project api
```

## Connection String

Default local development connection string:
```
Host=db;Port=5432;Database=probatedb;Username=probate;Password=probate123
```

## PostgreSQL Access

### Connect via Docker

```bash
docker exec -it jag-probate-db-1 psql -U probate -d probatedb
```

### Common PostgreSQL Commands

```sql
-- List tables
\dt

-- Describe table
\d+ submissions

-- Show submissions
SELECT * FROM submissions;

-- Exit
\q
```