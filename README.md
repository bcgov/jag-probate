# JAG Probate

Probate Management System for BC Gov

## Project Structure

```
jag-probate/
├── api/                    # .NET 9 Web API
│   ├── Controllers/        # API Controllers
│   ├── Services/          # Business logic services
│   ├── Program.cs         # Application entry point
│   └── Startup.cs         # Service configuration
├── db/                    # .NET 9 Class Library - Data Layer
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

## Getting Started

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

### 4. Access the application

- **Web UI**: http://localhost:8080
- **API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/api/health

## Docker Commands

### Build all containers
```bash
./manage build
```

### Build specific container
```bash
./manage build api
```

### Start services
```bash
./manage start
```

### Start in debug mode (with hot reload)
```bash
./manage debug
```

### Stop services
```bash
./manage stop
```

### Remove containers and volumes
```bash
./manage down
# or
./manage rm
```

## Database

The project uses PostgreSQL as the database. Entity Framework Core handles migrations automatically on startup.

### Connection String
```
Host=db;Port=5432;Database=probatedb;Username=probate;Password=probate123
```

### Manual Migration Commands

If you need to create migrations manually:

```bash
# From the root directory
dotnet ef migrations add InitialCreate --project db --startup-project api

# Apply migrations
dotnet ef database update --project db --startup-project api
```

## API Endpoints

### Health Check
- `GET /api/health` - Check API health status

### Cases
- `GET /api/cases` - Get all cases
- `GET /api/cases/{id}` - Get case by ID
- `POST /api/cases` - Create new case
- `PUT /api/cases/{id}` - Update case
- `DELETE /api/cases/{id}` - Delete case

## Environment Variables

Copy `docker/.env.template` to `docker/.env` and customize as needed.

Key variables:
- `POSTGRESQL_DATABASE` - Database name
- `POSTGRESQL_USER` - Database user
- `POSTGRESQL_PASSWORD` - Database password
- `ASPNETCORE_ENVIRONMENT` - Development/Production
- `CORS_DOMAIN` - Allowed CORS origins

## Development

### Project Dependencies

**api** project depends on:
- db (Entity Framework models and context)

### Adding New Entities

1. Create entity class in `db/Models/`
2. Add DbSet to `ProbateDbContext.cs`
3. Create migration:
   ```bash
   dotnet ef migrations add YourMigrationName --project db --startup-project api
   ```
4. Restart the application (migrations run automatically)

### Adding New API Endpoints

1. Create controller in `api/Controllers/`
2. Inject `ProbateDbContext` in constructor
3. Implement endpoints following REST conventions

## Deployment

This application is designed to run on OpenShift BC Gov Emerald cluster.

### OpenShift Configuration

OpenShift deployment files will be added in future iterations.

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

## License

Copyright © 2025 Province of British Columbia
