# API Documentation

## Overview

The Probate API is a RESTful service built with .NET 10 and Entity Framework Core.

## Base URL

Local Development: `http://localhost:5000`

## Application ownership

Submission-by-ID reads, finalization, deletion, and all step-data routes require
the authenticated `preferred_username` to match the submission's `CreatedBy`.
`SubmissionOwnershipAttribute` checks ownership before the controller action
runs. Submission and step-data service lookups also require the same username,
so duplicate legacy public IDs cannot authorize one owner's row and access
another owner's row. Foreign, missing, and soft-deleted submissions return the same `404`;
an authenticated principal without a usable username returns `401`.
Deletion prefers live owner-matching rows over legacy rows already soft-deleted;
the service remains idempotent when no live row remains.
Draft creation and submission listing already use the authenticated username.

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
