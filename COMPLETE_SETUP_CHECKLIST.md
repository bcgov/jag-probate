# Complete Setup Checklist for JAG Probate OpenShift Deployment

## Current State Analysis

### ✅ What's Already Done

1. **Docker Structure** - Jasper pattern implemented:
   - `docker/nginx-runtime/` - S2I runtime base
   - `docker/vue-on-nginx/` - S2I scripts and base URL fix
   - `docker/web/` - 3-stage Dockerfile (runtime → artifacts → final)
   - `docker/api/` - Standard .NET Dockerfile

2. **GitOps Structure** - tenant-gitops-a94b15:
   - `applications/argocd/` - ArgoCD application definitions (probate-dev/test/prod.yaml)
   - `services/probate/` - Kustomize configuration
   - `services/probate/resources/` - All Kubernetes manifests (deployments, services, ingresses, statefulset, PVC)
   - Three branches synced: main, develop, test

3. **GitHub Workflows** - Basic structure created but needs fixes
4. **CI/CD Workflows** - GitOps repo workflows fixed (Helm lint, Datree, registry login)

---

## ❌ What's WRONG (Critical Issues)

### 1. **GitHub Workflows Don't Follow Jasper Pattern Correctly**

**Current Issues:**
- API Dockerfile: Using `docker/api/Dockerfile` but jasper uses `docker/api/Dockerfile.release`
- Web Dockerfile: Using `docker/web/Dockerfile` but jasper uses `docker/web/Dockerfile.release`
- Missing: Separate `.dev` and `.release` Dockerfiles
- Build args: Not passing proper build-time arguments like jasper does

**Jasper Pattern:**
```
docker/api/
  ├── Dockerfile.dev      # For local development (docker-compose)
  └── Dockerfile.release  # For production (GitHub Actions)

docker/web/
  ├── Dockerfile.dev      # For local development
  └── Dockerfile.release  # For production builds
```

**What You Need:**
- Rename `docker/api/Dockerfile` → `docker/api/Dockerfile.release`
- Rename `docker/web/Dockerfile` → `docker/web/Dockerfile.release`
- Keep `Dockerfile.dev` files as they are
- Update workflows to use `.release` files

---

### 2. **Missing imagePullSecrets in Deployments**

**Problem:** OpenShift can't pull images from `ghcr.io/bcgov` without authentication.

**Required:** Create image pull secret in each OpenShift namespace:

```bash
# For each namespace: a94b15-dev, a94b15-test, a94b15-prod
oc create secret docker-registry ghcr-pull-secret \
  --docker-server=ghcr.io \
  --docker-username=<GITHUB_USERNAME> \
  --docker-password=<GITHUB_PAT> \
  --docker-email=<YOUR_EMAIL> \
  -n a94b15-dev

# Repeat for test and prod
```

**Then add to deployments:**
```yaml
spec:
  template:
    spec:
      imagePullSecrets:
        - name: ghcr-pull-secret
      containers:
        - name: probate-api
          image: ghcr.io/bcgov/jag-probate/api:abc1234
```

---

### 3. **Missing Database Secret in OpenShift**

**Problem:** API deployment references `probate-db-secret` but it doesn't exist.

**Create in each namespace:**
```bash
oc create secret generic probate-db-secret \
  --from-literal=connection-string="Host=probate-db;Port=5432;Database=probate;Username=probate;Password=CHANGE_ME" \
  -n a94b15-dev
```

**Note:** Get the actual generated password from the StatefulSet after first deployment:
```bash
# After db pod starts
oc get secret probate-db-secret -n a94b15-dev -o yaml
```

---

### 4. **Missing ArgoCD Application Deployment**

**Problem:** ArgoCD application definitions exist but aren't applied to OpenShift.

**Apply to OpenShift:**
```bash
# Must be done in the OpenShift cluster's ArgoCD namespace
oc apply -f applications/argocd/probate-dev.yaml -n <argocd-namespace>
oc apply -f applications/argocd/probate-test.yaml -n <argocd-namespace>
oc apply -f applications/argocd/probate-prod.yaml -n <argocd-namespace>
```

**Ask your platform team:**
- What is the ArgoCD namespace name?
- Do you have access to create ArgoCD applications?
- Or do they need to create it for you?

---

### 5. **GitHub Repository Secret Missing**

**Status:** You said you have a PAT - need to add it.

**Add to jag-probate repository:**
1. Go to https://github.com/bcgov/jag-probate/settings/secrets/actions
2. Create secret: `GITOPS_DEPLOY_TOKEN`
3. Value: Your PAT with `repo` scope

---

### 6. **GitHub Workflow Permissions**

**Enable at:** https://github.com/bcgov/jag-probate/settings/actions

**Settings:**
- ✅ Read and write permissions
- ✅ Allow GitHub Actions to create and approve pull requests

---

## 📋 Complete Setup Steps (In Order)

### Phase 1: Fix Docker Files (jag-probate repo)

```bash
cd c:\Users\SalamatEmad\Source\probate\jag-probate

# Rename Dockerfiles to match jasper pattern
mv docker/api/Dockerfile docker/api/Dockerfile.release
mv docker/web/Dockerfile docker/web/Dockerfile.release

# Verify .dev files exist
ls docker/api/Dockerfile.dev
ls docker/web/Dockerfile.dev
```

### Phase 2: Fix GitHub Workflows (jag-probate repo)

**Update publish-api.yml:**
```yaml
file: ./docker/api/Dockerfile.release  # Change from Dockerfile
```

**Update publish-web.yml:**
```yaml
file: ./docker/web/Dockerfile.release  # Change from Dockerfile
```

### Phase 3: Update GitOps Deployments (tenant-gitops-a94b15 repo)

**Add to `services/probate/resources/api-deployment.yaml`:**
```yaml
spec:
  template:
    spec:
      imagePullSecrets:
        - name: ghcr-pull-secret
      containers:
        - name: probate-api
          image: ghcr.io/bcgov/jag-probate/api:latest  # Will be updated by kustomize
```

**Add to `services/probate/resources/web-deployment.yaml`:**
```yaml
spec:
  template:
    spec:
      imagePullSecrets:
        - name: ghcr-pull-secret
      containers:
        - name: probate-web
          image: ghcr.io/bcgov/jag-probate/web:latest  # Will be updated by kustomize
```

### Phase 4: Commit All Changes

```bash
# Commit jag-probate changes
cd c:\Users\SalamatEmad\Source\probate\jag-probate
git add .
git commit -m "✨ Follow jasper pattern: separate .dev and .release Dockerfiles"
git push origin main

# Commit tenant-gitops-a94b15 changes
cd "c:\Users\SalamatEmad\Source\probate\گیت اپس\tenant-gitops-a94b15"
git add services/probate/
git commit -m "✨ Add imagePullSecrets and fix image references"

# Push to all branches
git push origin main
git checkout develop && git merge main && git push origin develop
git checkout test && git merge main && git push origin test
git checkout main
```

### Phase 5: GitHub Configuration

1. **Add Secret:**
   - Go to https://github.com/bcgov/jag-probate/settings/secrets/actions
   - Create: `GITOPS_DEPLOY_TOKEN` = your PAT

2. **Enable Workflow Permissions:**
   - Go to https://github.com/bcgov/jag-probate/settings/actions
   - Select: "Read and write permissions"
   - Save

### Phase 6: OpenShift Configuration

**6.1 Create image pull secrets (for each namespace):**
```bash
# Dev
oc create secret docker-registry ghcr-pull-secret \
  --docker-server=ghcr.io \
  --docker-username=YOUR_GITHUB_USERNAME \
  --docker-password=YOUR_PAT \
  --docker-email=YOUR_EMAIL \
  -n a94b15-dev

# Test
oc create secret docker-registry ghcr-pull-secret \
  --docker-server=ghcr.io \
  --docker-username=YOUR_GITHUB_USERNAME \
  --docker-password=YOUR_PAT \
  --docker-email=YOUR_EMAIL \
  -n a94b15-test

# Prod
oc create secret docker-registry ghcr-pull-secret \
  --docker-server=ghcr.io \
  --docker-username=YOUR_GITHUB_USERNAME \
  --docker-password=YOUR_PAT \
  --docker-email=YOUR_EMAIL \
  -n a94b15-prod
```

**6.2 Create database secrets:**
```bash
# Dev
oc create secret generic probate-db-secret \
  --from-literal=connection-string="Host=probate-db;Port=5432;Database=probate;Username=probate;Password=$(openssl rand -base64 32)" \
  -n a94b15-dev

# Test
oc create secret generic probate-db-secret \
  --from-literal=connection-string="Host=probate-db;Port=5432;Database=probate;Username=probate;Password=$(openssl rand -base64 32)" \
  -n a94b15-test

# Prod
oc create secret generic probate-db-secret \
  --from-literal=connection-string="Host=probate-db;Port=5432;Database=probate;Username=probate;Password=$(openssl rand -base64 32)" \
  -n a94b15-prod
```

**6.3 Apply ArgoCD Applications:**
```bash
# Find ArgoCD namespace (ask platform team or check)
ARGOCD_NS="<argocd-namespace>"  # Usually 'argocd' or 'gitops'

# Apply applications
oc apply -f applications/argocd/probate-dev.yaml -n $ARGOCD_NS
oc apply -f applications/argocd/probate-test.yaml -n $ARGOCD_NS
oc apply -f applications/argocd/probate-prod.yaml -n $ARGOCD_NS
```

### Phase 7: Test Deployment

1. **Trigger GitHub Actions:**
   ```bash
   # Make a small change to trigger workflows
   cd c:\Users\SalamatEmad\Source\probate\jag-probate
   echo "# Test" >> README.md
   git add README.md
   git commit -m "test: Trigger CI/CD"
   git push origin main
   ```

2. **Monitor Progress:**
   - **GitHub Actions:** https://github.com/bcgov/jag-probate/actions
   - **GitOps Commits:** https://github.com/bcgov-c/tenant-gitops-a94b15/commits/develop
   - **ArgoCD UI:** Check your ArgoCD dashboard

3. **Verify in OpenShift:**
   ```bash
   # Check if ArgoCD synced
   oc get applications -n $ARGOCD_NS | grep probate

   # Check pods in dev namespace
   oc get pods -n a94b15-dev

   # Check image versions
   oc describe deployment probate-api -n a94b15-dev | grep Image
   oc describe deployment probate-web -n a94b15-dev | grep Image
   ```

---

## 🎯 Critical Questions You Must Answer

### Question 1: ArgoCD Namespace
**What is the ArgoCD namespace in your OpenShift cluster?**
- Common names: `argocd`, `gitops`, `openshift-gitops`
- Ask: Platform team or cluster admin

### Question 2: ArgoCD Access
**Do you have permission to create ArgoCD applications?**
- YES → You can apply the YAML files yourself
- NO → Platform team needs to apply them for you

### Question 3: OpenShift Access
**Can you create secrets in a94b15-dev/test/prod namespaces?**
- YES → Follow Phase 6 steps
- NO → Request secrets from platform team

### Question 4: GitHub PAT Scope
**Does your PAT have these permissions?**
- ✅ `repo` (full repository access)
- ✅ `write:packages` (push to ghcr.io)
- ✅ `read:org` (if bcgov is an organization)

---

## 📝 Summary of What You Need to Do NOW

1. ✅ Rename Dockerfiles to `.release` pattern
2. ✅ Update workflow files to use `.release` Dockerfiles
3. ✅ Add `imagePullSecrets` to deployment manifests
4. ✅ Fix image names to include full registry path
5. ✅ Commit and push all changes
6. ✅ Add `GITOPS_DEPLOY_TOKEN` secret to GitHub
7. ✅ Enable workflow permissions in GitHub
8. ⚠️ Create `ghcr-pull-secret` in OpenShift namespaces (need credentials)
9. ⚠️ Create `probate-db-secret` in OpenShift namespaces
10. ⚠️ Apply ArgoCD applications (need ArgoCD namespace name + access)

**Items with ⚠️ require OpenShift access and information from platform team.**

---

## 🚀 Expected Flow After Setup

1. **Developer pushes to main** in jag-probate
2. **GitHub Actions** builds Docker images, pushes to `ghcr.io/bcgov/jag-probate/{api|web}:abc1234`
3. **GitHub Actions** updates `tenant-gitops-a94b15/services/probate/kustomization.yaml` with new image tag
4. **ArgoCD** detects GitOps repo change
5. **ArgoCD** applies changes to OpenShift
6. **OpenShift** pulls images from ghcr.io using `ghcr-pull-secret`
7. **Rolling update** deploys new version
8. **Application runs** with new code

---

## ❓ What to Ask Platform Team

1. "What is the ArgoCD namespace in Emerald cluster?"
2. "Do I have permission to create ArgoCD Application resources?"
3. "Can I create secrets in a94b15-dev/test/prod namespaces?"
4. "Is there a standard way to create image pull secrets for GitHub Container Registry?"

Tell me when you're ready to execute these changes step by step!
