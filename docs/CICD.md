# CI/CD Pipeline Documentation

This document describes the GitHub Actions workflows for building, testing, and publishing the `AgentSdk` NuGet package.

## Workflows Overview

| Workflow | File | Purpose |
|----------|------|---------|
| CI | `.github/workflows/ci.yml` | Build and test on pull requests |
| Publish | `.github/workflows/publish.yml` | Build, test, and publish to NuGet.org |

## Branching Strategy

The project follows a GitFlow-inspired branching model:

```
main (stable releases)
  ↑
dev (preview/alpha releases)
  ↑
feature/* (development work)
```

### Branch Types

| Branch | Purpose | Merges To |
|--------|---------|-----------|
| `main` | Stable production releases | - |
| `dev` | Integration branch for preview releases | `main` |
| `feature/*` | New features and changes | `dev` |
| `release/*` | Release candidates | `main` |
| `hotfix/*` | Emergency fixes | `main` |

## Version Strategy

Versioning is managed automatically by [GitVersion](https://gitversion.net/) based on the branch and commit history.

### Version Examples

| Branch | Version Format | Example |
|--------|----------------|---------|
| `main` | `X.Y.Z` | `1.0.0`, `1.0.1` |
| `dev` | `X.Y.Z-alpha.N` | `1.1.0-alpha.1`, `1.1.0-alpha.5` |
| `feature/my-feature` | `X.Y.Z-my-feature.N` | `1.1.0-my-feature.1` |
| `release/1.2.0` | `X.Y.Z-rc.N` | `1.2.0-rc.1` |
| `hotfix/fix-bug` | `X.Y.Z-hotfix.N` | `1.0.1-hotfix.1` |
| Pull Request | `X.Y.Z-prN.N` | `1.1.0-pr123.1` |

### Version Bumping

You can control version bumps using commit message tags:

| Commit Message | Effect |
|----------------|--------|
| `+semver: major` or `+semver: breaking` | Bump major version |
| `+semver: minor` or `+semver: feature` | Bump minor version |
| `+semver: patch` or `+semver: fix` | Bump patch version |
| `+semver: none` or `+semver: skip` | No version bump |

## CI Workflow

**Trigger:** Pull requests to `main` or `dev` branches

### Steps

1. **Checkout** - Clone repository with full history
2. **Setup .NET** - Install .NET 8.0 SDK
3. **Restore Tools** - Restore GitVersion and other .NET tools
4. **Determine Version** - Calculate version using GitVersion
5. **Restore Dependencies** - Restore NuGet packages
6. **Build** - Build solution in Release configuration
7. **Test** - Run unit tests with code coverage
8. **Pack (Validation)** - Create NuGet packages (not published)
9. **Upload Artifacts** - Store packages for 7 days

## Publish Workflow

**Triggers:**
- Push to `main` branch → Publishes stable packages
- Push to `dev` branch → Publishes preview/alpha packages
- Manual trigger → Configurable options

### Jobs

#### 1. Build & Pack

- Builds the solution
- Runs all tests
- Creates NuGet packages (`.nupkg` and `.snupkg`)
- Uploads artifacts

#### 2. Publish to NuGet.org

- Downloads built packages
- Publishes to NuGet.org using API key
- Skips duplicate versions

#### 3. Create GitHub Release (main only)

- Creates a GitHub release with tag
- Attaches NuGet packages
- Auto-generates release notes

### Manual Trigger Options

| Option | Default | Description |
|--------|---------|-------------|
| `publish_nuget` | `true` | Publish packages to NuGet.org |
| `create_release` | `false` | Create a GitHub release |

## Trusted Publishing (OIDC)

This project uses **Trusted Publishing** for NuGet.org authentication instead of long-lived API keys. This provides:

- **No secrets to manage** - No API keys to rotate or risk leaking
- **Short-lived credentials** - Tokens are valid for only 1 hour
- **Cryptographic verification** - GitHub Actions signs tokens that NuGet.org verifies

### How It Works

1. GitHub Actions workflow runs and requests an OIDC token
2. The token is cryptographically signed by GitHub
3. Workflow sends token to NuGet.org
4. NuGet.org validates the token against your Trusted Publishing policy
5. NuGet.org issues a temporary API key (valid 1 hour)
6. Workflow uses the temporary key to push packages

### Workflow Configuration

The publish workflow uses the `nuget/login@v1` action:

```yaml
- name: NuGet Login (OIDC → Trusted Publishing)
  uses: nuget/login@v1
  with:
    user: ${{ secrets.NUGET_USER }}

- name: Publish to NuGet.org
  run: |
    dotnet nuget push "*.nupkg" \
      --source https://api.nuget.org/v3/index.json \
      --skip-duplicate
```

> **Important:** The `id-token: write` permission must be set for the job to enable OIDC token issuance.

## Setup Instructions

### 1. Configure Trusted Publishing on NuGet.org

Trusted Publishing uses OIDC tokens instead of long-lived API keys, making your publishing process more secure.

1. Log into [nuget.org](https://www.nuget.org)
2. Click your username → **Trusted Publishing**
3. Click **Add new trusted publishing policy**
4. Configure the policy:
   - **Repository Owner:** `cyclotron-azure`
   - **Repository:** `maf-agent-sdk`
   - **Workflow File:** `publish.yml`
   - **Environment:** `nuget` (optional, for additional protection)
5. Click **Save**

> **Note:** For private repositories, the policy starts as "temporarily active" for 7 days. After a successful publish, it becomes permanently active.

### 2. Add GitHub Repository Secret

You need to store your NuGet.org username (profile name, NOT email) as a secret:

1. Go to your repository on GitHub
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Add:
   - **Name:** `NUGET_USER`
   - **Value:** Your NuGet.org profile name (username)

### 3. Create GitHub Environment (Optional but Recommended)

For additional protection (approvals, deployment rules):

1. Go to **Settings** → **Environments**
2. Click **New environment**
3. Name it `nuget`
4. Configure protection rules:
   - Required reviewers
   - Wait timer
   - Deployment branches (limit to `main` and `dev`)

### 4. Create the `dev` Branch

```bash
git checkout main
git pull origin main
git checkout -b dev
git push -u origin dev
```

### 5. Configure Branch Protection (Recommended)

For `main` branch:
- Require pull request reviews
- Require status checks (CI workflow)
- Require branches to be up to date

For `dev` branch:
- Require status checks (CI workflow)

## Package Contents

The NuGet package includes:

| Item | Path in Package |
|------|-----------------|
| README | `README.md` |
| Logo | `logo.jpeg` |
| Library | `lib/net8.0/Cyclotron.Maf.AgentSdk.dll` |
| XML Docs | `lib/net8.0/Cyclotron.Maf.AgentSdk.xml` |
| Symbols | Separate `.snupkg` package |

## Dependency Updates

Dependabot is configured to automatically create PRs for:

- **NuGet packages** - Weekly on Mondays
- **GitHub Actions** - Weekly on Mondays
- **.NET tools** - Monthly

Package updates are grouped by:
- Azure SDK packages
- Microsoft.Extensions packages
- Microsoft.Agents packages
- OpenTelemetry packages

## Troubleshooting

### GitVersion Not Finding Version

Ensure full git history is fetched:
```yaml
- uses: actions/checkout@v4
  with:
    fetch-depth: 0
```

### Package Already Exists

The publish step uses `--skip-duplicate` to handle this gracefully.

### Tests Failing in CI

Run tests locally with the same configuration:
```bash
dotnet test --configuration Release --verbosity normal
```

### Version Not Incrementing

Check commit messages for version bump tags, or create a git tag:
```bash
git tag v1.0.0
git push origin v1.0.0
```

## Related Files

| File | Description |
|------|-------------|
| `GitVersion.yaml` | GitVersion configuration |
| `Directory.Build.props` | Centralized build properties |
| `Directory.Packages.props` | Centralized package versions |
| `.github/dependabot.yml` | Dependabot configuration |
