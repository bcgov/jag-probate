# API Documentation

## Overview

The Probate API is a RESTful service built with .NET 10 and Entity Framework Core.

## Base URL

Local Development: `http://localhost:5000`

## Authentication

CDogs uses OAuth2 client credentials for authentication. To obtain a token and get your `ClientId` and `ClientSecret`, register or find the CDogs API through the BC Government API Directory:

https://api.gov.bc.ca/devportal/api-directory

Once you have your credentials, set the following in your `.env` file:

```env
CDOGS_TOKEN_URL=https://loginproxy.gov.bc.ca/auth/realms/comsvcauth/protocol/openid-connect/token
CDOGS_CLIENT_ID=<your-client-id>
CDOGS_CLIENT_SECRET=<your-client-secret>
```

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
