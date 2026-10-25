# CHEFS Integration

The API integrates with [CHEFS (Common Hosted Form Service)](https://developer.gov.bc.ca/docs/default/component/chefs-techdocs/) for form authentication, submission creation, and application listings. Credentials are configured per logical form; the API sends Basic Authentication using the CHEFS form GUID and that form's API key.

## Configuration

Copy `docker/.env.template` to `docker/.env` and set each form's variables. `docker-compose.yaml` maps these `.env` values onto the ASP.NET Core `Chefs__Forms__<key>__*` configuration keys.

| `.env` variable | Config key | Description |
| --- | --- | --- |
| `CHEFS_BASE_URL` | `Chefs__BaseUrl` | CHEFS app base URL |
| `CHEFS_FORM_<KEY>_FORMID` | `Chefs__Forms__<key>__FormId` | Actual CHEFS form GUID |
| `CHEFS_FORM_<KEY>_API_KEY` | `Chefs__Forms__<key>__ApiKey` | API key for that form |

Example: `CHEFS_FORM_DECEASED_FORMID` maps to `Chefs__Forms__form-deceased__FormId`.

Form GUIDs and API keys stay server-side. CHEFS credentials are attached only to approved form endpoints; other paths are rejected.

## Per-step submissions

When an application is finalized, API submits each saved step to its configured CHEFS form and records returned CHEFS submission ID and actual form ID on that step's `StepData` row. Retries skip steps with a recorded pair and retry steps whose create did not complete.

Deleting an application walks recorded step ID/form pairs. Successful deletes and CHEFS 404 responses clear each pair locally. Other failures remain recorded and return an error; retrying delete retries only remaining pairs.

## Legacy parent-level CHEFS submissions

Older records may have a CHEFS submission ID only in `submissions.chefs_submission_id`; they do not have a CHEFS form ID or per-step IDs. New deletion logic cannot safely infer their form. Clean these records before removing legacy form credentials:

1. Pause writes to the legacy application. Before deploying a release without legacy credentials, query the production database for non-empty parent-level IDs:

   ```sql
   SELECT public_id, chefs_submission_id
   FROM submissions
   WHERE chefs_submission_id IS NOT NULL
     AND chefs_submission_id <> '';
   ```

2. For each returned ID, delete it from CHEFS using the legacy form GUID and credentials held in the approved secret store. Use CHEFS admin tooling or `DELETE /api/v1/forms/{formId}/submissions/{submissionId}`. Confirm each deletion returns success or 404; do not treat the old application's delete response as confirmation, since older releases may suppress CHEFS errors.
3. After verifying CHEFS deletions, clear only the matching parent IDs from the local database. Use the verified public IDs from the cleanup export:

   ```sql
   UPDATE submissions
   SET chefs_submission_id = ''
   WHERE public_id = ANY (
     ARRAY['<verified-public-id-1>'::uuid, '<verified-public-id-2>'::uuid]
   );
   ```

   Replace UUID placeholders with the verified public IDs. Keep an export of the ID list and deletion results as the cleanup record.
4. Verify the query returns no rows, then deploy the credential-free release and remove legacy credentials from deployment secrets.

Do not use the new per-step deletion path for these records before cleanup; it intentionally does not guess a CHEFS form ID from the legacy parent field.
