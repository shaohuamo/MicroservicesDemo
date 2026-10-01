# MicroservicesDemo

English | [简体中文](README.md)

**MicroservicesDemo** is a .NET 9 microservices showcase project demonstrating API gateway routing, service discovery, event-driven messaging, distributed caching, observability, and Clean Architecture.

It runs locally with Docker Compose and also has a live demonstration environment on AKS.

## 🌐 Live Demo

Try the [MicroservicesDemo live demo](https://250669.xyz/), deployed on AKS, without starting Docker Compose locally.

The live demo uses IdentityServer for secure authentication, with sign-in, account creation, and English/Chinese language selection before entering Admin Web.

When managing products in Admin Web, note the following:

- Product Names must be unique, case-insensitively (for example, `ProductA` and `producta` are treated as the same name).
- A notification is sent when an update, add, or delete operation succeeds.
- An error message is displayed when an update, add, or delete operation fails, prompting the user to make corrections.

> **Email verification:** After registration, the verification email typically takes 2–5 minutes to arrive.
>
> If it is not in your inbox, check the spam or promotions folder. Because the sending domain was registered recently, some providers may temporarily classify these messages as spam.

The online environment also exposes these observability endpoints:

- [Grafana Dashboard](https://grafana.250669.xyz/dashboards): View monitoring dashboards, metrics, and logs for the applications and infrastructure.
- [Jaeger UI](https://jaeger.250669.xyz/): Query distributed traces and inspect the complete request path through Admin Web, the API Gateway, backend services, and their dependencies.

## 📖 Quick Navigation

- [Live Demo](#-live-demo)
- [Key Highlights](#-key-highlights)
- [Architecture](#️-architecture)
- [Design and Tradeoffs](#️-design-and-tradeoffs)
- [Core Features](#-core-features)
- [Caching Strategy](#-caching-strategy)
- [Repository Structure](#-repository-structure)
- [Quick Start](#-quick-start)
- [FAQ](#-faq)
- [Testing and Verification](#-testing-and-verification)
- [Screenshots and Evidence](#️-screenshots-and-evidence)
- [Contributing](#-contributing)

## Project Snapshot

- A runnable .NET 9 microservices demo that combines Ocelot gateway routing, local Consul service discovery, RabbitMQ async messaging, Redis caching, and full-stack observability; AKS deployments use Kubernetes Service DNS instead of Consul.
- Shows a secured path from the Next.js admin UI through Duende IdentityServer and Ocelot into Products and Notifications APIs, with reliable delivery through PostgreSQL, RabbitMQ, Redis, SSE, and Resend.
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
[![Notifications Microservice Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2FNotificationsMicroservice?branchName=dev&label=Notifications%20Microservice)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=5&branchName=dev)
[![API Gateway Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2Fapigateway?branchName=dev&label=API%20Gateway)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=3&branchName=dev)
[![Products Microservice Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2FProductsMicroservice?branchName=dev&label=Products%20Microservice)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=1&branchName=dev)
[![Infrastructure Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2Finfrastructure?branchName=dev&label=Infrastructure)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=4&branchName=dev)

The badges above show the latest `dev` branch status for each pipeline. Select one to open its Azure Pipeline.

- **Application pipelines:** Build images, push them to ACR, and deploy to AKS.
- **Platform pipelines:** Manage infrastructure, ingress, and cluster add-ons.

| Type | Pipeline definitions |
| --- | --- |
| Applications | [Products Microservice](aks/pipelines/azure-pipelines-products-microservice.yaml) · [API Gateway](aks/pipelines/azure-pipelines-apigateway.yaml) · [IdentityServer](aks/pipelines/azure-pipelines-identityserver.yaml) · [Notifications Microservice](aks/pipelines/azure-pipelines-notifications-microservice.yaml) · [Admin Web](aks/pipelines/azure-pipelines-admin-web.yaml) |
| Platform | [Infrastructure](aks/pipelines/azure-pipelines-infrastructure.yaml) · [Ingress](aks/pipelines/azure-pipelines-ingress.yaml) · [Cluster Add-ons](aks/pipelines/azure-pipelines-cluster-addons.yaml) |

## ✨ Key Highlights

| # | Highlight | Why it matters |
| --- | --- | --- |
| 1 | **AI-Assisted Engineering Workflow** | The `.agents/` folder contains project-level agents and skills covering C# test generation, EF migrations, telemetry constraints, Products code review, and frontend UI practices |
| 2 | **Environment-Aware Service Discovery** | Local Docker Compose uses Consul for dynamic registration and discovery; AKS uses Kubernetes Service DNS and ClusterIP routing without Consul |
| 3 | **Transactional Outbox Messaging** | Product Add/Delete/Update and the ProductOperations Outbox commit in one PostgreSQL transaction, then a worker publishes them asynchronously through RabbitMQ |
| 4 | **Redis Caching with Decorator Pattern** | A Scrutor-based decorator chain adds caching and telemetry transparently above core business logic |
| 5 | **PostgreSQL + EF Core Persistence** | Options-based connection configuration and exponential-backoff retries improve resilience |
| 6 | **OpenTelemetry End-to-End Tracing** | Frontend, backend, and infrastructure signals flow through the OTEL Collector |
| 7 | **Clean Architecture + SOLID + Unit Tests** | The Products service enforces inward dependencies and covers core behavior with xUnit and Moq |
| 8 | **Authentication and Secure Sessions** | Duende IdentityServer and ASP.NET Core Identity provide OIDC/OAuth 2.0 login, registration, email confirmation, refresh tokens, scope validation, and a Redis-backed token denylist |
| 9 | **Online and Offline Notifications** | Online users receive Redis-routed SSE events through the BFF; offline users receive Resend email |
| 10 | **Container and AKS Delivery** | Azure Pipelines build and push images to ACR, then deploy multi-environment Kubernetes manifests to AKS |
| 11 | **Production-Grade Secret Management** | Azure DevOps Variable Groups retrieve values from Azure Key Vault and deploy environment-specific Kubernetes Secrets |
| 12 | **Replica-Safe Delivery** | Notifications workers use `FOR UPDATE SKIP LOCKED`, row leases, versions, and exponential backoff so another replica can take over after a pod failure |

## 🏗️ Architecture

<p align="center">
  <img src="images/ComponentsDiagram.svg" alt="System Architecture" style="width: 100%; max-width: 900px; height: auto;" />
</p>

**🧭 Layer mapping**

- The Frontend Layer contains the Next.js UI and Admin Web BFF
- The Backend Layer contains the API Gateway, IdentityServer, Products Service, and Notifications Service
- The Products Service is split into API, Core, and Infrastructure projects
- The Infrastructure Layer contains Products DB, Notifications DB, Identity DB, Redis, RabbitMQ, and Resend
- Monitoring Services consist of the OTEL Collector, Jaeger, Prometheus, Loki, Grafana, and Alertmanager

**🔗 Solid and dashed connectors**

- Solid connectors represent synchronous runtime requests or data-store flows
- Dashed connectors represent asynchronous message or event communication, including Products Service to RabbitMQ, Notifications Service to Redis Pub/Sub, and Redis Pub/Sub to the Admin Web BFF
- Dashed grouping boxes only mark logical layer boundaries

**🔄 Synchronous request path**

- Browser → Next.js UI → Admin Web BFF → API Gateway (Ocelot) → Products Service / Notifications Service
- Each service accesses its own Products DB or Notifications DB and uses the Redis cache when needed

**📨 Asynchronous communication path**

- Products Service → PostgreSQL ProductOperations Outbox → RabbitMQ → Notifications Service → Notifications DB
- The Notifications Service publishes notifications through Redis Pub/Sub to the Admin Web BFF, which pushes them to the Next.js UI through SSE; Offline users receive notification emails through Resend

**🔍 Observability path**

- The BFF, API Gateway, IdentityServer, Products Service, and Notifications Service push traces, logs, and metrics to the OTEL Collector
- OTEL Collector → Jaeger (traces), Loki (logs), and Prometheus (metrics)
- Prometheus pulls metrics from Infrastructure Layer databases
- Grafana queries logs from Loki and metrics from Prometheus; Prometheus triggers Alertmanager, which pushes alerts to Slack

**🧭 Service discovery boundary**

- Consul is used only in local Docker Compose
- In AKS, the gateway reaches backends through Kubernetes Service DNS, while Kubernetes provides registration, addressing, and load balancing

### 🔐 Authentication and Request Flow

`Next.js UI` and the `BFF` are logical components in the same Admin Web deployment, not separate services.

The browser carries an encrypted NextAuth session cookie. The BFF reads the access token from it and proxies API requests, so browser code does not read the token or call the API Gateway directly.

| Relationship | Description |
| --- | --- |
| `Next.js UI → BFF` | The browser calls a Next.js API Route over same-origin HTTPS |
| `BFF → API Gateway` | The BFF reads the access token from the session cookie's JWT payload and proxies API requests with a Bearer token |
| `Admin Web / Browser ↔ IdentityServer` | When the user is unauthenticated, Admin Web initiates OIDC login and redirects the browser; the user completes registration, email confirmation, and sign-in in IdentityServer; the BFF handles the callback, token exchange, token refresh, and logout |
| `API Gateway ⇢ IdentityServer` | The gateway retrieves and caches OIDC metadata/JWKS to validate JWT signature, issuer, audience, and lifetime locally |

1. The sign-in callback exchanges the OIDC authorization code for an access token, ID token, and refresh token.
2. The access and ID tokens are kept in the encrypted session cookie. The refresh token is encrypted in Admin Web's PostgreSQL database; the cookie holds only its record ID.
3. After IdentityServer refreshes the tokens, the BFF updates the access token in the cookie and updates the PostgreSQL record if a new refresh token is returned.
4. On sign-out, Admin Web attempts to add the access token to the Redis denylist and deletes the PostgreSQL refresh-token record.

## ⚖️ Design and Tradeoffs

### Concurrent Access Token Refresh for Products and Notifications Requests

- **Symptom:** Near access-token expiry, the Products list, Notifications list, and notification SSE replay requests may each call `/connect/token`.
- **Cause:** These independent requests can carry the same stale session cookie before the browser receives an updated one, so each decides to refresh. The [Auth.js documentation](https://authjs.dev/guides/refresh-token-rotation) also describes this race.
- **Current approach:**
  - **Trigger:** Refresh only before a backend API call needs the token and only when the cookie's access token enters its final 60 seconds.
  - **Redis coordination:** A result key and lock, keyed by the login's refresh-token record ID, coordinate replicas. The lock holder checks the result again before calling IdentityServer.
  - **Result cache:** Successful results are encrypted and live for the lesser of 30 seconds or the time until the 60-second refresh window minus one second; failures live for 15 seconds.
  - **Timeouts:** With healthy Redis, waiters give up after eight seconds with a retryable 503. The refresh call has a 45-second timeout and the lock a 50-second lease.
  - **Redis failure:** Each request reads the refresh token from PostgreSQL and refreshes independently without writing a Redis result. Before returning success, it checks that the database record has not been deleted.
  - **Session update:** BFF responses update the session cookie. Normal sign-out revokes the Redis lock and temporarily marks the session invalid so an in-flight refresh cannot publish a result afterward.
- **Tradeoff:**
  - During a Redis outage, concurrent requests may each call IdentityServer, increasing its load. A Redis publish failure after a refresh does not trigger another IdentityServer call.
  - Checking the database record cannot eliminate a sign-out race immediately after that check.
  - The 30-second result window covers closely spaced requests while Redis is healthy, but a lock or result may expire, disappear on restart, or be evicted early under `allkeys-lru`. This does not guarantee unconditional exactly-once behavior. [Redis documents key eviction](https://redis.io/docs/latest/develop/reference/eviction/).

### Access Token Revocation

- **Symptom:** After sign-out, an unexpired access token may still pass JWT signature and lifetime validation. During a Redis outage or restart, the gateway may also be unable to read revocation records reliably.
- **Cause:** A JWT does not carry server-side sign-out state, so the gateway checks a separate denylist. Redis [AOF `everysec` persistence](https://redis.io/docs/latest/operate/oss_and_stack/management/persistence/) reduces restart data loss but cannot prevent [memory eviction](https://redis.io/docs/latest/develop/reference/eviction/).
- **Current approach:**
  - **Sign-out:** Admin Web attempts to add the access token to the Redis denylist shared with Products and deletes the PostgreSQL refresh-token record.
  - **Healthy Redis:** The gateway validates the JWT, then checks the denylist; a hit is rejected and a miss is allowed.
  - **Redis unavailable or recently recovered:** After a connection or command failure, and for 930 seconds after recovery, the gateway accepts only requests carrying a short-lived Admin Web server proof and checks whether `auth_refresh_tokens.id` exists.
  - **Fallback result:** Direct gateway requests receive 401; a database outage returns 503. The query confirms only that the session was not explicitly signed out; it does not validate the IdentityServer refresh token.
  - **Lifetime:** Access tokens live for 900 seconds, gateway clock skew is 30 seconds, and denylist keys remain until `exp + 30 seconds`; [IdentityModel defaults to 300 seconds of skew](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters.defaultclockskew).
- **Tradeoff:**
  - PostgreSQL queries add load during Redis failures, and direct gateway clients cannot use session fallback.
  - Eviction of a denylist key while Redis is healthy does not trigger a database query, so an old token may pass for the rest of its lifetime.
  - The same window exists if Admin Web cannot write to Redis while the gateway can still read it, or if the gateway misses a brief outage, even though sign-out deleted the database record.
  - Covering this one-sided failure requires checking PostgreSQL even while Redis is healthy or adding durable revocation retries that the gateway can observe.

### API Idempotency

- **Problem:** A client may time out or lose the response after a product write commits. HTTP PUT/DELETE effect idempotence does not guarantee replay of the first result: an update retry with the original `Version` can return 409, and a delete retry can return 404.
- **Cause:** HTTP method semantics do not guarantee that a retry receives the first successful response, so the API must record results and recognize duplicate requests.
- **Current approach:**
  - Add, Update, and Delete require an idempotency key. Retrying the same request replays its first successful result; reusing a key for a different request returns a conflict.
  - PostgreSQL stores successful results and remains authoritative. Redis serves fast replays, with misses or failures falling back to the database. Product changes and idempotency records commit atomically to prevent a committed write from running twice after a lost response.
  - Admin Web retains the key when the result is uncertain, and the BFF forwards the related request and response headers.
- **Tradeoff:** Idempotency records are retained for a limited time, so deduplication is not guaranteed after cleanup. Redis misses or outages add database load, and Redis and PostgreSQL do not share a transaction. A replay returns the first successful response, which may not reflect the product's current state. Message delivery remains at least once.

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
| Messaging | RabbitMQ, ProductOperations Outbox, Redis Pub/Sub, SSE, Resend | Durable event propagation, replay, targeted realtime delivery, and offline email |
| Secrets | Azure Key Vault, Azure DevOps Variable Groups, Kubernetes Secrets | Secure, environment-specific injection into AKS workloads |
| Observability | OpenTelemetry, OTEL Collector, Prometheus, Grafana, Jaeger, Loki, Alertmanager | OpenTelemetry is a vendor-neutral open standard. W3C Trace Context carries correlation across the browser, BFF, gateway, and microservices, while OTLP and the Collector decouple instrumentation SDKs from Jaeger, Loki, Prometheus, and other observability backends |
| Frontend | Next.js, React, TypeScript, TanStack Query | Next.js provides SSR, routing, and BFF API Routes; React supports component-based UI, TypeScript provides type safety, and TanStack Query manages server-state requests, caching, and invalidation |
| Testing | xUnit, Moq, FluentAssertions, AutoFixture | Readable, idiomatic .NET tests |
| Delivery | Docker Compose, AKS, Azure Pipelines | Reproducible local stack and multi-environment deployment assets |

## 💡 Core Features

**📐 Product Management (Products Service)**

- Product CRUD endpoints exposed through the Ocelot Gateway
- Add, Update, and Delete require a canonical UUID v4 `Idempotency-Key`.
  - Redis replays successful results first; a miss or outage falls back to PostgreSQL. A unique `(UserId, Operation, IdempotencyKey)` constraint protects persisted results; a different payload with the same key returns 409.
- Product Add/Delete/Update operations are delivered asynchronously to the Notifications Service through the ProductOperations Outbox and RabbitMQ

**🚀 Service Governance (Gateway + Consul / Kubernetes DNS)**

- The API Gateway is the single entry point
- Local services self-register with Consul and are discovered by service name
- AKS uses Kubernetes Service DNS and ClusterIP Services instead of Consul

**🛡️ Resilient Delivery (Database Leases + Backoff)**

- The ProductOperations Outbox and Notifications Delivery workers claim rows in short transactions and perform RabbitMQ, Redis, and Resend network calls outside those transactions
- Leases, version-checked updates, and up to five exponential-backoff attempts allow safe retry or takeover after a pod failure or ACK timeout

**🔐 Authentication (IdentityServer + Admin Web)**

- Admin Web uses OIDC Authorization Code Flow and refreshes tokens using an encrypted session cookie and a PostgreSQL refresh-token record
- Registration, Resend email confirmation, and Redis-backed rate limiting are included.
  - A unique `NormalizedEmail` index complements `RequireUniqueEmail` to enforce case-insensitive email uniqueness even when concurrent registrations bypass remote validation.
  - Products routes require a valid Bearer token and the `products-api` scope.
- The unique index is managed by an EF Migration and applied automatically by `Database.Migrate()` when IdentityServer starts; EF migration history ensures that it runs only once per database
- Logout adds access tokens to a Redis denylist that the gateway can validate in fail-closed mode

**📨 Messaging and Notifications Reliability**

- **Atomic write**: Product Add/Delete/Update operations share one EF Core unit of work with `ProductOperationOutbox` and `IdempotencyRecord` and commit atomically in a single `SaveChanges` call.
- **Reliable publishing**: The Outbox Dispatcher uses publisher confirms for `products.operation.completed`; a UUID v7 `NotificationId` is both the RabbitMQ `MessageId` and the end-to-end idempotency key.
- **Idempotent consumption and takeover:** The Notifications Consumer persists messages idempotently by payload hash; Delivery Workers coordinate replicas with PostgreSQL row leases.
  - RabbitMQ DLQ handles messages that cannot be delivered. Redis Pub/Sub routes live messages but does not provide durable storage.
- **Online and offline delivery**

  - **Delivery rules**: Online delivery waits for browser ACK, retries failures, and falls back to email. In-app acknowledgement is tracked independently of email status. SSE setup replays every notification without an in-app ACK, oldest operation first. BFF Presence routes live messages; `SequenceNumber` bounds pagination and breaks timestamp ties.
  - **Notification delivery workflow**:

    1. **Persist and publish:** Products Service commits the product operation and `ProductOperationOutbox` in one database transaction. The Outbox Dispatcher publishes the result to RabbitMQ.
       - The Notifications Consumer validates the message, stores it idempotently in Notifications DB using `NotificationId` and the payload hash, then acknowledges the RabbitMQ message.
    2. **Establish SSE:** After sign-in, the browser requests `/api/notifications/stream` through `EventSource`.
       - The BFF registers the local SSE connection, then connects to Redis and subscribes to `notifications:bff:{instanceId}` asynchronously. Each process shares one channel; `instanceId` combines an instance name and a random UUID. A Redis outage neither closes existing SSE connections nor prevents new connections and database replay.
    3. **Track presence and replay:** When Redis is available, the BFF adds `{instanceId}:{connectionId}` to the `notifications:presence:{userId}` sorted set. Its score is the expiry time, with a default 45-second TTL.
       - Before the first replay, the BFF waits up to one second for its Redis subscription and this connection's presence to become ready. It then runs one complete replay of notifications without an in-app ACK, including notifications sending or delivered by email, ordered by `OccurredAtUtc ASC, SequenceNumber ASC`. Page cursors preserve the scan's sequence watermark. Live messages are buffered and deduplicated by `notificationId`, then sent in the same order after replay, preserving microsecond precision when comparing timestamps. The browser's `Last-Event-ID` does not skip notifications. After replay, presence is refreshed every 15 seconds and removed on disconnect. Redis recovery restores subscriptions and presence.
    4. **Deliver in real time:** The Delivery Worker claims a due notification and removes expired presence members. If an active member remains and the current claim is within the realtime delivery limit, it publishes to the corresponding BFF instance channel.
       - The BFF routes the message to the user's local SSE connections. Idempotent browser ACK records `InAppAcknowledgedAtUtc`; retryable deliveries move to `DeliveredInApp`, while email states are preserved without invalidating an active email task's version or lease.
    5. **Handle offline users and retries:** The worker allows at least 20 seconds from notification creation for an in-app ACK; an ACK persisted before email starts prevents the fallback email. Without active presence, or when Redis calls fail, it may move to `SendingEmail` through Resend after the window if no ACK has arrived.
       - By default, only the first two worker claims for a notification can attempt a Redis publication to the BFF. Every claim counts, even when no presence exists or Redis fails, so the actual number of publications may be zero, one, or two. After a successful publication, the worker waits five seconds for ACK before the next claim. This limit is unrelated to the number of SSE connections and excludes the BFF's database fallback scans.
       - When the BFF detects Redis is unavailable, it pages through unacknowledged notifications from Notifications API's `/replay` endpoint every five seconds using the connection's session identity, then forwards them over the existing SSE stream. After resubscribing and restoring presence, it completes a final scan. Fallback and initial replay share ascending cursor pagination over all notifications without an in-app ACK. If readiness completes within one second and no disconnect occurs during replay, setup runs one initial scan; a scan may require multiple page requests, each returning up to 100 notifications. A readiness timeout still allows SSE to open, followed by recovery scans. Disconnects during replay or after setup also retain recovery scans. The browser deduplicates by `notificationId` and retries failed ACKs. If only the Notifications API publisher fails while the BFF's Redis connection stays healthy, replay waits for SSE reconnect.

The notification list displays history in `SequenceNumber DESC` order, loading 20 items initially without automatically acknowledging them. SSE replay recovers deliveries without an in-app ACK in ascending operation-time order, including after reconnect. The browser deduplicates overlapping notifications by `notificationId` and acknowledges duplicate SSE messages again.

Each database replay and buffered batch is ordered internally; there is no global business-order guarantee across batches and live sources.

Notifications API `/replay` starts with `limit` only; subsequent requests pass the returned `nextCursor` as `cursor`, replacing `afterSequence` / `upToSequence`.

Ascending cursors use `AfterOccurredAtUtc` and `AfterSequence`. Legacy `Before...` descending cursors return 400, ending the current replay; a new SSE connection rescans all notifications without an ACK. Complete the Notifications API rollout before updating Admin Web, allowing a brief SSE disconnect and automatic reconnect.

The existing unacknowledged partial index supports backward scans, so no database migration is needed. During development, the notification table, in-app ACK column, and indexes are created directly by the initialization SQL; service startup only checks database connectivity.

**🔍 Observability Stack**

- Services emit traces, metrics, and logs through OpenTelemetry
- Grafana correlates logs with Jaeger traces through TraceID
- Alertmanager sends alerts to Slack

## 🧊 Caching Strategy

The Products Service caches product data in Redis, with separate keys for product details and the full list:

- **Reads:** A by-ID cache miss loads the product from the database and fills the detail cache. Missing products use the `CacheOptions.NullValuePlaceholder` negative-cache entry.
  - The full-list cache uses logical expiration and refreshes in the background after expiry.
- **Reads during Redis failures:** Disconnection or a failed cache operation sends detail and full-list reads directly to PostgreSQL without filling the cache.
  - A full-list cold miss also falls back after a five-second distributed-lock timeout. A logically expired list can still be returned immediately; a failed background refresh is logged.
- **Redis command timeouts:** Products explicitly sets `Redis:AsyncTimeout = 2000` milliseconds and keeps `SyncTimeout` at 5000 milliseconds. Once a disconnection is detected, `BacklogPolicy.FailFast` prevents new commands from waiting in the reconnect backlog. The async timeout also applies to Redis commands used by cache locks; lock contention and initial connection attempts have separate settings.
- **Successful writes:** The first successful Add invalidates the list cache. The first successful Update or Delete immediately invalidates that product's detail cache and the full-list cache. Idempotent replays of all three writes skip invalidation; Update replays also skip delayed deletion.
  - Detail and list deletions run concurrently, and the service awaits both attempts. Only a successful update repeats both deletions concurrently after `Redis:DelayedDeleteMs` (about two seconds by default).
- **Update or delete fails because of a version conflict (409)**: The service makes a best-effort attempt to invalidate the detail and list caches, then rethrows the original exception.
  - Both deletions run concurrently and handle cache errors independently. Invalidation is recorded as successful only when both succeed.
- **Update or delete fails because the product is missing (404):** The service always attempts to invalidate the list cache. It invalidates the detail key only when it contains product data rather than a negative-cache placeholder.
  - List deletion runs concurrently with detail processing. The detail lookup and conditional deletion remain sequential; lookup failures are logged.
  - This failed-write policy does not apply to ordinary GET 404s or 409s caused by other errors.

**Cache consistency and failure boundaries:**

- **Wait time:** Two seconds applies to each async command, and timeout detection may take slightly longer. Concurrent deletions primarily wait for the slower attempt. Initial connections, lock contention, database operations, and sequential commands in the 404 detail branch may add latency; the API is not guaranteed to return within two seconds.
- **Read-after-write visibility:** PostgreSQL and Redis do not share a transaction. Cache deletion failures are logged, and concurrent reads can repopulate old values, so immediate visibility is not guaranteed.
- **Redis recovery:** A stale detail entry whose deletion failed may remain until its TTL expires. A stale list may be returned under logical expiration before a background refresh replaces it.
- **Load:** Database fallback adds load; repeated version conflicts or writes against nonexistent IDs can also increase full-list cache misses.
- **Health checks:** Products readiness requires only PostgreSQL, while `/health` reports all dependencies. Redis dependencies in Admin Web and the Gateway can still affect end-to-end access.

### Future improvements (not implemented)

- **Durable cache invalidation:** Record the invalidation intent in the same database transaction as the Product update in a new, separate cache-invalidation Outbox table. A background worker retries Redis deletion.
  - This improves the likelihood of eventual invalidation, but reads can still see the old cache before the worker runs; it does not provide immediate visibility.
- **Version invalidation:** Update the Product and increment the database version for its detail or list cache in the same transaction. Before returning cached data, check the committed version in the authoritative database and reload on a mismatch.
  - This provides strong consistency for reads that begin after the update commits, at the cost of one database version lookup per read. Keeping the version only in Redis does not provide strong consistency.

## 📁 Repository Structure

```
.agents/
  agents/                       # Project-level agents, such as C# Expert and Expert React Frontend Engineer
  skills/                       # Project-level skills, such as csharp-test-gen, ef-migration, and products-code-review
src/backend/
  Gateway/ApiGateway/           # Ocelot API Gateway; routing rules are in ocelot.json
  IdentityServer/               # OIDC/OAuth 2.0, registration, email confirmation, and token issuance
  Services/Products/            # Products microservice
    ProductsMicroservice.Core/           # Business logic, contracts, and AutoMapper
    ProductsMicroservice.Infrastructure/ # EF Core, Redis caching, RabbitMQ publishing, and Scrutor decorators
    ProductsMicroService.API/            # Controllers, middleware, Consul registration, and OTEL configuration
  Services/Notifications/        # Notifications Core, Infrastructure, and API projects
  BuildingBlocks/CommonService/ # Shared cross-cutting components
src/frontend/admin-web/         # Next.js admin UI with OTEL integration
aks/                            # Multi-environment Kubernetes manifests and Azure Pipelines
configs/                        # Monitoring, alerting, logging, and database configuration
docker/                         # Local development and demo Compose environments
tests/                          # Products, Notifications, and IdentityServer unit tests
```

## 🚀 Quick Start

**⚡️ Prerequisites**: Docker Desktop. Full offline email delivery also requires a Resend API token and verified sender. Install the .NET 9 SDK and Node.js 20+ to build or test on the host.

**📑 Local development** (create local configuration before the first start):

```powershell
if (-not (Test-Path docker/dev/.env)) { Copy-Item docker/dev/.env.example docker/dev/.env }
# Edit docker/dev/.env and replace sample credentials; configure Resend for real email delivery
docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml up
```

`docker/dev/.env` is ignored by Git. Never commit real passwords or Resend API tokens. The development IdentityServer URL is `http://localhost:8485`.

The Products PostgreSQL bootstrap SQL only creates the logical database. EF Core migrations manage the Products and ProductOperations Outbox tables and indexes.

### Access Token Revocation Configuration

**Proof key**

- Admin Web uses `AUTH_GATEWAY_PROOF_KEY_ID` and `AUTH_GATEWAY_PROOF_KEY`; configure the gateway with the same key ID and secret.
- The gateway also needs `Authentication__SessionFallback__PostgresConnectionString` and `Authentication__SessionFallback__PostgresPassword`.
- The proof key is separate from `AUTH_SECRET`, contains at least 32 random bytes, and is Base64 encoded. Generate one in PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

**Docker Compose**

- **Development:** Set the proof key and ID in `docker/dev/.env` using [`docker/dev/.env.example`](docker/dev/.env.example). Compose supplies the same values to Admin Web and the gateway.
- **Demo deployment:** Set both proof variables and `GATEWAY_DB_PASSWORD` in `docker/deploy/.env`, and set `GATEWAY_SESSION_FALLBACK_ENABLED` to `true`.
- **Database permissions:** The gateway's `gateway_auth_reader` account has only database connect, `public` schema usage, and column-level read access to `auth_refresh_tokens(id)`.
  - Compose runs SQL in `configs/postgres/init` only when a PostgreSQL volume is first initialized. For an existing volume, run [`create-gateway-auth-reader.sql`](configs/postgres/init/create-gateway-auth-reader.sql) manually or perform a planned rebuild.

**AKS**

- Each namespace needs a `gateway-auth-secrets` Secret with `proof-key-id`, `proof-key`, `postgres-connection-string`, and `postgres-password`.
- **Dev:** The infrastructure pipeline uses the Key Vault variables `gateway-proof-key` and `gateway-db-password` to create the Secret and runs the read-only role SQL against an existing database.
- **Other namespaces:** Create the Secret through that environment's secret-management process and run the SQL above before deployment. Point the connection string to PostgreSQL reachable from that namespace and use the gateway's read-only account.
- The repository's AKS Redis Deployment covers dev only. If QA, UAT, staging, and prod use external Redis, configure AOF `everysec` and persistent storage on the existing shared instance while keeping `allkeys-lru` and the current service address.

### Products Database Migrations and Seed Data

`MigrateDatabaseAsync()` prepares the Products database in this order:

1. Call EF Core `Database.MigrateAsync()` to apply pending migrations.
2. Import sample products from `SeedData/products.json`, embedded in the Infrastructure assembly.

Migration or seeding failures are logged at Critical level and rethrown, so the API does not start if database initialization fails. The seed process has these boundaries:

- It validates the JSON and uses `INSERT ... ON CONFLICT DO NOTHING` for `ProductId` inside one database transaction.
- Existing products are not overwritten. Job retries or occasional duplicate executions do not insert duplicates; other unique-key conflicts still fail.
- The migration Job needs only PostgreSQL. Local and Docker Compose startup migration assumes one API instance; multi-instance deployments use a separate Job.

**How it runs**

- **Local and Docker Compose:** `ProductsMigration:RunOnStartup` defaults to `true`, so startup applies migrations and seed data.
- **Migration Job:** The same API image runs with `--migrate`, completes initialization, then exits without starting the HTTP server.
- **AKS:** All five Products Deployments set `ProductsMigration__RunOnStartup=false`, so scaling or restarting API Pods does not repeat initialization.
  - Azure Pipelines creates a uniquely named Job for each release and deploys the API only after it completes. A Job failure or timeout stops the release and triggers collection of status and logs.
- **Manual deployment:** Run the Job in `aks/manifests/shared/backend/products-database-migration.yaml` with the target API image, confirm success, then update the Deployment.
  - Enable the **Exclusive lock** check on each Azure DevOps environment (dev, qa, uat, staging, and prod); `lockBehavior: sequential` relies on those checks to serialize releases to the same environment.

**📦 Demo deployment** (pull pre-built images):

```powershell
if (-not (Test-Path docker/deploy/.env)) { Copy-Item docker/deploy/.env.example docker/deploy/.env }
# Edit docker/deploy/.env and configure a real IdentityServer .pfx signing certificate and password
docker compose --env-file docker/deploy/.env -f docker/deploy/docker-compose.yml up -d
```

This pulls `latest` by default. To pin a CI build, set the desired `sha-<commit>` tags in `docker/deploy/.env`:

- Backend: `PRODUCTS_IMAGE_TAG`, `APIGATEWAY_IMAGE_TAG`, `IDENTITYSERVER_IMAGE_TAG`, `NOTIFICATIONS_IMAGE_TAG`
- Frontend: `ADMINWEB_IMAGE_TAG`

Then restart:

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

### RabbitMQ Quorum Queue and Dead-Letter Replay

**Queue and alerts**

- The Notifications consumer declares `notifications.products.operations` as a quorum queue with `x-message-ttl = 10000` (10 seconds), `x-delivery-limit = 3`, and dead-letter routing. Recreate the main queue when changing these declaration arguments.
- The dead-letter queue has no TTL or length limit. TTL limits only the time a message waits in the main queue; expired messages can still be replayed manually.
- Prometheus checks pending messages and consumer count only for the main notification queue.
- If `notifications.products.operations.dead-letter` contains messages for one minute, Alertmanager sends a critical alert to Slack. The DLQ normally has no standing consumer, so a no-consumer alert does not apply.

On local startup, the Notifications consumer declares the main queue and dead-letter routing; no separate RabbitMQ policy is required:

```powershell
docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml up -d
```

**Manual replay in AKS**

1. The Notifications pipeline builds the `notifications-dlq-replay:<BuildId>` image but does not run replay automatically. Confirm that the consumer has recovered and inspect the DLQ first.
2. Register the [Notifications DLQ Replay pipeline](aks/pipelines/azure-pipelines-notifications-dlq-replay.yaml) once in Azure DevOps. For each replay, select the branch for the target environment, click **Run pipeline**, and choose `targetEnvironment`.
3. The replay pipeline uses the Azure DevOps API to find the latest successful `NotificationsMicroservice` `BuildId` on that branch and select the image. It stops if there is no successful run.
4. Using the target environment's Kubernetes Service Connection, the pipeline creates a one-time Job, waits for completion, and reports the result in its logs. `trigger: none` prevents commits from launching replay automatically.

For local debugging, run the console app after starting RabbitMQ. Connection and limit settings are in `NotificationsMicroservice.DlqReplay/appsettings.json` and can be overridden with environment variables:

```powershell
dotnet run --project src/backend/Services/Notifications/NotificationsMicroservice.DlqReplay/NotificationsMicroservice.DlqReplay.csproj
```

**Job limits and idempotency**

- The Job reads RabbitMQ connection details from `notifications-microservice-secrets`. By default, it inspects at most 10 messages and runs for 60 seconds.
- It automatically replays only messages dead-lettered for `expired` or `delivery_limit`; `rejected` and other reasons remain in the DLQ for investigation. Each `MessageId` is replayed at most once per run, and the `manual-replay-count` header limits replay to three rounds.
- The Job ACKs the original dead-letter message only after publication is confirmed. A failure between confirmation and ACK may publish it again; Notifications DB deduplicates by `NotificationId` and the original payload.

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

7. **PostgreSQL Seed Data is not imported correctly**
  - Symptom: PostgreSQL starts successfully, but the expected initial data is missing or does not match the current seed files.
  - Root cause: When PostgreSQL reuses an existing `postgres_data` volume, it skips first-time initialization scripts. Changes to the seed files are not automatically applied to an existing database.
  - Fix: After confirming that the existing local database data can be discarded, stop the Compose stack and delete the corresponding `postgres_data` volume, then start the environment again. PostgreSQL will recreate the database and run the initialization and Seed Data import steps.

    ```powershell
    docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml down
    docker volume rm <your-compose-project-name>_postgres_data
    docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml up
    ```

  > Deleting the volume permanently removes the local PostgreSQL data. Use this only for development environments or when the data can be safely recreated.

## ✅ Testing and Verification

```powershell
dotnet build MicroservicesDemo.sln
dotnet test tests/ProductsServiceUnitTests/ProductsServiceUnitTests.csproj
# Requires Docker Desktop; isolated PostgreSQL/Redis containers are created and cleaned up automatically
dotnet test tests/ProductsServiceIntegrationTests/ProductsServiceIntegrationTests.csproj
dotnet test tests/IdentityServerUnitTests/IdentityServerUnitTests.csproj
Set-Location src/frontend/admin-web
npm ci
npm run lint
npm test
npm run build
```

Test coverage:

- **Backend:** Product CRUD, API idempotency for all three writes with Redis/database fallback, real PostgreSQL/Redis concurrency and rollback integration tests, message idempotency, API controllers, exception-handling middleware, AutoMapper mappings, Redis caching decorators, OpenTelemetry decorators, and IdentityServer login, registration, email confirmation, and resend flows.
- **Frontend:** ESLint, Vitest, and a production Next.js build.

## 💪 Engineering Competencies Demonstrated

- **Microservice decomposition and layered design** — independently designed responsibility boundaries across Admin Web, API Gateway, Products Service, and Notifications Service; Products service enforces strict Clean Architecture with inward-only dependencies
- **Synchronous and asynchronous communication** — Ocelot routes synchronous requests through Consul locally and Kubernetes Service DNS in AKS; RabbitMQ queues and the DLQ decouple and protect asynchronous flows
- **Caching strategy design** — Scrutor decorator chain adds Redis caching non-invasively above the business layer; cache invalidation is handled explicitly on update and delete flows
- **Observability pipeline setup** — both frontend and backend emit OpenTelemetry signals; OTEL Collector routes traces, metrics, and logs to separate backends; Grafana, Jaeger, and Alertmanager provide unified visibility
- **Configuration management and service governance** — strongly-typed Options pattern for component configuration; Azure Key Vault, Variable Groups, and Kubernetes Secrets manage sensitive values; Consul is local-only while AKS uses native Kubernetes discovery
- **Unit testing and maintainability** — xUnit tests for all core service behaviors, Moq-injected dependencies, FluentAssertions for readable verification

## 🎯 Future Extensions

- **Product details page**: Add an Admin Web page that fetches a product by ID, displays its details, and reuses the existing by-ID API and detail cache.
- **Products Pod autoscaling (HPA):** HPA could adjust Products Pod counts by CPU utilization in dev, qa, uat, staging, and prod. It is not currently deployed.
  - **Requirements:** Containers need sensible CPU requests and AKS needs a resource metrics API. Available node capacity and PostgreSQL connection and processing capacity also constrain scaling.
  - **Capacity tradeoff:** More replicas use more cluster resources and increase concurrent database load. Set scaling limits using load tests.
- Introduce the Saga pattern for distributed transaction consistency
- **Optional TOTP multi-factor authentication:** Add an IdentityServer account-security page where users can bind apps such as Google Authenticator or Microsoft Authenticator. After password verification, require a six-digit time-based code.
  - Provide one-time recovery codes, authenticator reset, and security audit events.
  - 2FA is optional in the current demo; TOTP could be required for administrators or sensitive operations. Email codes may support recovery or a transition, but are not the final high-assurance authenticator.


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

The overview shows pipeline runs for Admin Web, IdentityServer, API Gateway, Products, Notifications Service, infrastructure, ingress, and cluster add-ons.

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

ACR contains repositories for Admin Web, API Gateway, IdentityServer, Products, and Notifications Service, demonstrating that application pipelines publish each service's container image.

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

**Evidence to look for:**

- Jaeger shows traces for OIDC sign-in, token issuance, and business requests through the gateway, APIs, Redis, and RabbitMQ-related spans.
- Grafana and log-to-trace links show how to investigate metrics and logs in the same distributed trace context.

#### 🔐 IdentityServer OIDC Login Flow

This screenshot shows four authentication traces from one sign-in operation:

1. `POST /Account/Login` validates the user and establishes the login session.
2. The authorization callback resumes the original authorize request.
3. Discovery retrieves the OIDC metadata.
4. The BFF calls the token endpoint.

Browser front-channel redirects and BFF back-channel HTTP requests appear as separate traces in Jaeger.

![IdentityServer OIDC Login Flow](images/JaegerIdentityServerLoginFlow.png)

#### 🎫 Authorization Code Token Exchange

The token endpoint trace shows the BFF exchanging a one-time authorization code for tokens. IdentityServer retrieves and removes the code, validates the client and scopes, creates the access, refresh, and identity tokens, and signs the JWTs.

Removing the authorization code after redemption prevents replay.

<details>
<summary>Expand the complete token exchange trace</summary>

![IdentityServer Authorization Code Token Exchange](images/JaegerIdentityServerTokenExchange.png)

</details>

#### 📊 Jaeger POST Flow

This historical screenshot shows Jaeger capturing RabbitMQ message handling. The current implementation consumes `products.operation.completed` and creates a Notifications Consumer span.

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

**Evidence to look for:** The RabbitMQ exchange and queue demonstrate asynchronous routing and buffering. The Slack screenshot shows how messaging failures become actionable alerts.

#### 🔄 RabbitMQ Exchange

![RabbitMQ Exchange](images/RabbitMQ_Exchange.png)

#### 📦 RabbitMQ Queue

![RabbitMQ Queue](images/RabbitMQ_Queue.png)

#### 📢 RabbitMQ Slack Alert Message

![Slack Alert Message](images/SlackAlertMessageFromRabbitMQ.png)

## 🤝 Contributing

Contributions are welcome! Before submitting, read [CONTRIBUTING.md](CONTRIBUTING.md) for the branch strategy, commit-message format, code-quality requirements, and PR guidelines.

When documentation changes, keep the [Chinese README](README.md) in sync.

## 📄 License

This project is licensed under the [MIT License](LICENSE).
