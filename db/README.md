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

## Database Schema

### Tables

- **cases** - Probate case information
- **users** - User accounts
- **documents** - Document metadata associated with cases
- **data_protection_keys** - ASP.NET Core Data Protection keys

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

## Seeding Data

To add seed data, create a new file in `db/Seeders/` and call it from the `MigrationService`.
