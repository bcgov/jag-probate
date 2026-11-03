# CHEFS Integration

The API integrates with [CHEFS (Common Hosted Form Service)](https://developer.gov.bc.ca/docs/default/component/chefs-techdocs/) to provide web-component auth tokens and the sidebar structure. Application data is stored only in our own database; the API does not create, update or delete CHEFS submissions. Credentials are configured per logical form; the API sends Basic Authentication using the CHEFS form GUID and that form's API key when requesting a token.

## Configuration

Copy `docker/.env.template` to `docker/.env` and set each form's variables. `docker-compose.yaml` maps these `.env` values onto the ASP.NET Core `Chefs__Forms__<key>__*` configuration keys.

| `.env` variable | Config key | Description |
| --- | --- | --- |
| `CHEFS_BASE_URL` | `Chefs__BaseUrl` | CHEFS app base URL |
| `CHEFS_FORM_<KEY>_FORMID` | `Chefs__Forms__<key>__FormId` | Actual CHEFS form GUID |
| `CHEFS_FORM_<KEY>_API_KEY` | `Chefs__Forms__<key>__ApiKey` | API key for that form |

Example: `CHEFS_FORM_DECEASED_FORMID` maps to `Chefs__Forms__form-deceased__FormId`.

Form GUIDs and API keys stay server-side. CHEFS credentials are attached only to approved form endpoints; other paths are rejected.

## Legacy CHEFS submissions

The `submissions.chefs_submission_id` column was dropped by migration `DropChefsSubmissionIdFromSubmissions`. Back up or export any IDs you still need before applying that migration to a shared environment; CHEFS submissions created by the earlier direct-to-CHEFS flow must be cleaned up manually through CHEFS admin tooling.
