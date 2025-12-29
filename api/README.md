# API Documentation

## Overview

The Probate API is a RESTful service built with .NET 10 and Entity Framework Core.

## Base URL

Local Development: `http://localhost:5000`

## Authentication

Authentication will be configured in future iterations.

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
- Swagger UI: `http://localhost:5000/swagger`
- OpenAPI JSON: `http://localhost:5000/swagger/v1/swagger.json`
