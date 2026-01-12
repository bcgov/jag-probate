# GitHub Container Registry (GHCR) Setup

## Error: "denied: permission_denied: The requested installation does not exist"

This means GitHub Actions doesn't have permission to push to GHCR for this repository.

## Fix Steps

### 1. Enable Package Write Permissions

**Go to:** https://github.com/bcgov-c/jag-probate/settings/actions

Scroll down to **"Workflow permissions"** section:

1. Select: ✅ **"Read and write permissions"**
2. Check: ✅ **"Allow GitHub Actions to create and approve pull requests"**
3. Click **"Save"**

### 2. Link Package to Repository (After First Manual Push)

After the first successful push, you'll need to link the packages to the repository:

1. Go to your profile packages: https://github.com/orgs/bcgov-c/packages
2. Find `jag-probate-api` and `jag-probate-web`
3. Click on each package → **"Package settings"**
4. Scroll to **"Manage Actions access"**
5. Add repository: `bcgov-c/jag-probate` with **"Write"** role

### 3. Alternative: Manual First Push (Recommended)

Sometimes you need to create the package manually first:

**Option A: Push from your local machine**

```powershell
# Log in to GHCR with your PAT
$env:GITHUB_TOKEN = "your-pat-here"
echo $env:GITHUB_TOKEN | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin

# Build and push API image
cd c:\Users\SalamatEmad\Source\probate\jag-probate
docker build -f docker/api/Dockerfile -t ghcr.io/bcgov-c/jag-probate-api:manual .
docker push ghcr.io/bcgov-c/jag-probate-api:manual

# Build and push Web image
docker build -f docker/web/Dockerfile -t ghcr.io/bcgov-c/jag-probate-web:manual .
docker push ghcr.io/bcgov-c/jag-probate-web:manual
```

**Option B: Check organization settings**

If you're in an organization (bcgov-c), check org-level settings:

1. Go to: https://github.com/organizations/bcgov-c/settings/actions
2. Under **"Fork pull request workflows"**, ensure Actions can access packages
3. Under **"Workflow permissions"**, ensure appropriate defaults are set

### 4. Verify Package Visibility

After first push:
1. Go to: https://github.com/orgs/bcgov-c/packages
2. Click on `jag-probate-api` → **"Package settings"**
3. Under **"Danger Zone"**, set visibility to **"Internal"** or **"Public"** as needed
4. Under **"Manage Actions access"**, verify `bcgov-c/jag-probate` has **"Write"** access

## Common Issues

### Issue: "installation does not exist"
**Fix:** You need to enable workflow permissions (Step 1 above)

### Issue: Package exists but push fails
**Fix:** Link package to repository and grant write access (Step 2 above)

### Issue: Organization restrictions
**Fix:** Organization admin needs to allow GHCR for the org (Step 3, Option B)

## Test After Setup

Re-run the failed workflow:
1. Go to: https://github.com/bcgov-c/jag-probate/actions
2. Find the failed workflow run
3. Click **"Re-run all jobs"**

Or trigger manually:
1. Go to: https://github.com/bcgov-c/jag-probate/actions/workflows/build-deploy-dev.yml
2. Click **"Run workflow"** → **"Run workflow"**

## Success Indicators

✅ Docker build completes
✅ Images push to ghcr.io/bcgov-c/jag-probate-{api,web}
✅ GitOps repo gets updated with new image tags
✅ ArgoCD syncs changes to OpenShift

## Quick Checklist

- [ ] Workflow permissions set to "Read and write" in repo settings
- [ ] Organization allows GHCR access (if applicable)
- [ ] Packages linked to repository with Write access (after first push)
- [ ] GITOPS_DEPLOY_TOKEN secret exists
- [ ] Re-run workflow to verify fix
