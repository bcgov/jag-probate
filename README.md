# JAG Probate

This repository contains the code for the Probate Management System for BC Gov

## Project Structure

```
jag-probate/
├── api/                   # .NET 10 Web API
│   ├── Controllers/       # API Controllers
│   ├── Services/          # Business logic services
│   ├── Program.cs         # Application entry point
│   └── Startup.cs         # Service configuration
├── db/                    # .NET 10 Class Library - Data Layer
│   ├── Models/            # Entity models
│   └── Migrations/        # EF Core migrations
├── web/                   # Vue 3 Frontend
│   ├── src/               # Vue source code
│   ├── public/            # Static assets
│   └── vite.config.js     # Vite configuration
└── docker/                # Docker configuration
    ├── api/               # API Dockerfile
    ├── web/               # Web Dockerfile
    ├── docker-compose.yaml
    └── manage             # Build and run script
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [s2i (Source-to-Image)](https://github.com/openshift/source-to-image) tool

## Getting Started with Docker

### 1. Clone the repository

```bash
git clone <repository-url>
cd jag-probate
```

### 2. Build Docker images

```bash
cd docker
./manage build
```

### 3. Start the application

```bash
./manage start
```

Or for development with hot reload:

```bash
./manage debug
```
Refer to [Running Application on Docker](./docker/README.md) for more commands.

### 4. Access the application

- **Web UI**: http://localhost:8080
- **API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/api/health



## Database

The project uses PostgreSQL as the database. Entity Framework Core handles migrations automatically on startup.

## API

### Health Check
- `GET /api/health` - Check API health status

### CHEFS (Common Hosted Form Service)

The API integrates with [CHEFS](https://developer.gov.bc.ca/docs/default/component/chefs-techdocs/) to retrieve form submissions (applications) for the dashboard. Authentication uses **api-key** only (no auth-token).

**Form URL (dev):**  
https://chefs-dev.apps.silver.devops.gov.bc.ca/app/form/submit?f=449a6ceb-9cf8-45cc-a697-e5bd46cfb522  

**Endpoint:**
- `GET /api/chefs/Applications?formId={formId}` – Returns current and previous applications (submissions) for the given CHEFS form. Requires authentication. `formId` must match the configured `Chefs:FormId`.

**Required configuration** (see Environment Variables): `Chefs:BaseUrl`, `Chefs:ApiKey`, and `Chefs:FormId` (for validation). The frontend passes `formId` in the query; the backend validates it against the configured form.

**Error handling:** CHEFS API errors (e.g. 4xx/5xx or unreachable) are returned as Problem Details (e.g. 502 Bad Gateway with a descriptive message).

## Environment Variables

Copy `docker/.env.template` to `docker/.env` and customize as needed.

**CHEFS** (required for the applications endpoint):
- `CHEFS_BASE_URL` – CHEFS API base URL (e.g. `https://chefs-dev.apps.silver.devops.gov.bc.ca/app/api/v1`)
- `CHEFS_API_KEY` – API key for the form (per-form; obtain from CHEFS form settings)
- `CHEFS_FORM_ID` – Form UUID allowed for submissions (e.g. `449a6ceb-9cf8-45cc-a697-e5bd46cfb522`). The frontend must pass this same `formId` in the query.


## Troubleshooting

### Port already in use
If port 5000 or 5432 is already in use:
```bash
./manage stop
docker ps -a  # Check for running containers
```

### Database connection issues
Ensure PostgreSQL container is running:
```bash
docker ps
docker logs jag-probate-db-1
```

### Build issues
Clean and rebuild:
```bash
./manage down
./manage build
./manage start
```

## How to Contribute

If you would like to contribute, please see our [CONTRIBUTING](./CONTRIBUTING.md) guidelines.

Please note that this project is released with a [Contributor Code of Conduct](./CODE_OF_CONDUCT.md).
By participating in this project you agree to abide by its terms.

