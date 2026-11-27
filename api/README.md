# API Documentation

## Overview

The Probate API is a RESTful service built with .NET 10 and Entity Framework Core.

## Base URL

Local Development: `http://localhost:5000`

## Application ownership

All submission and step endpoints require a nonblank Keycloak `sub`, falling
back to ASP.NET's mapped `ClaimTypes.NameIdentifier`. Username alone never
authorizes access. Missing user IDs return `401`, including on listing and draft
creation. `preferred_username` is optional and used only for legacy rows.

The shared `SubmissionOwnership.OwnedBy` database predicate accepts:

- `CreatedByUserId == currentUserId` (the authoritative owner).
- `CreatedByUserId IS NULL` and an exact, nonblank legacy username match.

A different non-null owner ID always denies access, even with the same username.
No case folding or trimming is performed. Ownership and public-ID/soft-delete
filters are applied together in SQL, never by checking a fetched row in memory.
Foreign, missing, and soft-deleted submissions return `404`.
`SubmissionOwnershipAttribute` enforces identity/resource guards before
automatic request validation and controller execution. Services repeat the
shared predicate, so duplicate legacy public IDs cannot cross user boundaries.

Successful reads **and** writes atomically claim accessed legacy rows before
returning data or performing the requested operation. Listing loads owned live
rows in one query and claims its legacy rows in one guarded `SaveChanges` batch;
if a concurrent claim wins, the batch rolls back and the list is reloaded under
the shared predicate. `FindAndClaimAsync` updates only the owner ID;
EF concurrency tokens put the original null owner ID and exact legacy username
in the UPDATE's WHERE clause. A losing claim is discarded and the same row is
requeried under authoritative ownership: a different user receives no data;
another request from the same user may proceed. Existing usernames are preserved.

New drafts populate only `CreatedByUserId` and leave `CreatedBy` null. No `users`
table lookup or write is used. Submission responses do not expose owner IDs or
`sub`; deprecated `createdBy` remains nullable for compatibility.
Deletion prefers live owner-matching rows over legacy rows already soft-deleted;
the service remains idempotent when no live row remains.

See [the staged database rollout](../db/README.md#staged-submission-owner-id-migration)
for deployment order, unclaimed-row counts, rollback restrictions, and the later
removal of the username fallback and column.

All 18 controller endpoints were reviewed:

| Endpoints | Data scope |
| --- | --- |
| `GET /api/submissions`, `POST /api/submissions/draft` | Current user only |
| `GET`, `DELETE /api/submissions/{id}`, `POST /api/submissions/{id}/submit` | Ownership check before reading or changing application data |
| `GET /api/submissions/{publicId}/steps`, `GET .../compiled`, `GET`, `PUT .../{formId}` | Parent submission ownership check |
| `POST /api/report/generate-from-submission` | Renders caller-provided data; does not load stored applications |
| `GET /api/chefs/Auth`, `GET /api/chefs/Sidebar` | Shared form configuration/token for embedding; no stored application data |
| `GET /api/courtlocations` | Shared reference data |
| `GET /api/auth/user` | Current principal only |
| `GET /api/auth/login`, `GET /api/auth/logout`, `GET /api/auth/status` | Current session/login flow |
| `GET /api/health` | Public health response; no user data |

### Ownership validation

Focused HTTP/service tests cover subject mapping, missing identities, new drafts,
listing, foreign-ID denial, username changes, legacy claims, concurrent claim
winners/losers, and duplicate public IDs. PostgreSQL migration/claim tests use
isolated temporary schemas and verify the actual guarded UPDATE and generated
ownership SQL:

```powershell
$env:PROBATE_OWNERSHIP_TEST_CONNECTION_STRING = '<isolated test PostgreSQL connection>'
dotnet test api.tests\api.tests.csproj --filter "FullyQualifiedName~SubmissionOwnerMigrationTests|FullyQualifiedName~SubmissionOwnershipIntegrationTests|FullyQualifiedName~SubmissionServiceTests|FullyQualifiedName~SubmissionsControllerTests|FullyQualifiedName~StepDataControllerTests"
```

Without the test connection, only the PostgreSQL-specific tests are skipped.
Never point these tests at a production or shared application database.

CHEFS gateway permissions are managed by CHEFS, outside this API. Application
data is stored locally; form embedding tokens must not grant access to other
users' submissions in CHEFS. Recheck that external permission boundary when
changing the CHEFS integration.

## PDF generation

The Docker stack runs WeasyPrint at `http://weasyprint:5001` for the API and exposes it at `http://localhost:8061` for local inspection. Set `WEASYPRINT_BASE_URL` only when the API must use a different WeasyPrint host.

Templates use Fluid's built-in money filters with CAD as the default currency.
The shared options set `MoneyFormat` to `{{amount}}` because the exhibit templates
already supply the dollar sign: `1134.65` renders as `$1,134.65`.

The template service passes `HtmlEncoder.Default` to Fluid when rendering HTML.
Model values are HTML-encoded by default, including when a client time zone is
provided. For example, `<span style="background-color:purple">TestHTML</span>`
is displayed as literal text in the PDF, not as a styled element. Static template
markup is unchanged; existing `escape` filters and captured output are not
encoded again. Do not use Fluid's `raw` filter on model values, as it bypasses
the encoder. This encoding is separate from HTML sanitization.

### Health Check

#### GET /api/health

Check the health status of the API.

**Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2025-12-21T10:00:00Z",
  "application": "Probate API"
}
```


Interactive API documentation is available at:
- Swagger UI: `http://localhost:5000/api/swagger`
- OpenAPI JSON: `http://localhost:5000/api/swagger/v1/swagger.json`
