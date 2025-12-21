# Quick Start Guide

This guide will help you get the JAG Probate application running on your local machine in minutes.

## Prerequisites Check

Before starting, ensure you have the following installed:

- [ ] .NET 10 SDK - [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)
- [ ] Docker Desktop - [Download here](https://www.docker.com/products/docker-desktop)
- [ ] s2i tool - [Download here](https://github.com/openshift/source-to-image/releases)
- [ ] Git

### Verify Prerequisites

```bash
# Check .NET version (should show 10.x.x)
dotnet --version

# Check Docker
docker --version

# Check s2i
s2i version

# Check Git
git --version
```

## Step-by-Step Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd jag-probate
```

### 2. Build the Docker Images

```bash
cd docker
./manage build
```

This will build the API container image. First build may take several minutes.

### 3. Start the Application

```bash
./manage start
```

Or for development mode with hot reload:

```bash
./manage debug
```

The application will:
- Start PostgreSQL database on port 5432
- Start API on port 5000
- Start Web UI on port 8080
- Automatically run database migrations
- Be ready to accept requests

### 4. Verify the Application is Running

Open your browser or use curl:

**Web UI:**
Open browser to: http://localhost:8080

**Health Check:**
```bash
curl http://localhost:5000/api/health
```

**Swagger UI:**
Open browser to: http://localhost:5000/swagger

### 5. Test the API

#### Create a Case
```bash
curl -X POST http://localhost:5000/api/cases \
  -H "Content-Type: application/json" \
  -d '{
    "caseNumber": "P-2025-001",
    "title": "Estate of John Doe",
    "status": "Open",
    "filedDate": "2025-01-15T00:00:00Z",
    "description": "Test probate case"
  }'
```

#### Get All Cases
```bash
curl http://localhost:5000/api/cases
```

## Stopping the Application

```bash
cd docker
./manage stop
```

## Cleanup (Remove Everything)

To remove containers and volumes:

```bash
cd docker
./manage down
```

## Troubleshooting

### Port Already in Use

If you get "port already in use" errors:

1. Check what's using the port:
   ```bash
   # Windows
   netstat -ano | findstr :5000
   
   # Linux/Mac
   lsof -i :5000
   ```

2. Stop the conflicting process or change the port in `docker/.env`

### Docker Build Fails

1. Ensure Docker Desktop is running
2. Try cleaning up:
   ```bash
   docker system prune -a
   ```
3. Rebuild:
   ```bash
   cd docker
   ./manage build
   ```

### Database Connection Errors

1. Check if PostgreSQL container is running:
   ```bash
   docker ps
   ```

2. View database logs:
   ```bash
   docker logs jag-probate-db-1
   ```

### API Won't Start

1. Check API logs:
   ```bash
   docker logs jag-probate-api-1
   ```

2. Verify .env file exists:
   ```bash
   ls docker/.env
   ```

## Next Steps

- Read [README.md](README.md) for detailed documentation
- Explore [API documentation](api/README.md)
- Learn about [database migrations](db/README.md)
- Check [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines

## Using Make Commands (Alternative)

From project root:

```bash
# Build
make build

# Start
make start

# Debug mode
make debug

# Stop
make stop

# Clean up
make down

# Help
make help
```

## Development Workflow

1. Make code changes in `api/` or `db/`
2. If running in debug mode (`./manage debug`), changes will hot reload
3. If not in debug mode, restart:
   ```bash
   ./manage stop
   ./manage start
   ```

## Getting Help

- Check the [README.md](README.md)
- Open an issue on GitHub
- Contact the development team

Happy coding! 🚀
