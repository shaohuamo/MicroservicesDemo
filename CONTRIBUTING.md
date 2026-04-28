# Contributing to MicroservicesDemo

Thank you for considering contributing! Whether you're fixing a typo, improving documentation, adding a new feature, or reporting a bug — every contribution makes this project better. This guide will walk you through everything you need to get started.

> **License note:** By submitting a contribution, you agree that your work will be made available under the existing [MIT License](LICENSE).

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Before You Start](#before-you-start)
- [Environment Setup](#environment-setup)
  - [Prerequisites](#prerequisites)
  - [Clone & Run](#clone--run)
- [Project Structure](#project-structure)
- [Development Workflow](#development-workflow)
  - [Branch Strategy](#branch-strategy)
  - [Commit Message Format](#commit-message-format)
- [Code Quality](#code-quality)
  - [Backend — Lint & Test](#backend--lint--test)
  - [Frontend — Lint & Test](#frontend--lint--test)
  - [README Quality Gate](#readme-quality-gate)
- [CI/CD & Deployment](#cicd--deployment)
  - [GitHub Actions Workflows](#github-actions-workflows)
  - [Image Tags & Deployment](#image-tags--deployment)
- [Submitting Issues](#submitting-issues)
- [Submitting Pull Requests](#submitting-pull-requests)
- [Adding a New Microservice](#adding-a-new-microservice)

---

## Code of Conduct

This project follows a simple rule: **be kind and constructive**. Criticism of code and ideas is welcome; disrespect toward people is not. Maintainers reserve the right to remove comments or contributions that violate this principle.

---

## Before You Start

1. **Search existing [Issues](../../issues) and [PRs](../../pulls)** to avoid duplicating work.
2. For significant changes (new microservice, architectural change, new infra component), **open an Issue first** to discuss the approach before investing time in an implementation.
3. For small improvements (typo, doc clarification, minor bug fix) feel free to submit a PR directly.

---

## Environment Setup

### Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [Docker Desktop](https://www.docker.com/products/docker-desktop) | Latest | Required for the full stack |
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0+ | Backend services & tests |
| [Node.js](https://nodejs.org/) | 20 LTS+ | Frontend admin-web |
| [Git](https://git-scm.com/) | 2.x+ | |

> **Windows tip:** Use Git Bash or WSL2 for shell scripts (`scripts/check-readme.sh`, `.githooks/pre-commit`).

### Clone & Run

```bash
# 1. Clone the repository
git clone https://github.com/<your-fork>/MicroservicesDemo.git
cd MicroservicesDemo

# 2. Activate the pre-commit hook
git config core.hooksPath .githooks
chmod +x .githooks/pre-commit scripts/check-readme.sh   # Git Bash / WSL2 / macOS / Linux

# 3a. Start the LOCAL DEV environment (volume mounts + hot-reload)
docker compose -f docker/dev/docker-compose.yml \
               -f docker/dev/docker-compose.override.yml up

# 3b. — OR — start the DEMO DEPLOYMENT environment (pre-built images)
cd docker/deploy
docker compose -f docker-compose.yml up -d
```

Once all containers are healthy, open the services listed below:

| Service | URL |
|---------|-----|
| Admin Web | <http://localhost:3000> |
| Jaeger UI | <http://localhost:16686> |
| Grafana | <http://localhost:13000> |
| Prometheus | <http://localhost:9090> |
| Consul UI | <http://localhost:8500> |
| RabbitMQ Management | <http://localhost:15672> (guest / guest) |

> **Note on Demo Deployment**: By default, `docker-compose.yml` pulls `latest` images. To pin a specific CI build, see [Image Tags & Deployment](#image-tags--deployment).

---

## Project Structure

```
.githooks/              pre-commit hook (README quality gate)
scripts/                helper scripts (check-readme.sh)
src/
  backend/
    BuildingBlocks/CommonService/   shared RabbitMQ base classes & middleware
    Gateway/ApiGateway/             Ocelot gateway (routing: ocelot.json)
    Services/Products/              Products microservice (Clean Architecture)
      ProductsMicroservice.Core/         entities, interfaces, AutoMapper, Polly
      ProductsMicroservice.Infrastructure/  EF Core, Redis, RabbitMQ, Scrutor
      ProductsMicroService.API/             controllers, middleware, Consul, OTEL
    Services/Test/                  RabbitMQ consumer demo service
  frontend/admin-web/               Next.js 16 + React 19 admin UI
configs/                            observability & infra config files
docker/
  debug/                            development Compose files
  deploy/                           demo deployment Compose files
tests/
  ProductsServiceUnitTests/         xUnit unit tests for the Products service
```

Dependencies flow **inward only**: `API → Infrastructure → Core`. Never import an outer layer from an inner one.

---

## Development Workflow

### Branch Strategy

This project uses a **Feature Branch** model:

```
master                   ← stable, always deployable
  └── feature/<topic>    ← new features
  └── fix/<topic>        ← bug fixes
  └── docs/<topic>       ← documentation-only changes
  └── chore/<topic>      ← tooling, deps, CI changes
```

**Rules:**
- Branch from `master`, target `master`.
- Keep branches short-lived and focused on a single concern.
- Delete the branch after the PR is merged.

### Commit Message Format

This project follows **[Conventional Commits](https://www.conventionalcommits.org/)**.

```
<type>(<scope>): <short summary>

[optional body]

[optional footer(s)]
```

**Types:**

| Type | When to use |
|------|-------------|
| `feat` | A new feature |
| `fix` | A bug fix |
| `docs` | Documentation only |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `test` | Adding or correcting tests |
| `chore` | Build process, tooling, dependencies |
| `perf` | Performance improvement |
| `ci` | CI/CD configuration changes |

**Examples:**

```
feat(products): add pagination to GET /products endpoint

fix(gateway): correct Consul health-check interval configuration

docs(readme): add Jaeger Trace - Delete Flow screenshot and caption

test(products-adder): add edge case for duplicate product name

chore(frontend): upgrade Next.js to 16.3.0
```

**Rules:**
- Use the **imperative mood** in the summary line ("add", not "added" or "adds").
- Keep the summary line ≤ 72 characters.
- Reference issues in the footer: `Closes #42` or `Fixes #42`.

---

## Code Quality

### Backend — Lint & Test

```bash
# Build the entire solution (catches compile errors)
dotnet build MicroservicesDemo.sln

# Run all backend unit tests
dotnet test tests/ProductsServiceUnitTests/ProductsServiceUnitTests.csproj

# Run with verbose output
dotnet test tests/ProductsServiceUnitTests/ProductsServiceUnitTests.csproj --logger "console;verbosity=detailed"
```

**Backend testing conventions** (follow when adding tests):

- Framework: **xUnit** — use `[Fact]` for single cases, `[Theory]` + `[InlineData]` for parameterised tests.
- Mocking: **Moq** — inject all dependencies via constructor mocks.
- Assertions: **FluentAssertions** — use `.Should()` syntax.
- Test data: **AutoFixture** — avoid manually crafting large objects.
- One test file per service class; group related tests with `#region` comments.

### Frontend — Lint & Test

```bash
cd src/frontend/admin-web

# Install dependencies
npm install

# ESLint — static analysis
npm run lint

# Vitest unit tests (single run)
npm run test

# Vitest in watch mode (development)
npm run test:watch
```

### README Quality Gate

A pre-commit hook runs `scripts/check-readme.sh` automatically whenever `README.md` is staged. You can also run it manually at any time:

```bash
bash scripts/check-readme.sh README.md
```

The script checks for:

| Check | Failure mode |
|-------|-------------|
| Known typos (e.g. `Folw` → `Flow`) | **Auto-fixed** — commit continues |
| `###` headings missing icons (when neighbours have icons) | ❌ Blocks commit |
| Images without `Evidence to look for` / `看点` captions | ❌ Blocks commit |
| Incorrect screenshot order in Tracing section | ❌ Blocks commit |

If the hook blocks your commit, fix the issues reported, then:

```bash
git add README.md
git commit
```

To extend the typo dictionary, add entries to the `TYPO_KEYS` / `TYPO_VALS` arrays in `scripts/check-readme.sh`.

---

## CI/CD & Deployment

### GitHub Actions Workflows

This project uses **GitHub Actions** to automatically build and push Docker images to Docker Hub on every push to `master`.

Four independent workflows, each triggered only when its respective service changes:

| Workflow | File | Trigger Paths | Output Images |
|----------|------|---------------|--------------|
| **Products Microservice** | `.github/workflows/ci-products.yml` | `src/backend/Services/Products/**`, `src/backend/BuildingBlocks/**`, `tests/ProductsServiceUnitTests/**` | `<user>/productmicroservice:latest`, `<user>/productmicroservice:sha-<commit>` |
| **API Gateway** | `.github/workflows/ci-gateway.yml` | `src/backend/Gateway/**` | `<user>/apigateway:latest`, `<user>/apigateway:sha-<commit>` |
| **Test Microservice** | `.github/workflows/ci-test-microservice.yml` | `src/backend/Services/Test/**`, `src/backend/BuildingBlocks/**` | `<user>/testmicroservice:latest`, `<user>/testmicroservice:sha-<commit>` |
| **Frontend (admin-web)** | `.github/workflows/ci-frontend.yml` | `src/frontend/admin-web/**` | `<user>/admin-web:latest`, `<user>/admin-web:sha-<commit>` |

**Key features:**
- **Products workflow only**: Runs `dotnet test` before building to catch regressions early.
- **Tag strategy**: Each image is tagged with `latest` (always the newest) and `sha-<commit>` (immutable reference to a specific build).
- **Layer caching**: Uses GitHub Actions native layer cache (`type=gha`) to speed up rebuilds.

### Image Tags & Deployment

Images are built and pushed automatically by CI. For local or production deployments:

**Default deployment** (pulls `latest`):
```bash
cd docker/deploy
docker compose -f docker-compose.yml up -d
```

**Pin to a specific build** (e.g., to match a stable release or regression test):
```bash
cd docker/deploy
cp .env.example .env

# Edit .env to set image tags (use sha-<commit> from GitHub Actions logs)
# PRODUCTS_IMAGE_TAG=sha-a1b2c3d4
# APIGATEWAY_IMAGE_TAG=sha-a1b2c3d4
# TESTMICROSERVICE_IMAGE_TAG=sha-a1b2c3d4
# ADMINWEB_IMAGE_TAG=sha-a1b2c3d4

docker compose -f docker-compose.yml --env-file .env up -d
```

The `.env.example` file documents all available environment variables. See [docker/deploy/.env.example](docker/deploy/.env.example) for details.

---

## Submitting Issues

1. **Search** [existing issues](../../issues) — your problem may already be reported or solved.
2. **Check** the [FAQ section in README.md](README.md#-常见问题) for common startup problems.
3. **Reproduce** the issue with the latest version from `master`.

When filing an issue, include:

- **Environment:** OS, Docker Desktop version, .NET SDK version.
- **Steps to reproduce:** numbered, minimal sequence.
- **Expected behaviour** vs **actual behaviour**.
- **Logs or screenshots** where relevant (docker compose logs, browser console, Jaeger trace).

---

## Submitting Pull Requests

### Before You Submit

- [ ] The branch is based on the latest `master`.
- [ ] `dotnet build MicroservicesDemo.sln` succeeds with no warnings in changed files.
- [ ] `dotnet test tests/ProductsServiceUnitTests/...` passes.
- [ ] `npm run lint` and `npm run test` pass (if frontend files changed).
- [ ] `bash scripts/check-readme.sh README.md` passes (if README changed).
- [ ] New behaviour is covered by tests (or a test gap is clearly explained).
- [ ] Commit messages follow Conventional Commits format.

### PR Description Template

```markdown
## Summary
<!-- One paragraph describing what this PR does and why. -->

## Changes
- 
- 

## Screenshots / Evidence
<!-- Required if UI or observability output changed. -->

## Related Issues
Closes #
```

### Review Process

1. Open the PR as a **draft** while it's still in progress.
2. Mark **Ready for Review** when all checklist items pass.
3. At least one approval is required before merging.
4. Maintainers may request changes — please respond or resolve within a reasonable timeframe.
5. PRs are merged with **Squash & Merge** to keep `master` history clean.

---

## Adding a New Microservice

Follow the Products service as the reference implementation:

1. Create three projects under `src/backend/Services/<Name>/`:
   - `<Name>.Core` — entities, interfaces, AutoMapper profiles, Polly policies
   - `<Name>.Infrastructure` — EF Core, Redis, RabbitMQ, Scrutor decorators
   - `<Name>.API` — controllers, middleware, Consul registration, OTEL config
2. Register each layer's DI via extension methods (`Add<Name>Core`, `<Name>Infrastructure`).
3. Add Consul self-registration in `Program.cs`.
4. Add a route block in `src/backend/Gateway/ApiGateway/ocelot.json`.
5. Add service definitions to both `docker/dev/docker-compose.yml` and `docker/deploy/docker-compose.yml`.
6. Add unit tests under `tests/<Name>UnitTests/`.

Refer to [AGENTS.md](AGENTS.md) for naming conventions, DI scopes, and the full layering rules.

---

Thank you for taking the time to contribute. Every improvement — no matter how small — is appreciated. If you have any questions, feel free to open a discussion or drop a comment in an existing issue. 🙌
