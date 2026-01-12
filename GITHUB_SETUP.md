# GitHub Configuration for CI/CD

## Overview
The jag-probate repository is configured to automatically build and deploy to the tenant-gitops-a94b15 repository when you push to main.

## What You Need

### 1. Create a GitHub Personal Access Token (PAT)

You need a PAT that allows the jag-probate workflow to push to tenant-gitops-a94b15.

**Steps:**
1. Go to https://github.com/settings/tokens
2. Click **"Generate new token"** → **"Generate new token (classic)"**
3. Configure the token:
   - **Note:** `JAG Probate GitOps Deploy Token`
   - **Expiration:** Choose appropriate duration (recommend: 90 days minimum)
   - **Scopes:** Select these permissions:
     - ✅ `repo` (Full control of private repositories)
       - ✅ `repo:status`
       - ✅ `repo_deployment`
       - ✅ `public_repo`
       - ✅ `repo:invite`
       - ✅ `security_events`
4. Click **"Generate token"**
5. **⚠️ COPY THE TOKEN IMMEDIATELY** - you won't see it again!

### 2. Add Secret to jag-probate Repository

**Steps:**
1. Go to https://github.com/bcgov-c/jag-probate
2. Click **Settings** → **Secrets and variables** → **Actions**
3. Click **"New repository secret"**
4. Configure:
   - **Name:** `GITOPS_DEPLOY_TOKEN`
   - **Value:** Paste the PAT you created above
5. Click **"Add secret"**

### 3. Verify Workflow Permissions

The workflow needs permission to push container images to GitHub Container Registry.

**Steps:**
1. Go to https://github.com/bcgov-c/jag-probate/settings/actions
2. Under **"Workflow permissions"**, ensure:
   - ✅ **"Read and write permissions"** is selected
   - ✅ **"Allow GitHub Actions to create and approve pull requests"** (optional but recommended)
3. Click **"Save"** if you made changes

## How It Works

When you push to `main` branch in jag-probate:

1. **Trigger:** Workflow detects changes in `api/`, `db/`, `web/`, or `docker/` directories
2. **Build:** Creates Docker images for API and/or Web (only changed components)
3. **Push:** Pushes images to `ghcr.io/bcgov-c/jag-probate-{api|web}` with:
   - Tag: 7-character SHA (e.g., `a1b2c3d`)
   - Tag: `latest`
4. **Update GitOps:** Checks out tenant-gitops-a94b15 `develop` branch and updates:
   - `services/probate/kustomization.yaml` with new image tag
5. **Commit & Push:** Commits changes to GitOps repo
6. **ArgoCD Sync:** ArgoCD detects changes and deploys to OpenShift automatically

## Test the Setup

### Option 1: Make a small change
```bash
cd c:\Users\SalamatEmad\Source\probate\jag-probate
echo "# Test" >> README.md
git add README.md
git commit -m "test: Trigger CI/CD"
git push origin main
```

### Option 2: Manual trigger
1. Go to https://github.com/bcgov-c/jag-probate/actions/workflows/build-deploy-dev.yml
2. Click **"Run workflow"** → **"Run workflow"**

### Monitor the Deployment
1. **GitHub Actions:** https://github.com/bcgov-c/jag-probate/actions
2. **GitOps Repo:** https://github.com/bcgov-c/tenant-gitops-a94b15/commits/develop
3. **ArgoCD:** Check your ArgoCD UI for sync status

## Environments

| Branch | GitOps Branch | OpenShift Namespace | Purpose |
|--------|--------------|---------------------|---------|
| `main` | `develop` | `a94b15-dev` | Development |
| `test` | `test` | `a94b15-test` | Testing/QA |
| `prod` | `main` | `a94b15-prod` | Production |

**Note:** Currently only `main` → `develop` → `a94b15-dev` is configured. Add workflows for test/prod later.

## Troubleshooting

### "Resource not accessible by integration"
- Check workflow permissions (see step 3 above)
- Ensure `GITHUB_TOKEN` has `packages: write` permission

### "Authentication failed" when updating GitOps
- Verify `GITOPS_DEPLOY_TOKEN` secret exists and is valid
- Check PAT hasn't expired
- Ensure PAT has `repo` scope

### Images not updating in OpenShift
- Check ArgoCD sync status
- Verify image pull secrets exist in OpenShift namespace
- Check image names match in kustomization.yaml

### GitOps branch doesn't exist
```bash
cd c:\Users\SalamatEmad\Source\probate\گیت اپس\tenant-gitops-a94b15
git checkout -b develop
git push origin develop
```

## Next Steps

After configuring the secret:

1. ✅ Create `develop` branch in tenant-gitops-a94b15 (if not exists)
2. ✅ Commit and push jag-probate docker changes
3. ✅ Workflow will trigger automatically
4. ✅ Monitor GitHub Actions
5. ✅ Verify GitOps repo updated
6. ✅ Check ArgoCD sync
7. ✅ Test application in OpenShift

## Security Notes

- **Never commit the PAT to git**
- Rotate the token every 90 days
- Use the minimum required permissions
- Consider using GitHub Apps for better security (future enhancement)
