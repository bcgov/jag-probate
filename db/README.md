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

### Reverting Migrations

```bash
dotnet ef database update <PreviousMigrationName> --project db --startup-project api
```

### Removing Last Migration

```bash
dotnet ef migrations remove --project db --startup-project api
```

## Staged submission owner-ID migration

`20261127050141_AddCreatedByUserIdToSubmissions` adds nullable
`submissions.created_by_user_id` (`varchar(255)`) and a non-unique index, and makes
`created_by` nullable. Existing usernames are neither rewritten nor erased.
No `users` table is read or populated for ownership.

`CreatedByUserId` is the authoritative Keycloak `sub`. Deprecated `CreatedBy`
is an optional exact username fallback **only** while `CreatedByUserId` is null.
New drafts write only the user ID. The API claims legacy rows on successful reads
and before every write; both ownership fields are EF concurrency tokens so the
claim is guarded by the original null ID and unchanged username.

### Release order

1. Apply this additive schema migration with the normal configured API startup
   or `dotnet ef database update --project db --startup-project api`.
2. Drain/replace old username-only API instances before serving requests under
   the new ownership contract. Old binaries cannot enforce authoritative user-ID
   ownership and must not remain in a mixed-version serving pool.
3. Deploy this release: accept legacy rows, claim them atomically, and create
   ID-only drafts. Do not restore a username-only binary after ID-only writes.
4. Monitor all remaining unclaimed rows, including soft-deleted rows:

   ```sql
   SELECT count(*) AS unclaimed_total,
          count(*) FILTER (WHERE deleted_at IS NULL) AS unclaimed_live,
          count(*) FILTER (
              WHERE created_by IS NULL OR btrim(created_by) = ''
          ) AS missing_legacy_username
   FROM submissions
   WHERE created_by_user_id IS NULL;
   ```

   Run this read-only count in the target environment; repository tests cannot
   establish its production value. Do not infer IDs from guessed usernames.
   Rows without a usable legacy username need an explicit trusted recovery
   process. Inactive/deleted legacy rows will not be claimed by normal live reads.
5. After all retained rows have verified IDs and all old writers are retired,
   release an ID-only reader with no username fallback. Observe it before a later
   migration removes `created_by` and makes `created_by_user_id` non-null.
   Remove the deprecated response field only with client compatibility review.

### Rollback

This migration is deliberately forward-only. `Down` raises a clear
`NotSupportedException` before changing data or schema. Dropping owner IDs would
orphan new rows whose username is null and re-enable username access to claimed
rows. Inventing empty usernames to restore `NOT NULL` is not a safe reversal.
Roll forward with both columns intact; never drop IDs to make an older binary work.

When scaffolding migrations, use valid API configuration so startup supplies
the runtime snake-case naming convention. Reject generated table/column renames
if EF falls back to an unconfigured context after a startup configuration error.

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
\d+ cases

-- Show all cases
SELECT * FROM cases;

-- Exit
\q
```