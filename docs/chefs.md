# CHEFS Integration

The API integrates with [CHEFS (Common Hosted Form Service)](https://developer.gov.bc.ca/docs/default/component/chefs-techdocs/) to retrieve form submissions (applications) for the dashboard. CHEFS credentials are configured per form; the API sends Basic Authentication using the form GUID and that form's API key.

## Endpoint

`GET /api/chefs/applications?formKey={formKey}`

Returns current and previous applications (submissions) for the given form. Requires authentication.

`formKey` is a logical identifier (e.g. `probate`) configured server-side. The backend resolves it to the actual CHEFS form GUID — the GUID is never exposed to the frontend.

**Error handling:** CHEFS API errors (4xx/5xx or unreachable) are returned as Problem Details (e.g. `502 Bad Gateway` with a descriptive message).

## Configuration

Copy `docker/.env.template` to `docker/.env` and set the following variables:

| Variable | Description |
|---|---|
| `Chefs__BaseUrl` | CHEFS API base URL (e.g. `https://chefs-dev.apps.silver.devops.gov.bc.ca/app/api/v1`) |
| `Chefs__Forms__<key>__FormId` | Maps a logical form key to its CHEFS form GUID. Add one entry per form. |
| `Chefs__Forms__<key>__ApiKey` | API key for that CHEFS form. Add one entry per form. |

### Example: two forms

```
Chefs__BaseUrl=https://chefs-dev.apps.silver.devops.gov.bc.ca/app/api/v1
Chefs__Forms__legal__FormId=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
Chefs__Forms__legal__ApiKey=legal-api-key
Chefs__Forms__nonlegal__FormId=yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
Chefs__Forms__nonlegal__ApiKey=nonlegal-api-key
```

The frontend calls `?formKey=legal` or `?formKey=nonlegal`. Form GUIDs and API keys remain server-side only.
