# API Documentation

## Overview

The Probate API is a RESTful service built with .NET 10 and Entity Framework Core.

## Base URL

Local Development: `http://localhost:5000`

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
