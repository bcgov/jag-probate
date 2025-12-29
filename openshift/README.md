# OpenShift Deployment

This directory will contain OpenShift deployment configurations for the Probate application.

## Deployment to OpenShift BC Gov Emerald

### Prerequisites

- Access to OpenShift BC Gov Emerald cluster
- `oc` CLI tool installed
- Proper RBAC permissions in the target namespace

### Deployment Steps

Coming soon...

## Configuration

Environment-specific configurations:
- **DEV**: Development environment
- **TEST**: Testing environment
- **PROD**: Production environment

### Secrets Management

Secrets should be managed through OpenShift secrets **Vault**.

### Image Streams

The application uses s2i (source-to-image) builds for creating container images.
