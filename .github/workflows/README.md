# GitHub Actions Workflows

This directory contains CI/CD workflows for the JAG Probate project.

## Workflows

### build-and-test-api.yml
Builds and tests the .NET API with quality gates:
- **Runs on**: Pull requests and pushes to `main` affecting `api/` or `db/`
- **Quality Gates**:
  - CSharpier formatting check
  - dotnet format verification
  - Build verification

### build-and-test-web.yml
Builds and tests the Vue.js web application with quality gates:
- **Runs on**: Pull requests and pushes to `main` affecting `web/`
- **Quality Gates**:
  - ESLint linting
  - Prettier formatting check
  - Build verification
  - Unit tests with Vitest

## Quality Gates

### 1. Linting
- **API**: `dotnet format` ensures consistent C# code style
- **Web**: ESLint enforces Vue.js and TypeScript best practices

### 2. Code Formatting
- **API**: CSharpier checks C# code formatting
- **Web**: Prettier checks Vue/TS/JS formatting


## Local Development

Run quality checks locally before pushing:

### API
```bash
cd api
dotnet restore
dotnet format --verify-no-changes
dotnet csharpier --check .
dotnet build
```

### Web
```bash
cd web
npm ci
npm run lint
npm run prettier:check
npm run build
npm run test
```

## Continuous Integration

All workflows run automatically on:
- Pull requests to `main`
- Pushes to `main`
- Manual trigger via `workflow_dispatch`

Failed quality gates will block PR merges.
