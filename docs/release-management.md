# Release management

Releases are managed from `bcgov/jag-probate` with the `Release` GitHub Actions workflow.

## Flow

```text
GitHub Release or manual workflow_dispatch
  -> build API and web images tagged from the release tag
  -> update tenant-gitops develop branch for dev
  -> wait for GitHub Environment approval for test
  -> update tenant-gitops test branch for test
  -> wait for GitHub Environment approval for prod
  -> update tenant-gitops main branch for prod
```

Pre-release tags, such as `v1.2.3-rc1`, deploy to dev and test only. Full release tags, such as `v1.2.3`, deploy through prod.

## GitOps mapping

The workflow updates `bcgov-c/tenant-gitops-a94b15` using the existing `update-argo-kustomize` action.

| Environment | GitHub Environment | GitOps branch | Kustomize path |
|---|---|---|---|
| Dev | `dev` | `develop` | `services/probate/overlays/dev` |
| Test | `test` | `test` | `services/probate/overlays/test` |
| Prod | `prod` | `main` | `services/probate/overlays/prod` |

The image tags are semantic versions derived from the release tag. For example, `v1.2.3` becomes `1.2.3`.

## Required app repo setup

Configure these in `bcgov/jag-probate`:

1. GitHub Environments:
   - `dev`: no required reviewers.
   - `test`: required reviewers enabled.
   - `prod`: required reviewers enabled.
2. Actions secret:
   - `GITOPS_DEPLOY_TOKEN`: can push to `bcgov-c/tenant-gitops-a94b15` branches `develop`, `test`, and `main`.

Prefer a machine-user fine-grained PAT or GitHub App token with `contents: write` on `bcgov-c/tenant-gitops-a94b15`.

## Required tenant GitOps setup

Use these settings in `bcgov-c/tenant-gitops-a94b15`:

1. Keep the existing overlay structure under `services/probate/overlays/{dev,test,prod}`.
2. Protect `test` and `main` so humans cannot bypass the release workflow with direct pushes.
3. Ensure the deploy automation actor can bypass pull-request requirements and push image-tag updates.
4. Decide the prod sync gate:
   - If GitHub Environment approval is the sole prod gate, enable ArgoCD auto-sync on `probate-prod`.
   - If ArgoCD is required as a second prod gate, keep prod manual-sync and document the manual sync step.

## Tenant prod auto-sync option

If GitHub Environment approval is accepted as the production gate, update `applications/argocd/probate-prod.yaml` in the tenant repo:

```yaml
syncPolicy:
  automated:
    prune: true
    selfHeal: true
  syncOptions:
    - CreateNamespace=false
```

This matches dev and test behaviour. Do not enable it if a second manual ArgoCD gate is required.

## Running a release

1. Create and publish a GitHub Release with a tag like `v1.2.3`.
2. Watch the `Release` workflow.
3. Approve the `test` environment job.
4. Approve the `prod` environment job.
5. Verify ArgoCD reports the target applications as `Synced` and `Healthy`.

For a release candidate, publish a pre-release tag like `v1.2.3-rc1`. Prod is skipped.
