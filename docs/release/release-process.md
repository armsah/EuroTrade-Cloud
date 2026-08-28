# EuroTrade Cloud Release Process

## Purpose

EuroTrade Cloud uses GitHub Actions to provide a repeatable release process with automated build validation, testing, security analysis, Software Bill of Materials (SBOM) generation, integrity checks, and artifact attestations.

The release process is intentionally independent of the live Azure environment. A release can be built and validated without provisioning AKS, Azure Database for PostgreSQL, Azure Service Bus, Azure Key Vault, Azure Container Registry, or Application Insights.

## Release Principles

The release process follows these principles:

- Releases originate from a validated `main` branch.
- Releases use immutable Git tags.
- Release builds use the `Release` configuration.
- Compiler warnings are treated as errors during the build.
- Automated tests must pass before release artifacts are published.
- Integration tests run against an ephemeral PostgreSQL instance provided by Docker Compose.
- Database migrations are validated as part of the release workflow.
- Every successful release includes an SPDX SBOM.
- Release artifacts have SHA-256 checksums.
- GitHub artifact attestations provide build provenance and SBOM integrity evidence.
- Failed release tags are preserved rather than moved or overwritten.
- Live Azure infrastructure is not required to produce a release.

## GitHub Actions Workflows

EuroTrade Cloud uses separate workflows for continuous integration, security validation, and releases.

### CI

The CI workflow is defined in:

```text
.github/workflows/ci.yml
```

It runs for pushes and pull requests targeting `main`.

The workflow performs:

1. Repository checkout.
2. .NET SDK setup.
3. Local .NET tool restoration.
4. NuGet dependency restoration.
5. Release build with warnings treated as errors.
6. PostgreSQL startup through Docker Compose.
7. PostgreSQL readiness validation.
8. EF Core database migration.
9. Automated test execution.
10. Test-result artifact upload.
11. PostgreSQL and volume cleanup.

The workflow uses explicit GitHub token permissions and concurrency control to reduce unnecessary overlapping CI runs.

## Security Validation

The security workflow is defined in:

```text
.github/workflows/security.yml
```

Security validation includes CodeQL analysis for C# and dependency review for pull requests.

CodeQL runs against the source code to identify potentially vulnerable coding patterns.

Dependency review evaluates dependency changes introduced by pull requests and is configured to reject newly introduced dependencies with vulnerabilities meeting the configured severity threshold.

The security workflow also runs on a schedule so the repository can be re-evaluated as security intelligence changes.

## Release Trigger

The release workflow is defined in:

```text
.github/workflows/release.yml
```

Releases are triggered by Git tags matching:

```text
v*.*.*
```

The project uses semantic-style version tags:

```text
vMAJOR.MINOR.PATCH
```

Examples:

```text
v1.0.0
v1.0.1
v1.0.2
```

## Release Preconditions

Before creating a release, verify that:

1. `main` is synchronized with the remote repository.
2. CI is green.
3. Security validation is green.
4. The local working tree is clean.
5. The intended release commit is present on `main`.

Run:

```powershell
git status
git pull --ff-only origin main
git log --oneline -5
```

Do not create a release tag from an unvalidated local commit.

## Creating a Release

Create an annotated tag from the validated `main` commit.

For example:

```powershell
git tag -a v1.0.2 -m "EuroTrade Cloud v1.0.2"
git push origin v1.0.2
```

Pushing the tag automatically starts the GitHub Actions Release workflow.

The release tag must not subsequently be moved to another commit.

## Release Pipeline

The tagged release workflow performs the following sequence.

### 1. Checkout

The repository and Git history are checked out so the workflow operates on the exact commit referenced by the release tag.

### 2. .NET Environment

The required .NET SDK is installed on the GitHub-hosted runner.

Local .NET tools are restored with:

```text
dotnet tool restore
```

This makes repository-managed tools such as `dotnet-ef` available to subsequent release steps.

### 3. Dependency Restore

NuGet dependencies are restored for the solution.

```text
dotnet restore EuroTrade-Cloud.sln
```

### 4. Release Build

The complete solution is built using the `Release` configuration.

Warnings are treated as errors:

```text
dotnet build EuroTrade-Cloud.sln --no-restore --configuration Release --warnaserror
```

A compiler warning therefore prevents release publication rather than silently entering a release.

### 5. Ephemeral PostgreSQL

The workflow starts PostgreSQL using Docker Compose.

This database exists only for workflow validation and does not require Azure Database for PostgreSQL.

The workflow waits until PostgreSQL reports that it is accepting connections before continuing.

### 6. Database Migration Validation

EF Core migrations are applied to the ephemeral PostgreSQL database.

This verifies that the migration chain can be executed successfully before a release is published.

### 7. Automated Tests

The complete solution test suite is executed against the release build.

This includes the project's unit, architecture, integration, and end-to-end test projects.

A failing test prevents subsequent release publication.

### 8. Environment Cleanup

The PostgreSQL container and associated Docker volumes are removed after test execution.

Cleanup runs even when the test stage fails.

### 9. API Publication

The EuroTrade API is published using the .NET `Release` configuration into the workflow's artifact directory.

### 10. Release Archive

The published API is packaged into a versioned compressed archive.

For `v1.0.2`, the archive is:

```text
eurotrade-api-v1.0.2-linux-x64.tar.gz
```

### 11. SBOM Generation

The workflow generates an SPDX JSON Software Bill of Materials for the published application.

For `v1.0.2`:

```text
eurotrade-api-v1.0.2.spdx.json
```

The SBOM provides machine-readable information about the software components contained in the release.

### 12. SHA-256 Checksums

The workflow calculates SHA-256 hashes for the release archive and SBOM.

The hashes are stored in:

```text
SHA256SUMS
```

These hashes allow downloaded release artifacts to be checked for integrity.

### 13. Build Provenance Attestation

GitHub Actions creates a build provenance attestation for the release archive.

The attestation associates the artifact with the GitHub Actions workflow and source revision that produced it.

### 14. SBOM Attestation

The workflow also creates an SBOM attestation associating the generated SPDX SBOM with the release archive.

### 15. Workflow Artifact Upload

The release archive, SBOM, and checksum file are retained as GitHub Actions workflow artifacts.

### 16. GitHub Release

After all preceding validation steps succeed, the workflow creates the GitHub Release and attaches the release assets.

For `v1.0.2`, the expected assets are:

```text
eurotrade-api-v1.0.2-linux-x64.tar.gz
eurotrade-api-v1.0.2.spdx.json
SHA256SUMS
```

## Artifact Integrity Verification

After downloading a release and its checksum file on Linux, the published hashes can be checked using:

```bash
sha256sum -c SHA256SUMS
```

The calculated digest must match the value recorded during the release workflow.

The GitHub build provenance and SBOM attestations provide an additional supply-chain integrity mechanism beyond the checksum.

## Failed Release Policy

Release tags are treated as immutable.

If a tagged release workflow fails:

1. Do not force-update the existing tag.
2. Diagnose the workflow failure.
3. Fix the problem on `main`.
4. Push the correction.
5. Wait for CI and security workflows to pass.
6. Create a new patch release tag.
7. Run the release process again.

The initial P11 implementation demonstrated this policy.

```text
v1.0.0 -> release validation failed
v1.0.1 -> release validation failed
v1.0.2 -> release validation passed
```

The failed tags remain part of the repository history instead of being rewritten.

This provides an auditable record of the release-pipeline improvements.

## v1.0.2 Validation

`v1.0.2` is the first successfully validated release produced by the hardened P11 release process.

The release demonstrated:

- CI validation: PASS
- Security workflow: PASS
- Release workflow: PASS
- Release build with warnings as errors: PASS
- Ephemeral PostgreSQL startup: PASS
- EF Core migration validation: PASS
- Automated tests: PASS
- API publication: PASS
- SPDX SBOM generation: PASS
- SHA-256 checksum generation: PASS
- Build provenance attestation: PASS
- SBOM attestation: PASS
- GitHub workflow artifact publication: PASS
- GitHub Release creation: PASS

P11 evidence is stored under:

```text
results/p11/
```

The evidence includes:

```text
README.md
release-commit.txt
tags.txt
workflow-release-v1.0.2.png
github-release-v1.0.2.png
```

## Azure Cost Model

The P11 release pipeline does not require a persistent Azure environment.

The following Azure resources do not need to be provisioned for CI, security analysis, or release generation:

- Azure Kubernetes Service
- Azure Container Registry
- Azure Database for PostgreSQL
- Azure Service Bus
- Azure Key Vault
- Application Insights
- Log Analytics
- Azure virtual networking resources

GitHub-hosted runners perform the build and security work, while Docker Compose provides the temporary PostgreSQL instance required by integration tests.

This keeps Azure infrastructure cost at effectively zero while the project is not actively performing Azure deployment or runtime validation.

## Release Checklist

Before tagging:

- CI is green.
- Security workflow is green.
- `main` is current.
- Working tree is clean.
- Release version has not previously been used.

After tagging:

- Release workflow is green.
- All tests passed.
- Database migrations passed.
- Release archive exists.
- SPDX SBOM exists.
- `SHA256SUMS` exists.
- Build provenance attestation succeeded.
- SBOM attestation succeeded.
- GitHub Release exists.
- Release assets are downloadable.
- Evidence is captured under `results/p11/`.

A release is considered complete only after the tagged Release workflow and its required validation steps have succeeded.
