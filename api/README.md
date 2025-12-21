# API Documentation

## Overview

The Probate API is a RESTful service built with .NET 9 and Entity Framework Core.

## Base URL

Local Development: `http://localhost:5000`

## Authentication

Authentication will be configured in future iterations.

## Endpoints

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

---

### Cases

#### GET /api/cases

Retrieve all cases.

**Response:**
```json
[
  {
    "id": 1,
    "caseNumber": "P-2025-001",
    "title": "Estate of John Doe",
    "status": "Open",
    "filedDate": "2025-01-15T00:00:00Z",
    "description": "Probate case for estate settlement",
    "createdAt": "2025-01-10T08:30:00Z",
    "updatedAt": null
  }
]
```

#### GET /api/cases/{id}

Retrieve a specific case by ID.

**Parameters:**
- `id` (integer, required) - The case ID

**Response:**
```json
{
  "id": 1,
  "caseNumber": "P-2025-001",
  "title": "Estate of John Doe",
  "status": "Open",
  "filedDate": "2025-01-15T00:00:00Z",
  "description": "Probate case for estate settlement",
  "createdAt": "2025-01-10T08:30:00Z",
  "updatedAt": null
}
```

#### POST /api/cases

Create a new case.

**Request Body:**
```json
{
  "caseNumber": "P-2025-002",
  "title": "Estate of Jane Smith",
  "status": "Open",
  "filedDate": "2025-01-20T00:00:00Z",
  "description": "New probate case"
}
```

**Response:** `201 Created`
```json
{
  "id": 2,
  "caseNumber": "P-2025-002",
  "title": "Estate of Jane Smith",
  "status": "Open",
  "filedDate": "2025-01-20T00:00:00Z",
  "description": "New probate case",
  "createdAt": "2025-01-20T10:00:00Z",
  "updatedAt": null
}
```

#### PUT /api/cases/{id}

Update an existing case.

**Parameters:**
- `id` (integer, required) - The case ID

**Request Body:**
```json
{
  "id": 1,
  "caseNumber": "P-2025-001",
  "title": "Estate of John Doe - Updated",
  "status": "In Review",
  "filedDate": "2025-01-15T00:00:00Z",
  "description": "Updated description"
}
```

**Response:** `200 OK`

#### DELETE /api/cases/{id}

Delete a case.

**Parameters:**
- `id` (integer, required) - The case ID

**Response:** `204 No Content`

---

## Error Responses

### 404 Not Found
```json
{
  "message": "Case with ID 999 not found"
}
```

### 400 Bad Request
```json
{
  "message": "ID mismatch"
}
```

### 500 Internal Server Error
```json
{
  "status": 500,
  "title": "An error occurred while processing your request.",
  "detail": "Error message details"
}
```

## Swagger/OpenAPI

Interactive API documentation is available at:
- Swagger UI: `http://localhost:5000/swagger`
- OpenAPI JSON: `http://localhost:5000/swagger/v1/swagger.json`
