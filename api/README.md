# API Documentation

## Overview

The Probate API is a RESTful service built with .NET 10 and Entity Framework Core.

## Base URL

Local Development: `http://localhost:5000`

## PDF generation

The Docker stack runs WeasyPrint at `http://weasyprint:5001` for the API and exposes it at `http://localhost:8061` for local inspection. Set `WEASYPRINT_BASE_URL` only when the API must use a different WeasyPrint host.

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
