# MicroservicesDemo

English | [简体中文](README.md)

**MicroservicesDemo** is a .NET 9 microservices showcase project demonstrating API gateway routing, service discovery, event-driven messaging, distributed caching, observability, and Clean Architecture. It supports local execution with Docker Compose and also provides a live demonstration environment deployed on AKS.

## 🌐 Live Demo

Try the [MicroservicesDemo live demo](https://250669.xyz/), deployed on AKS, without starting Docker Compose locally.

The live demo uses IdentityServer for secure authentication, with sign-in, account creation, and English/Chinese language selection before entering Admin Web.

> **Email verification:** After registration, the verification email typically takes 2–5 minutes to arrive. If it is not visible in your inbox, check the spam or promotions folder. Because the sending domain was registered recently, some email providers may temporarily classify these messages as spam.

The online environment also exposes these observability endpoints:

- [Grafana Dashboard](https://grafana.250669.xyz/dashboards): View monitoring dashboards, metrics, and logs for the applications and infrastructure.
- [Jaeger UI](https://jaeger.250669.xyz/): Query distributed traces and inspect the complete request path through Admin Web, the API Gateway, backend services, and their dependencies.

## 📖 Quick Navigation

- [Live Demo](#-live-demo)
- [Key Highlights](#-key-highlights)
- [Architecture](#️-architecture)
- [Quick Start](#-quick-start)
- [FAQ](#-faq)
- [Screenshots and Evidence](#️-screenshots-and-evidence)
- [Contributing](#-contributing)
- [License](#-license)

## Project Snapshot

- A runnable .NET 9 microservices demo that combines Ocelot gateway routing, local Consul service discovery, RabbitMQ async messaging, Redis caching, and full-stack observability; AKS deployments use Kubernetes Service DNS instead of Consul.
- Shows a secured request path from the Next.js admin UI through Duende IdentityServer and the Ocelot gateway into PostgreSQL, Redis, RabbitMQ, and Azure Service Bus.
- Includes AKS manifests and pipelines for dev, qa, staging, uat, and prod environments.
- Uses Jaeger, Grafana, Loki, and Alertmanager screenshots as concrete evidence of trace, metric, log, and alert flows.

## ⚙️ Tech Stack

**🧩 Backend** &nbsp;
![.NET 9](https://img.shields.io/badge/.NET_9-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=flat-square&logo=csharp&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![EF Core](https://img.shields.io/badge/EF_Core-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Ocelot](https://img.shields.io/badge/Ocelot-333333?style=flat-square&logoColor=white)
![Steeltoe](https://img.shields.io/badge/Steeltoe-4CAF50?style=flat-square&logoColor=white)
![AutoMapper](https://img.shields.io/badge/AutoMapper-BE1622?style=flat-square&logoColor=white)
![Polly](https://img.shields.io/badge/Polly-0066CC?style=flat-square&logoColor=white)
![Scrutor](https://img.shields.io/badge/Scrutor-6B4FBB?style=flat-square&logoColor=white)
![Swagger](https://img.shields.io/badge/Swagger-85EA2D?style=flat-square&logo=swagger&logoColor=black)
![Duende IdentityServer](https://img.shields.io/badge/Duende_IdentityServer-6C4AB6?style=flat-square&logoColor=white)
<br>

**🖥️ Frontend** &nbsp;
![Next.js](https://img.shields.io/badge/Next.js_16-000000?style=flat-square&logo=nextdotjs&logoColor=white)
![React](https://img.shields.io/badge/React_19-61DAFB?style=flat-square&logo=react&logoColor=black)
![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?style=flat-square&logo=typescript&logoColor=white)
![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS_4-06B6D4?style=flat-square&logo=tailwindcss&logoColor=white)
![TanStack Query](https://img.shields.io/badge/TanStack_Query-FF4154?style=flat-square&logo=reactquery&logoColor=white)
![Axios](https://img.shields.io/badge/Axios-5A29E4?style=flat-square&logo=axios&logoColor=white)
<br>

**🗄️ Infrastructure** &nbsp;
![PostgreSQL](https://img.shields.io/badge/PostgreSQL_16-4169E1?style=flat-square&logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-DC382D?style=flat-square&logo=redis&logoColor=white)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ_4-FF6600?style=flat-square&logo=rabbitmq&logoColor=white)
![Consul](https://img.shields.io/badge/Consul-F24C53?style=flat-square&logo=consul&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat-square&logo=docker&logoColor=white)
![Azure Service Bus](https://img.shields.io/badge/Azure_Service_Bus-0078D4?style=flat-square&logo=microsoftazure&logoColor=white)
![Kubernetes](https://img.shields.io/badge/AKS-326CE5?style=flat-square&logo=kubernetes&logoColor=white)
<br>

**🔍 Observability** &nbsp;
![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-000000?style=flat-square&logo=opentelemetry&logoColor=white)
![Prometheus](https://img.shields.io/badge/Prometheus-E6522C?style=flat-square&logo=prometheus&logoColor=white)
![Grafana](https://img.shields.io/badge/Grafana-F46800?style=flat-square&logo=grafana&logoColor=white)
![Jaeger](https://img.shields.io/badge/Jaeger-00ADE4?style=flat-square&logoColor=white)
![Loki](https://img.shields.io/badge/Loki-F4A020?style=flat-square&logo=grafana&logoColor=white)
![Alertmanager](https://img.shields.io/badge/Alertmanager-E6522C?style=flat-square&logo=prometheus&logoColor=white)
<br>

**🧪 Testing** &nbsp;
![xUnit](https://img.shields.io/badge/xUnit-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Moq](https://img.shields.io/badge/Moq-555555?style=flat-square&logoColor=white)
![FluentAssertions](https://img.shields.io/badge/FluentAssertions-99CC00?style=flat-square&logoColor=white)
![AutoFixture](https://img.shields.io/badge/AutoFixture-555555?style=flat-square&logoColor=white)
![Vitest](https://img.shields.io/badge/Vitest-6E9F18?style=flat-square&logo=vitest&logoColor=white)

## 🔄 CI/CD Status

[![Admin Web Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2Fadmin-web?branchName=dev&label=Admin%20Web)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=2&branchName=dev)
[![IdentityServer Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2Fidentityserver?branchName=dev&label=IdentityServer)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=9&branchName=dev)
[![Test Microservice Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2FTestMicroservice?branchName=dev&label=Test%20Microservice)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=5&branchName=dev)
[![API Gateway Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2Fapigateway?branchName=dev&label=API%20Gateway)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=3&branchName=dev)
[![Products Microservice Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2FProductsMicroservice?branchName=dev&label=Products%20Microservice)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=1&branchName=dev)
[![Infrastructure Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2Finfrastructure?branchName=dev&label=Infrastructure)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=4&branchName=dev)

The badges above dynamically show the latest `dev` branch run for each pipeline; select a badge to open the corresponding Azure Pipeline. Application pipelines build images, push them to ACR, and deploy to AKS. Platform pipelines manage infrastructure, ingress, and cluster add-ons.

| Type | Pipeline definitions |
| --- | --- |
| Applications | [Products Microservice](aks/pipelines/azure-pipelines-products-microservice.yaml) · [API Gateway](aks/pipelines/azure-pipelines-apigateway.yaml) · [IdentityServer](aks/pipelines/azure-pipelines-identityserver.yaml) · [Test Microservice](aks/pipelines/azure-pipelines-test-microservice.yaml) · [Admin Web](aks/pipelines/azure-pipelines-admin-web.yaml) |
| Platform | [Infrastructure](aks/pipelines/azure-pipelines-infrastructure.yaml) · [Ingress](aks/pipelines/azure-pipelines-ingress.yaml) · [Cluster Add-ons](aks/pipelines/azure-pipelines-cluster-addons.yaml) |

## ✨ Key Highlights

| # | Highlight | Why it matters |
| --- | --- | --- |
| 1 | **AI-Assisted Engineering Workflow** | The `.github/` folder contains custom agents, skills, and `mcp-config.json`, while reusable skills capture C# test generation and frontend UI best practices |
| 2 | **Environment-Aware Service Discovery** | Local Docker Compose uses Consul for dynamic registration and discovery; AKS uses Kubernetes Service DNS and ClusterIP routing without Consul |
| 3 | **RabbitMQ Event-Driven Communication** | Product creation events are published asynchronously and consumed independently by the Test Service |
| 4 | **Redis Caching with Decorator Pattern** | A Scrutor-based decorator chain adds caching and telemetry transparently above core business logic |
| 5 | **PostgreSQL + EF Core Persistence** | Options-based connection configuration and exponential-backoff retries improve resilience |
| 6 | **OpenTelemetry End-to-End Tracing** | Frontend, backend, and infrastructure signals flow through the OTEL Collector |
| 7 | **Clean Architecture + SOLID + Unit Tests** | The Products service enforces inward dependencies and covers core behavior with xUnit and Moq |
| 8 | **Authentication and Secure Sessions** | Duende IdentityServer and ASP.NET Core Identity provide OIDC/OAuth 2.0 login, registration, email confirmation, refresh tokens, scope validation, and a Redis-backed token denylist |
| 9 | **Multi-Channel Asynchronous Messaging** | RabbitMQ supports local event demonstrations, while Azure Service Bus carries product-update topics |
| 10 | **Container and AKS Delivery** | Azure Pipelines build and push images to ACR, then deploy multi-environment Kubernetes manifests to AKS |
| 11 | **Production-Grade Secret Management** | Azure DevOps Variable Groups retrieve values from Azure Key Vault and deploy environment-specific Kubernetes Secrets |
| 12 | **HTTP Resilience and Circuit Breaker** | Products API applies `AddStandardResilienceHandler()` from `Microsoft.Extensions.Http.Resilience` to HttpClient defaults, combining timeouts, retries, and a circuit breaker; repeated downstream failures fail fast and temporarily stop calls to prevent thread buildup and cascading failures, then probe recovery automatically |

## 🏗️ Architecture

<p align="center">
  <img src="images/ComponentsDiagram.svg" alt="System Architecture" style="width: 100%; max-width: 900px; height: auto;" />
</p>

**Request path**: Browser → Admin Web → IdentityServer (OIDC) → API Gateway (Ocelot) → Products API / Test API → PostgreSQL / Redis

**Message path**: Products API → RabbitMQ / Azure Service Bus → Test API

**Products-to-Test service communication**:

- **Synchronous (product deletion)**: After Products Service deletes a product, Products Infrastructure calls Test API over HTTP to delete the related product information.
- **Asynchronous (product creation)**: Products Service publishes the `products.add` event to RabbitMQ, and Test API consumes it from the bound queue.
- **Asynchronous (product update)**: Products Service publishes the `product.update` event to an Azure Service Bus topic, and Test API consumes it through a subscription. Messages entering the Dead-letter Queue (DLQ) trigger an alert for operators to inspect and handle manually.

**Observability path**: All services → OTEL Collector → Jaeger (Traces) / Prometheus (Metrics) / Loki (Logs) → Grafana

### 🔐 Authentication and Request Flow

`Next.js UI` and the `BFF` are logical components in the same Admin Web deployment, not separate services. The BFF maintains the NextAuth session and tokens on the server, so the browser does not hold access tokens or call the API Gateway directly.

| Relationship | Description |
| --- | --- |
| `Next.js UI → BFF` | The browser calls a Next.js API Route over same-origin HTTPS |
| `BFF → API Gateway` | The BFF reads the access token from its server-side session and proxies API requests with a Bearer token |
| `Admin Web / Browser ↔ IdentityServer` | When the user is unauthenticated, Admin Web initiates OIDC login and redirects the browser; the user completes registration, email confirmation, and sign-in in IdentityServer; the BFF handles the callback, token exchange, token refresh, and logout |
| `API Gateway ⇢ IdentityServer` | The gateway retrieves and caches OIDC metadata/JWKS to validate JWT signature, issuer, audience, and lifetime locally |

Solid lines represent runtime requests or data flows; dashed lines represent discovery, trust, configuration, or optional dependencies. The API Gateway normally does not call IdentityServer for every application request.

> **Service discovery boundary**: Consul is used only in local Docker Compose. In AKS, the gateway reaches backends through Kubernetes Service DNS, while Kubernetes provides registration, addressing, and load balancing.

## ⚙️ Technology Choices

| Category | Technology | Why chosen |
| --- | --- | --- |
| Backend | .NET 9, ASP.NET Core, EF Core | Mature ecosystem with native OpenTelemetry integration |
| Gateway | Ocelot | Centralized routing decouples clients from internal service addresses |
| Service Discovery | Consul (local), Kubernetes Service DNS (AKS) | Dynamic local discovery and stable AKS service addressing |
| Architecture | Clean Architecture, SOLID, Decorator, DI | Clear dependency boundaries and non-invasive decorator chains |
| Database | PostgreSQL + EF Core | Relational persistence with Npgsql telemetry support |
| Cache | Redis | Reduces repeated reads and composes transparently through decorators |
| Identity | Duende IdentityServer, ASP.NET Core Identity, Resend | OIDC/OAuth 2.0 login, registration, email confirmation, and API scope authorization |
| Messaging | RabbitMQ, Azure Service Bus | Asynchronous event propagation and idempotent consumption allow producers and consumers to evolve independently |
| Secrets | Azure Key Vault, Azure DevOps Variable Groups, Kubernetes Secrets | Secure, environment-specific injection into AKS workloads |
| Observability | OpenTelemetry, OTEL Collector, Prometheus, Grafana, Jaeger, Loki, Alertmanager | Three-pillar observability from browser to infrastructure |
| Frontend | Next.js, React, TypeScript, TanStack Query | Frontend spans participate in distributed traces |
| Testing | xUnit, Moq, FluentAssertions, AutoFixture | Readable, idiomatic .NET tests |
| Delivery | Docker Compose, AKS, Azure Pipelines | Reproducible local stack and multi-environment deployment assets |

## 💡 Core Features

**📐 Product Management (Products Service)**

- Full CRUD exposed through the Ocelot Gateway
- New products trigger a RabbitMQ event consumed asynchronously by the Test Service
- Read flows use Redis caching; updates and deletes explicitly invalidate cache entries

**🚀 Service Governance (Gateway + Consul / Kubernetes DNS)**

- The API Gateway is the single entry point
- Local services self-register with Consul and are discovered by service name
- AKS uses Kubernetes Service DNS and ClusterIP Services instead of Consul

**🛡️ Resilient Communication (Polly / Microsoft.Extensions.Http.Resilience)**

- Products API HttpClients use the standard resilience pipeline with request timeouts, exponential-backoff retries, and a Circuit Breaker by default
- When a downstream dependency fails repeatedly, the breaker enters Open state and rejects calls quickly; after the cool-down it enters Half-Open to probe recovery and restores normal traffic after a successful probe, reducing cascading failures and resource exhaustion

**🔐 Authentication (IdentityServer + Admin Web)**

- Admin Web uses OIDC Authorization Code Flow with server-side sessions and token refresh
- Registration, Resend email confirmation, Redis-backed rate limiting, Bearer tokens, and `products-api` scope enforcement are included
- Logout adds access tokens to a Redis denylist that the gateway can validate in fail-closed mode

**📨 Messaging Reliability (RabbitMQ + Azure Service Bus)**

- RabbitMQ demonstrates product-created event publishing and idempotent consumption
- Azure Service Bus carries product-update events

**🔍 Observability Stack**

- Services emit traces, metrics, and logs through OpenTelemetry
- Grafana correlates logs with Jaeger traces through TraceID
- Alertmanager sends alerts to Slack

## 📁 Repository Structure

```
.github/
  agents/                       # Custom agents, such as C# Expert and Expert React Frontend Engineer
  skills/                       # Custom skills, such as csharp-test-gen and premium-frontend-ui
  mcp-config.json               # MCP Server configuration, such as filesystem, context7, and dockerhub
src/backend/
  Gateway/ApiGateway/           # Ocelot API Gateway; routing rules are in ocelot.json
  IdentityServer/               # OIDC/OAuth 2.0, registration, email confirmation, and token issuance
  Services/Products/            # Products microservice
    ProductsMicroservice.Core/           # Business logic, contracts, AutoMapper, and Polly policies
    ProductsMicroservice.Infrastructure/ # EF Core, Redis caching, RabbitMQ publishing, and Scrutor decorators
    ProductsMicroService.API/            # Controllers, middleware, Consul registration, and OTEL configuration
  Services/Test/                # Test microservice demonstrating RabbitMQ consumption
  BuildingBlocks/CommonService/ # Shared cross-cutting components
src/frontend/admin-web/         # Next.js admin UI with OTEL integration
aks/                            # Multi-environment Kubernetes manifests and Azure Pipelines
configs/                        # Monitoring, alerting, logging, and database configuration
docker/                         # Local development and demo Compose environments
tests/                          # Products and IdentityServer unit tests
```

## 🚀 Quick Start

**⚡️ Prerequisites**: Docker Desktop and an Azure Service Bus namespace with the `products.updates` topic and `products.updates.test` subscription already created. Install the .NET 9 SDK and Node.js 20+ as well if you want to build or test on the host.

**📑 Local development** (create local configuration before the first start):

```powershell
if (-not (Test-Path docker/dev/.env)) { Copy-Item docker/dev/.env.example docker/dev/.env }
# Edit docker/dev/.env, replace the sample credentials, and provide a valid Service Bus connection string
docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml up
```

`docker/dev/.env` is ignored by Git. Never commit real passwords, Resend API tokens, or Service Bus connection strings. The development IdentityServer URL is `http://localhost:8485`.

**📦 Demo deployment** (pull pre-built images):

```powershell
if (-not (Test-Path docker/deploy/.env)) { Copy-Item docker/deploy/.env.example docker/deploy/.env }
# Edit docker/deploy/.env and configure a real IdentityServer .pfx signing certificate and password
docker compose --env-file docker/deploy/.env -f docker/deploy/docker-compose.yml up -d
```

This pulls `latest` by default. To pin a CI build, set `PRODUCTS_IMAGE_TAG`, `APIGATEWAY_IMAGE_TAG`, `IDENTITYSERVER_IMAGE_TAG`, `TESTMICROSERVICE_IMAGE_TAG`, and `ADMINWEB_IMAGE_TAG` in `docker/deploy/.env` to the desired `sha-<commit>` tags, then restart:

```powershell
docker compose --env-file docker/deploy/.env -f docker/deploy/docker-compose.yml up -d
```

> Demo deployment uses Production settings and requires an IdentityServer `.pfx` signing certificate generated outside the repository. Never commit the certificate or its password.

**🌐 Key URLs**:

| Service | URL |
| --- | --- |
| Admin Web | http://localhost:3000 |
| IdentityServer (development / demo deployment) | http://localhost:8485 / http://localhost:8085 |
| API Gateway | http://localhost:9080 |
| Jaeger UI | http://localhost:16686 |
| Grafana | http://localhost:13000 |
| Prometheus | http://localhost:9090 |
| Consul UI | http://localhost:8500 |
| RabbitMQ Management(Account/Password:guest) | http://localhost:15672 |

**📄 Recommended demo flow**:

1. Import [MicroservicesDemo.postman_collection.json](MicroservicesDemo.postman_collection.json) to Postman
2. Trigger product operations through Admin Web or Postman
3. Inspect the distributed trace in Jaeger — observe Redis and RabbitMQ child spans
4. Open Grafana Logs, find a trace ID in a log entry, and jump directly to the Jaeger trace
5. Check Consul UI for registered services; check RabbitMQ management for queue activity

## ❓ FAQ

1. **Docker Desktop is not running**
  - Symptom: `docker compose up` or `docker ps` cannot connect to the Docker daemon, or containers never get created successfully.
  - Fix: Start Docker Desktop first, make sure Docker Engine is healthy, then rerun the compose command.

2. **Container startup fails because the required port is already in use**
  - Symptom: Errors such as `port is already allocated` or `bind for 0.0.0.0:xxxx failed` appear during startup.
  - Fix: Update the port mappings in `docker-compose.yml` so they do not conflict with ports already used on the host machine, then start the stack again.

3. **Container startup fails because dependency services are unstarted or unhealthy**
  - Symptom: Application containers exit immediately or keep restarting because PostgreSQL, Redis, RabbitMQ, Consul, or other dependencies are not ready yet.
  - Fix: Add startup dependencies and health checks in `docker-compose.yml`, and increase health-check retries plus timeout or startup grace periods where needed so upstream services only start after dependencies are actually ready.

4. **A valid Slack Webhook URL is not configured**
  - Symptom: Alert rules fire, but no notifications arrive in Slack.
  - Fix: Put the Webhook URL in the Git-ignored `configs/secrets/alertmanager-slack-webhook.txt`, verify that it is valid, and restart Alertmanager.

5. **Metric names in Grafana dashboards do not match the actual metric names**
  - Symptom: Panels show `No data` even though the application and telemetry pipeline are running.
  - Fix: This is commonly caused by OpenTelemetry package upgrades that rename exported metrics. Compare the live metric names in Prometheus and update the Grafana dashboard queries accordingly.

6. **RabbitMQ fails to start with `Error when reading /var/lib/rabbitmq/.erlang.cookie: eacces`**
  - Symptom: RabbitMQ container exits at startup with a permission error.
  - Root cause: Stale permissions in the Docker volume prevent the `rabbitmq` user inside the container from reading/writing `.erlang.cookie`.
  - Fix: Delete the `rabbitmq_data` volume and restart — Docker will recreate it with correct permissions.

    ```powershell
    docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml down
    docker volume rm <your-compose-project-name>_rabbitmq_data
    docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml up
    ```

## ✅ Testing and Verification

```powershell
dotnet build MicroservicesDemo.sln
dotnet test tests/ProductsServiceUnitTests/ProductsServiceUnitTests.csproj
dotnet test tests/IdentityServerUnitTests/IdentityServerUnitTests.csproj
Set-Location src/frontend/admin-web
npm ci
npm run lint
npm test
npm run build
```

Backend tests cover product CRUD, message idempotency, API controllers, exception-handling middleware, AutoMapper mappings, Redis caching decorators, OpenTelemetry decorators, and IdentityServer login, registration, email confirmation, and resend flows. The frontend is checked with ESLint, Vitest, and a production Next.js build.

## 💪 Engineering Competencies Demonstrated

- **Microservice decomposition and layered design** — independently designed responsibility boundaries across Admin Web, API Gateway, Products Service, and Test Service; Products service enforces strict Clean Architecture with inward-only dependencies
- **Synchronous and asynchronous communication** — Ocelot routes synchronous requests through Consul locally and Kubernetes Service DNS in AKS; RabbitMQ and Azure Service Bus decouple asynchronous flows
- **Caching strategy design** — Scrutor decorator chain adds Redis caching non-invasively above the business layer; cache invalidation is handled explicitly on update and delete flows
- **Observability pipeline setup** — both frontend and backend emit OpenTelemetry signals; OTEL Collector routes traces, metrics, and logs to separate backends; Grafana, Jaeger, and Alertmanager provide unified visibility
- **Configuration management and service governance** — strongly-typed Options pattern for component configuration; Azure Key Vault, Variable Groups, and Kubernetes Secrets manage sensitive values; Consul is local-only while AKS uses native Kubernetes discovery
- **Unit testing and maintainability** — xUnit tests for all core service behaviors, Moq-injected dependencies, FluentAssertions for readable verification

## 🎯 Future Extensions

- Add RabbitMQ retry policies and a Dead Letter Exchange to complete local messaging resilience
- Introduce a Transactional Outbox: persist cache-invalidation events in the same database transaction as product changes, reliably dispatch them to the message broker from a background worker, and use retryable, idempotent consumers to remove or rebuild Redis entries so transient Redis failures or service restarts do not leave stale cache entries indefinitely
- Introduce the Saga pattern for distributed transaction consistency
- Add optional TOTP multi-factor authentication: provide an IdentityServer account-security page where users can bind authenticator apps such as Google Authenticator or Microsoft Authenticator; require a six-digit time-based code after password verification, with one-time recovery codes, authenticator reset, and security audit events. 2FA is not mandatory in the current demo; it can be enforced for administrators or sensitive operations later. Email codes may be used for recovery or as a transition path, but not as the final high-assurance authenticator


## 🖼️ Screenshots and Evidence

These screenshots show the working admin UI, identity email delivery, CI/CD pipelines, cloud resources, AKS runtime state, routing, discovery, tracing, metrics, logs, asynchronous messaging, and alerting.

### 🖥️ Admin UI and Identity Email

#### 📦 Products Management Page

The authenticated products workspace shows inventory count, total inventory value, average price, and product create, edit, and delete actions, demonstrating that Admin Web is integrated with the protected Products API.

![Products Management Page](images/ProductsListPage.png)

#### ✉️ Resend Email Delivery

The Resend delivery history shows successful English and Chinese account-confirmation emails, verifying the IdentityServer registration and email-confirmation flow.

![Resend Email Delivery](images/ResendService.png)

### 🔄 CI/CD and Azure Delivery Evidence

#### ✅ Azure Pipelines Run Overview

The overview shows successful runs for Admin Web, IdentityServer, API Gateway, Products, Test Service, infrastructure, ingress, cluster add-ons, and the message-reprocessor function pipelines.

![Azure Pipelines Run Overview](images/AllPipelinesRunResult.png)

#### 🧪 Products Microservice Pipeline

The Products pipeline builds and publishes the image, runs unit tests, and deploys to dev. The screenshot also exposes test pass rate, code coverage, and condition-controlled stages for later environments.

![Products Microservice Pipeline Run Detail](images/ProductsMicroservicePipelineRunDetail.png)

#### 🏗️ Infrastructure Pipeline

The infrastructure pipeline successfully deploys the dev environment, demonstrating that Azure infrastructure definitions can be applied repeatedly through an independent pipeline.

![Infrastructure Pipeline Run Detail](images/InfrastructurePipelineRunDetial.png)

#### 🔐 Azure DevOps Variable Groups

Variable groups separate global and Key Vault-backed configuration by application and responsibility, providing centralized, reusable environment values to deployment pipelines.

![Azure DevOps Variable Groups](images/AllVariableGroups.png)

#### 📦 Azure Container Registry

ACR contains repositories for Admin Web, API Gateway, IdentityServer, Products, and Test Service, demonstrating that application pipelines publish each service's container image.

![Azure Container Registry Repositories](images/AzureContainerRegistry.png)

#### 🔑 Azure Key Vault

Secret names are redacted in the screenshot. Their enabled state demonstrates that deployment secrets are managed in a centralized vault rather than embedded in the repository or pipeline definitions.

![Azure Key Vault Secrets](images/AzureKeyVault.png)

### ☸️ AKS Runtime State

#### 🚀 dev Namespace Pods

Frontend, gateway, identity, business, data, and observability workloads in the `dev` namespace are `Running` and Ready, showing the complete environment deployed in AKS.

![AKS dev Namespace Pods](images/AllPods.png)

#### 🌐 Nginx Ingress Pod

The Nginx pod in the AKS application-routing namespace is `Running` and Ready, providing the ingress entry point for external domain traffic.

![AKS Nginx Ingress Pod](images/NginxPod.png)

### 🔍 Service Discovery Evidence

**Evidence to look for:** The Consul screenshot confirms dynamic registration and discovery in local Docker Compose. AKS deployments use Kubernetes Service DNS instead.

![Consul](images/Consul.png)

### 🔭 Tracing, Metrics, and Logs

**Evidence to look for:** Jaeger traces cover OIDC sign-in, token issuance, and business requests propagating through the gateway, APIs, Redis, and RabbitMQ-related spans. Grafana and log-to-trace links show that metrics and logs can be investigated in the same distributed trace context.

#### 🔐 IdentityServer OIDC Login Flow

This screenshot shows the four authentication traces produced by one sign-in operation: `POST /Account/Login` validates the user and establishes the login session, the authorization callback resumes the original authorize request, discovery retrieves the OIDC metadata, and the BFF finally calls the token endpoint. Browser front-channel redirects and BFF back-channel HTTP requests appear as separate traces in Jaeger.

![IdentityServer OIDC Login Flow](images/JaegerIdentityServerLoginFlow.png)

#### 🎫 Authorization Code Token Exchange

The token endpoint trace shows the BFF exchanging a one-time authorization code for tokens. IdentityServer retrieves and removes the code, validates the client and scopes, creates the access, refresh, and identity tokens, and signs the JWTs. Removing the authorization code after redemption prevents replay.

<details>
<summary>Expand the complete token exchange trace</summary>

![IdentityServer Authorization Code Token Exchange](images/JaegerIdentityServerTokenExchange.png)

</details>

#### 📊 Jaeger POST Flow

The highlighted area shows Jaeger automatically capturing Test.Api message processing for `products.add.queue`. Even without explicit logs, the `Simulated Delay: 3s` span exposes the tail-latency pattern.

![Jaeger POST Flow](images/JaegerTracePostFlow.png)

#### 📊 Jaeger GET Flow

![Jaeger GET Flow](images/JaegerTraceGetFlow.png)

#### 📊 Jaeger DELETE Flow

![Jaeger DELETE Flow](images/JaegerTraceDeleteFlow.png)

#### 🔗 Jaeger Trace-to-Log Correlation

This screenshot shows direct navigation from a Jaeger trace to a log entry, linking distributed traces with application logs for root-cause analysis.

![Jaeger Trace to Log](images/JaegerTraceToLog.png)

#### 🔄 Log-to-Jaeger Trace

This screenshot shows the reverse path: use the TraceID in a log entry to open the corresponding Jaeger trace.

![Log to Jaeger Trace](images/LogToJaegerTrace.png)

#### 📈 Jaeger Monitor

![Jaeger Monitor](images/JaegerMonitor.png)

#### 📊 Grafana OTEL Metrics

![Grafana OTEL Metrics](images/GrafanaOTELMetrics.png)

### 📨 Messaging and Alerting

**Evidence to look for:** The RabbitMQ exchange and queue verify asynchronous routing and buffering of product-created events. The Azure Service Bus screenshot shows a product-update message in the DLQ. The Slack screenshots demonstrate that RabbitMQ and Service Bus failure signals can be turned into actionable notifications.

#### 🔄 RabbitMQ Exchange

![RabbitMQ Exchange](images/RabbitMQ_Exchange.png)

#### 📦 RabbitMQ Queue

![RabbitMQ Queue](images/RabbitMQ_Queue.png)

#### 📢 RabbitMQ Slack Alert Message

![Slack Alert Message](images/SlackAlertMessageFromRabbitMQ.png)

#### 📥 Azure Service Bus Dead-letter Queue

This screenshot shows a product-update message in the Azure Service Bus subscription DLQ, where operators can inspect and manually handle the dead letter.

![Azure Service Bus Dead-letter Queue](images/AzureServiceBus_DeadLetter.png)

#### 📢 Azure Service Bus Slack Alert Message

This screenshot shows the Slack alert triggered when a message enters the Azure Service Bus DLQ.

![Azure Service Bus Slack Alert Message](images/SlackAlertMessageFromAzureServiceBus.png)

## 🤝 Contributing

Contributions are welcome! Read [CONTRIBUTING.md](CONTRIBUTING.md) for the branch strategy, commit-message format, code-quality requirements, and PR guidelines before submitting. When documentation changes, keep the [Chinese README](README.md) in sync.

## 📄 License

This project is licensed under the [MIT License](LICENSE).
