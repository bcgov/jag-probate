# Security Guidelines

## Credentials and Secrets

### ⚠️ Never commit:
- Production database credentials
- API keys or tokens
- `.env` files with real credentials
- `appsettings.Production.json` or `appsettings.Staging.json`

### ✅ Safe to commit:
- `appsettings.json` - Contains development/Docker defaults only
- `appsettings.Development.json` - Logging configuration
- `.env.template` - Template with placeholders

## Development Credentials

The credentials in `appsettings.json` and `docker/.env.template` are:
- **Only for local Docker development**
- **Not production credentials**
- Used within the isolated Docker network

Default development credentials:
- Database: `probate` / `probate123`
- These only work inside Docker containers on localhost

## Production Deployment

For production:
1. Never use default credentials
2. Store secrets in:
   - Environment variables
   - Azure Key Vault
   - OpenShift Secrets
3. Create `appsettings.Production.json` (excluded from git) with real credentials

## Reporting Security Issues

If you discover a security vulnerability, please email: security@gov.bc.ca
