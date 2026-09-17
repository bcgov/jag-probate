# WeasyPrint PDF Generation Refactor

## Problem and approach

The API currently renders DOCX templates through the CDOGS-specific `ICDogsDelegate`, `ICDogsApi`, authentication handler, options, request models, configuration, and Docker environment variables. `POST /api/report/generate-from-submission` selects one hard-coded DOCX template and returns its CDOGS-produced PDF. The low-level `POST /api/report/generate` endpoint is unused.

Replace the implementation with a provider-neutral `IPDFGenerationService` contract and a concrete `WeasyPrintPDFService` implementation. The controller depends on `IPDFGenerationService`, while dependency injection binds it to `WeasyPrintPDFService`. This keeps a future PDF provider replacement small. Retain `POST /api/report/generate-from-submission` only. It will enrich data as it does today, load its ordered HTML template group from configuration, render each template with Fluid, and submit the ordered HTML documents to WeasyPrint's native `/multiple` endpoint. The configured template order must be passed through unchanged.

## Confirmed decisions

- **Route cleanup:** Remove unused `POST /api/report/generate`; do not replace its CDOGS request model with a generic model.
- **Future flexibility:** `IPDFGenerationService` is the generic application contract. `WeasyPrintPDFService` is its concrete implementation and is registered through that interface.
- **PDF assembly:** Use WeasyPrint's `POST /multiple` endpoint, with `IReadOnlyList<string>` request body, `filename` query parameter, and response content returned by the service. Do not add a PDF-merging dependency.
- **Mock templates:** Replace DOCX files with `P1.html`, `P9.html`, and `PGT.html`, whose bodies contain their respective literal filenames in an `h1`.
- **Scope boundary:** Template groups remain file-backed configuration for this iteration; no database design or migration is included.

## Implementation todos

1. **Define provider-neutral PDF generation contracts**
   - Add `IPDFGenerationService` and provider-neutral request/result models that represent ordered HTML documents, output filename, cancellation, PDF bytes, and the error semantics needed by the controller.
   - Replace CDOGS-branded result/error types or move reusable pieces to non-CDOGS namespaces so no remaining type, enum value, or error code is CDOGS-specific.
   - Update the report controller to depend only on `IPDFGenerationService`. Keep the submission route's validation, enrichment behavior, PDF response type, and filename behavior.
   - Remove the unused low-level `POST /api/report/generate` endpoint and its CDOGS-specific body model.

2. **Implement and register the WeasyPrint integration**
   - Add `WeasyPrintOptions` with a configurable base URL and validate it at startup following the repository's options pattern.
   - Add an `IWeasyPrintApi` Refit interface for `POST /multiple` using the supplied `IReadOnlyList<string>` JSON body and `filename` query parameter, returning the PDF response content.
   - Add `WeasyPrintPDFService` as the concrete `IPDFGenerationService` implementation, including structured logging, cancellation/error handling, binary response reads, and construction of the report result.
   - Add a `WeasyPrintServiceCollectionExtensions` registration method. Wire it into `Startup` so `IPDFGenerationService` resolves to `WeasyPrintPDFService`; remove CDOGS registration.

3. **Replace DOCX lookup with configured HTML template groups and Fluid rendering**
   - Replace the hard-coded template-key-to-DOCX map in `ITemplateService`/`TemplateService` with an options-backed `Dictionary<string, string[]>` (or equivalent strongly typed configuration) mapping each logical key to its ordered HTML filenames.
   - Validate template keys, group membership, relative filenames, containment below `api/Templates`, and missing files. Preserve case-insensitive key resolution and the existing safe-key controller validation.
   - Add a rendering method that parses each selected HTML template with Fluid and binds the already enriched `request.SubmissionData`; return the rendered documents in the exact configured sequence.
   - Configure the initial P1, P9, and PGT groups in `appsettings.json`.

4. **Decommission all CDOGS artifacts and update runtime configuration**
   - Remove the CDOGS infrastructure directory, CDOGS options, CDOGS request models, auth handler, error translator/service-type artifacts if no longer generally needed, and CDOGS imports/usages.
   - Remove the `CDogs` section from `appsettings.json`, `CDogs__*` Docker environment variables, and CDOGS credential documentation.
   - Add a `weasyprint` service to `docker/docker-compose.yaml` using `ghcr.io/bcgov/weasyprint:63.1.0`, mapped `8061:5001`, with `restart: unless-stopped`; configure the API to call it through a new `WeasyPrint__BaseUrl` environment value and declare its dependency as appropriate.
   - Update `api.csproj` so HTML template content is copied to the build output, delete the three DOCX template files, and add the three requested basic HTML fixtures.
   - Update `api/README.md` for the local WeasyPrint service and remove all CDOGS authentication guidance.

5. **Add focused unit coverage**
   - Add controller tests covering valid submission generation, invalid/missing keys, enriched PGT/P9 data passthrough, provider errors, content type, and file-name responses.
   - Add template-service tests for configured ordered groups, Fluid binding, unknown keys, unsafe/missing filenames, and missing files.
   - Add WeasyPrint service/Refit registration tests that verify configuration validation, client/service registration, `/multiple` request shape, ordered documents, filename query parameter, PDF response handling, cancellation, and non-success/error behavior.
   - Execute the narrow API test project, then resolve regressions caused by deleted CDOGS test dependencies or compile references.

## Files/components expected to change

| Area | Expected work |
|---|---|
| `api/Controllers/ReportController.cs` | Depend on `IPDFGenerationService`, render grouped templates for submissions, and remove the low-level route. |
| `api/Services/ITemplateService.cs`, `TemplateService.cs` | Configure, securely load, and Fluid-render ordered HTML template groups. |
| `api/Infrastructure/WeasyPrint/*`, `api/Infrastructure/Options/*` | New Refit client, concrete service, options, and DI registration. |
| `api/Infrastructure/CDogs/*`, `api/Models/CDogs/*` | Delete all CDOGS-specific code. |
| `api/Models/*` | Add provider-neutral generation contracts and generalize or replace CDOGS-labelled report errors. |
| `api/Startup.cs`, `api/appsettings.json`, `api/api.csproj` | Register the replacement, add template grouping/base URL configuration, and ship HTML files. |
| `api/Templates/*`, `docker/docker-compose.yaml`, `api/README.md` | Replace templates; add the container/configuration; document local runtime setup. |
| `api.tests/**/*` | Add unit coverage for the new contracts, rendering, controller flow, and HTTP registration. |

## Considerations

- The working tree may contain unrelated changes to CDOGS files and templates. Inspect and deliberately integrate them rather than overwrite them.
- Existing submission enrichment remains a pre-render concern and must retain its PGT/P9 behavior; only output template/rendering transport changes.