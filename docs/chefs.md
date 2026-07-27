# CHEFS Integration

The API integrates with [CHEFS (Common Hosted Form Service)](https://developer.gov.bc.ca/docs/default/component/chefs-techdocs/) to retrieve form submissions (applications) for the dashboard. CHEFS credentials are configured per form; the API sends Basic Authentication using the form GUID and that form's API key.

## Endpoint

`GET /api/chefs/applications?formKey={formKey}`

Returns current and previous applications (submissions) for the given form. Requires authentication.

`formKey` is a logical identifier (e.g. `probate`) configured server-side. The backend resolves it to the actual CHEFS form GUID — the GUID is never exposed to the frontend.

**Error handling:** CHEFS API errors (4xx/5xx or unreachable) are returned as Problem Details (e.g. `502 Bad Gateway` with a descriptive message).

## Configuration (DEV Environments)

Copy `docker/.env.template` to `docker/.env` and set the following variables. `docker-compose.yaml` maps these flat `.env` values onto the ASP.NET Core `Chefs__*` configuration keys for the `api` service.

| `.env` variable               | Maps to config key               | Description                                                                    |
| ----------------------------- | -------------------------------- | ------------------------------------------------------------------------------ |
| `CHEFS_BASE_URL`              | `Chefs__BaseUrl`                 | CHEFS app base URL (e.g. `https://chefs-dev.apps.silver.devops.gov.bc.ca/app`) |
| `CHEFS_FORM_LEGAL_FORMID`     | `Chefs__Forms__legal__FormId`    | CHEFS form GUID for the `legal` form key.                                      |
| `CHEFS_FORM_LEGAL_API_KEY`    | `Chefs__Forms__legal__ApiKey`    | API key for the `legal` form.                                                  |
| `CHEFS_FORM_NONLEGAL_FORMID`  | `Chefs__Forms__nonlegal__FormId` | CHEFS form GUID for the `nonlegal` form key.                                   |
| `CHEFS_FORM_NONLEGAL_API_KEY` | `Chefs__Forms__nonlegal__ApiKey` | API key for the `nonlegal` form.                                               |

### Example

```
CHEFS_BASE_URL=https://chefs-dev.apps.silver.devops.gov.bc.ca/app
CHEFS_FORM_LEGAL_FORMID=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
CHEFS_FORM_LEGAL_API_KEY=legal-api-key
CHEFS_FORM_NONLEGAL_FORMID=yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
CHEFS_FORM_NONLEGAL_API_KEY=nonlegal-api-key
```

The frontend calls `?formKey=legal` or `?formKey=nonlegal`. Form GUIDs and API keys remain server-side only.

CHEFS credentials are attached only to approved outbound form endpoints (`/app/api/v1/forms/{formId}/...` and `/app/gateway/v1/auth/token/forms/{formId}`). Other CHEFS paths are rejected instead of being sent with credentials.

### Adding a new form

The `legal`/`nonlegal` keys are hardcoded in the `api` service's `environment` block in [docker/docker-compose.yaml](../docker/docker-compose.yaml). To add another form:

1. Add `CHEFS_FORM_<KEY>_FORMID` and `CHEFS_FORM_<KEY>_API_KEY` to `docker/.env.template` and `docker/.env`.
2. Add a corresponding `Chefs__Forms__<key>__FormId` / `Chefs__Forms__<key>__ApiKey` entry to the `api` service's `environment` list in `docker/docker-compose.yaml`.
