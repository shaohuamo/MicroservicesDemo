# MicroservicesDemo

[English](README.en.md) | 简体中文

**MicroservicesDemo** 是一个基于 .NET 9 的微服务演示项目，展示 API Gateway 路由、服务发现、事件驱动消息、分布式缓存、可观测性与 Clean Architecture 的整合落地；既支持通过 Docker Compose 在本地运行，也提供部署在 AKS 上的在线演示环境。

## 🌐 在线体验

无需在本地启动 Docker Compose，可直接访问部署在 AKS 上的 [MicroservicesDemo 在线演示](https://250669.xyz/)。

在线演示通过 IdentityServer 提供安全认证，支持登录、创建账号和中英文切换；认证完成后即可进入 Admin Web。

在 Admin Web 中管理产品时，请注意：

- Product Name 不可重复，且不区分大小写（例如 `ProductA` 与 `producta` 会被视为同一名称）。
- 更新、新增或删除成功时，系统会发送通知。
- 更新、新增或删除失败时，系统会显示错误信息，提示用户修改。

> **邮箱验证提示：** 注册后，验证邮件通常需要 2–5 分钟送达。若收件箱中暂未看到，请检查垃圾邮件或广告邮件目录；由于发信域名注册时间较短，部分邮件服务商可能会暂时将验证邮件归类为垃圾邮件。

在线环境同时提供以下可观测性入口：

- [Grafana Dashboard](https://grafana.250669.xyz/dashboards)：查看应用与基础设施的监控 Dashboard、指标和日志。
- [Jaeger UI](https://jaeger.250669.xyz/)：查询分布式 Trace，分析请求经过 Admin Web、API Gateway、后端服务及依赖组件的完整调用链路。

## 📖 快速导航

- [在线体验](#-在线体验)
- [项目亮点](#-项目亮点)
- [架构图](#️-架构图)
- [设计与权衡](#️-设计与权衡)
- [核心功能](#-核心功能)
- [缓存策略](#-缓存策略)
- [项目结构](#-项目结构)
- [快速启动](#-快速启动)
- [常见问题](#-常见问题)
- [测试与验证](#-测试与验证)
- [截图与证据说明](#️-截图与证据说明)
- [参与贡献](#-参与贡献)

## 项目速览

- 一个可本地运行的 .NET 9 微服务示例，串联 Ocelot 网关路由、本地 Consul 服务发现、RabbitMQ 异步消息、Redis 缓存与全链路可观测性；AKS 部署使用 Kubernetes Service DNS，不依赖 Consul。
- 展示从 Next.js 管理台登录，经 Duende IdentityServer 与 Ocelot 网关访问 Products 与 Notifications API，并通过 PostgreSQL、RabbitMQ、Redis、SSE 和 Resend 完成可靠通知。
- 提供覆盖 dev、qa、staging、uat、prod 的 AKS 清单与流水线。
- 通过 Jaeger、Grafana、Loki 与 Alertmanager 截图展示 Trace、Metric、Log 与告警链路。

## ⚙️ 技术栈

**🧩 后端** &nbsp;
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

**🖥️ 前端** &nbsp;
![Next.js](https://img.shields.io/badge/Next.js_16-000000?style=flat-square&logo=nextdotjs&logoColor=white)
![React](https://img.shields.io/badge/React_19-61DAFB?style=flat-square&logo=react&logoColor=black)
![TypeScript](https://img.shields.io/badge/TypeScript-3178C6?style=flat-square&logo=typescript&logoColor=white)
![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS_4-06B6D4?style=flat-square&logo=tailwindcss&logoColor=white)
![TanStack Query](https://img.shields.io/badge/TanStack_Query-FF4154?style=flat-square&logo=reactquery&logoColor=white)
![Axios](https://img.shields.io/badge/Axios-5A29E4?style=flat-square&logo=axios&logoColor=white)
<br>

**🗄️ 基础设施** &nbsp;
![PostgreSQL](https://img.shields.io/badge/PostgreSQL_16-4169E1?style=flat-square&logo=postgresql&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-DC382D?style=flat-square&logo=redis&logoColor=white)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ_4-FF6600?style=flat-square&logo=rabbitmq&logoColor=white)
![Consul](https://img.shields.io/badge/Consul-F24C53?style=flat-square&logo=consul&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat-square&logo=docker&logoColor=white)
![Kubernetes](https://img.shields.io/badge/AKS-326CE5?style=flat-square&logo=kubernetes&logoColor=white)
<br>

**🔍 可观测性** &nbsp;
![OpenTelemetry](https://img.shields.io/badge/OpenTelemetry-000000?style=flat-square&logo=opentelemetry&logoColor=white)
![Prometheus](https://img.shields.io/badge/Prometheus-E6522C?style=flat-square&logo=prometheus&logoColor=white)
![Grafana](https://img.shields.io/badge/Grafana-F46800?style=flat-square&logo=grafana&logoColor=white)
![Jaeger](https://img.shields.io/badge/Jaeger-00ADE4?style=flat-square&logoColor=white)
![Loki](https://img.shields.io/badge/Loki-F4A020?style=flat-square&logo=grafana&logoColor=white)
![Alertmanager](https://img.shields.io/badge/Alertmanager-E6522C?style=flat-square&logo=prometheus&logoColor=white)
<br>

**🧪 测试** &nbsp;
![xUnit](https://img.shields.io/badge/xUnit-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Moq](https://img.shields.io/badge/Moq-555555?style=flat-square&logoColor=white)
![FluentAssertions](https://img.shields.io/badge/FluentAssertions-99CC00?style=flat-square&logoColor=white)
![AutoFixture](https://img.shields.io/badge/AutoFixture-555555?style=flat-square&logoColor=white)
![Vitest](https://img.shields.io/badge/Vitest-6E9F18?style=flat-square&logo=vitest&logoColor=white)

## 🔄 CI/CD 状态

[![Admin Web Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2Fadmin-web?branchName=dev&label=Admin%20Web)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=2&branchName=dev)
[![IdentityServer Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2Fidentityserver?branchName=dev&label=IdentityServer)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=9&branchName=dev)
[![Notifications Microservice Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2FNotificationsMicroservice?branchName=dev&label=Notifications%20Microservice)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=5&branchName=dev)
[![API Gateway Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2Fapigateway?branchName=dev&label=API%20Gateway)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=3&branchName=dev)
[![Products Microservice Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2FProductsMicroservice?branchName=dev&label=Products%20Microservice)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=1&branchName=dev)
[![Infrastructure Build Status](https://dev.azure.com/lambdazb/MicroservicesDemo/_apis/build/status%2Finfrastructure?branchName=dev&label=Infrastructure)](https://dev.azure.com/lambdazb/MicroservicesDemo/_build/latest?definitionId=4&branchName=dev)

以上徽章动态展示各流水线在 `dev` 分支上的最新运行状态；点击徽章可进入对应的 Azure Pipeline。应用流水线负责构建镜像、推送至 ACR 并部署到 AKS。平台流水线负责基础设施、Ingress 与集群附加组件。

| 类型 | 流水线定义 |
| --- | --- |
| 应用 | [Products Microservice](aks/pipelines/azure-pipelines-products-microservice.yaml) · [API Gateway](aks/pipelines/azure-pipelines-apigateway.yaml) · [IdentityServer](aks/pipelines/azure-pipelines-identityserver.yaml) · [Notifications Microservice](aks/pipelines/azure-pipelines-notifications-microservice.yaml) · [Admin Web](aks/pipelines/azure-pipelines-admin-web.yaml) |
| 人工运维 | [Notifications DLQ Replay](aks/pipelines/azure-pipelines-notifications-dlq-replay.yaml) |
| 平台 | [Infrastructure](aks/pipelines/azure-pipelines-infrastructure.yaml) · [Ingress](aks/pipelines/azure-pipelines-ingress.yaml) · [Cluster Add-ons](aks/pipelines/azure-pipelines-cluster-addons.yaml) |

## ✨ 项目亮点

| # | 亮点 | 说明 |
| --- | --- | --- |
| 1 | **AI 工具链辅助开发** | 在 `.agents/` 下维护项目级 agents 与 skills，覆盖 C# 测试生成、EF Migration、Telemetry 约束、Products Code Review 与前端 UI 规范 |
| 2 | **环境适配的服务发现** | 本地 Docker Compose 使用 Consul 动态注册与发现；AKS 环境使用 Kubernetes Service DNS 与 ClusterIP 路由，不部署或依赖 Consul |
| 3 | **Transactional Outbox 事件驱动通信** | Product Add/Delete/Update 与 ProductOperations Outbox 在同一 PostgreSQL 事务提交，再由后台 Worker 通过 RabbitMQ 异步发送给 Notifications Service |
| 4 | **Redis 缓存 + Decorator 模式** | 基于 Scrutor 的装饰器链把缓存层、遥测层与核心业务层分开，读取场景显著减少数据库直连压力 |
| 5 | **PostgreSQL + EF Core 数据持久化** | 通过 Options 模式管理连接配置，支持指数退避重试，可维护性强 |
| 6 | **OpenTelemetry 全链路追踪** | 前端到网关再到后端与基础设施的完整链路，Trace、Metrics、Logs 统一通过 OTEL Collector 分发 |
| 7 | **Clean Architecture + SOLID + 单元测试** | Products 服务三层分层，依赖方向严格内向；xUnit + Moq 覆盖核心服务用例 |
| 8 | **认证授权与安全会话** | Duende IdentityServer + ASP.NET Core Identity 提供 OIDC/OAuth 2.0 登录、注册、邮箱确认和刷新令牌；网关校验访问令牌与 `products-api` scope，Redis 保存令牌拒绝列表 |
| 9 | **在线与离线通知** | 在线用户通过 Redis 定向路由到 BFF，并由 SSE 实时推送；离线用户通过 Resend 收到邮件 |
| 10 | **容器与 AKS 交付** | Azure Pipelines 构建服务镜像并推送至 Azure Container Registry（ACR），随后使用 `aks/` 中的多环境 Kubernetes 清单部署到 AKS；流水线同时覆盖基础设施、Ingress 与集群附加组件 |
| 11 | **生产级密钥管理** | Azure DevOps Variable Groups 关联 Azure Key Vault 获取敏感配置，部署流水线将其同步为各环境的 Kubernetes Secrets，应用通过 `secretKeyRef` 注入运行时配置 |
| 12 | **多副本安全投递** | Notifications Worker 使用 `FOR UPDATE SKIP LOCKED`、行租约、版本号和指数退避，Pod 故障后可由其他副本接管 |

## 🏗️ 架构图

<p align="center">
  <img src="images/ComponentsDiagram.svg" alt="System Architecture" style="width: 100%; max-width: 900px; height: auto;" />
</p>

**🧭 分层映射**

- Frontend Layer 包含 Next.js UI 与 Admin Web BFF
- Backend Layer 包含 API Gateway、IdentityServer、Products Service 与 Notifications Service
- Products Service 内部按 API、Core、Infrastructure 分层
- Infrastructure Layer 承载 Products DB、Notifications DB、Identity DB、Redis、RabbitMQ 与 Resend 等外部依赖
- Monitoring Services 由 OTEL Collector、Jaeger、Prometheus、Loki、Grafana 和 Alertmanager 组成

**🔗 实线与虚线**

- 实线连接表示同步运行时请求或数据存储流
- 虚线连接表示异步消息或事件通信，例如 Products Service 与 RabbitMQ、Notifications Service 与 Redis Pub/Sub，以及 Redis Pub/Sub 与 Admin Web BFF
- 虚线分组框仅表示逻辑层级边界

**🔄 同步请求路径**

- Browser → Next.js UI → Admin Web BFF → API Gateway (Ocelot) → Products Service / Notifications Service
- 服务分别访问其所属的 Products DB、Notifications DB，并按需使用 Redis Cache

**📨 异步通信路径**

- Products Service → PostgreSQL ProductOperations Outbox → RabbitMQ → Notifications Service → Notifications DB
- Notifications Service 通过 Redis Pub/Sub 将通知异步交给 Admin Web BFF，再由 SSE 推送至 Next.js UI；用户 Offline 时通过 Resend 发送通知邮件

**🔍 可观测路径**

- BFF、API Gateway、IdentityServer、Products Service 与 Notifications Service 推送 Trace、Logs 与 Metrics 至 OTEL Collector
- OTEL Collector → Jaeger（Trace）、Loki（Logs）与 Prometheus（Metrics）
- Prometheus 从 Infrastructure Layer 中的 DB 拉取 Metrics
- Grafana 从 Loki 查询 Logs、从 Prometheus 查询 Metrics；Prometheus 触发 Alertmanager，再由 Alertmanager 推送告警至 Slack

**🧭 服务发现边界**

- Consul 仅用于本地 Docker Compose 演示与开发
- 部署到 AKS 后，网关通过 Kubernetes Service DNS 访问后端服务，服务注册、寻址与负载均衡由 Kubernetes 提供

### 🔐 认证与请求链路

`Next.js UI` 与 `BFF` 是同一个 Admin Web 部署中的逻辑组件，并非两个独立服务。BFF 在服务端维护 NextAuth Session 和令牌，因此浏览器无需直接持有 Access Token 或调用 API Gateway。

| 关系 | 说明 |
| --- | --- |
| `Next.js UI → BFF` | 浏览器通过同源 HTTPS 调用 Next.js API Route |
| `BFF → API Gateway` | BFF 从服务端 Session 读取 Access Token，并以 Bearer Token 代理 API 请求 |
| `Admin Web / Browser ↔ IdentityServer` | 未登录时由 Admin Web 发起 OIDC 登录并重定向浏览器；用户在 IdentityServer 完成注册与邮箱确认、登录；BFF 处理回调、Token 换取、Token 刷新与登出 |
| `API Gateway ⇢ IdentityServer` | 网关获取并缓存 OIDC Metadata/JWKS，在本地校验 JWT 的签名、Issuer、Audience 与有效期 |

## ⚖️ 设计与权衡

### 商品与通知请求并发刷新 Access Token

- **现象：**Access Token 临近到期时，商品列表、通知列表和通知 SSE 回放请求可能分别调用 `/connect/token`。
- **原因：**这些独立请求可能同时携带旧会话 Cookie，在浏览器收到刷新后的 Cookie 前都判断需要刷新。[Auth.js 官方文档](https://authjs.dev/guides/refresh-token-rotation)也指出了并发刷新竞争。
- **当前采用的方案：**仅在调用后端 API 前，且 Cookie 中的 Access Token 进入到期前 60 秒窗口时按需刷新。以每次登录的 Refresh Token 记录 ID 为键，用 Redis 结果键和锁协调各副本；取得锁后再次检查结果，仅锁持有者调用 IdentityServer。成功结果加密存储，TTL 为 30 秒与“距到期前 60 秒窗口还剩的时间减 1 秒”中的较小值；失败结果保留 15 秒。等待者最多等待 8 秒，超时返回可重试的 503；刷新调用最多等待 45 秒，锁有效期 50 秒。BFF 响应将新 Token 写回会话 Cookie；登出时撤销锁并暂时标记会话失效，阻止进行中的刷新重新发布结果。
- **权衡：**为了减少 IdentityServer 的重复调用，接受临近到期时请求等待刷新完成，并依赖 Redis；Redis 不可用时返回错误，不直接绕过锁刷新。30 秒结果缓存覆盖通常相近的并发请求，但锁或结果可能因过期、重启或当前的 `allkeys-lru` 策略提前丢失；此时仍可能重复刷新，因此不保证无条件的“严格一次”。[Redis 文档](https://redis.io/docs/latest/develop/reference/eviction/)说明了键淘汰行为。

## ⚙️ 技术选型

| 分类 | 技术 | 选型原因 |
| --- | --- | --- |
| Backend | .NET 9, ASP.NET Core, EF Core | 成熟生态，支持 OpenTelemetry 原生集成 |
| Gateway | Ocelot | 轻量级 .NET API Gateway，负责集中路由，让客户端与内部服务解耦 |
| Service Discovery | Consul (本地), Kubernetes Service DNS (AKS) | 本地演示动态注册与发现；AKS 通过 ClusterIP Service 提供稳定 DNS、寻址与负载均衡 |
| Architecture | Clean Architecture, SOLID, Decorator, DI | 依赖边界清晰，Scrutor 支持无侵入装饰器链 |
| Database | PostgreSQL + EF Core | 关系型持久化，Npgsql 原生支持 OTEL |
| Cache | Redis | 减少重复读压力；通过 Decorator 模式透明叠加在业务层之外 |
| Identity | Duende IdentityServer, ASP.NET Core Identity, Resend | OIDC/OAuth 2.0 登录、用户注册、邮箱确认与 API scope 授权 |
| Messaging | RabbitMQ, ProductOperations Outbox, Redis Pub/Sub, SSE, Resend | 异步事件传播、持久化补偿、实时定向推送与离线邮件 |
| Secrets | Azure Key Vault, Azure DevOps Variable Groups, Kubernetes Secrets | 集中保存敏感配置，并在部署时按环境安全注入 AKS 工作负载 |
| Observability | OpenTelemetry, OTEL Collector, Prometheus, Grafana, Jaeger, Loki, Alertmanager | OpenTelemetry 是厂商无关的开放标准，使用 W3C Trace Context 贯通浏览器、BFF、网关与微服务；通过 OTLP 与 Collector 将埋点 SDK 和 Jaeger、Loki、Prometheus 等后端解耦，便于统一采集与替换观测后端 |
| Frontend | Next.js, React, TypeScript, TanStack Query | Next.js 提供 SSR、路由与 BFF API Route；React 支持组件化 UI，TypeScript 提供类型安全，TanStack Query 统一管理服务端状态的请求、缓存与失效 |
| Testing | xUnit, Moq, FluentAssertions, AutoFixture | 轻量可读，符合 .NET 社区主流实践 |
| Delivery | Docker Compose, AKS, Azure Pipelines | 本地可重复环境与 dev/qa/staging/uat/prod 多环境部署资产 |

## 💡 核心功能

**📐 产品管理（Products Service）**

- Product 的增删改查（CRUD）Endpoint，通过 Ocelot Gateway 对外暴露
- Add Product 要求客户端提供 canonical UUID v4 格式的 `Idempotency-Key`；PostgreSQL 以 `(UserId, Operation, IdempotencyKey)` 唯一约束保存最终响应，同一请求重试返回相同的成功响应，payload 不一致返回冲突错误
- Product Add/Delete/Update 通过 ProductOperations Outbox 与 RabbitMQ 异步交给 Notifications Service

**🚀 服务治理（Gateway + Consul / Kubernetes DNS）**

- API Gateway 统一对外，客户端无需感知内部服务地址
- 本地 Docker Compose 中，服务自注册到 Consul，网关按服务名动态发现并路由
- AKS 环境不使用 Consul；网关通过 Kubernetes Service DNS 访问 ClusterIP Service

**🛡️ 弹性投递（数据库租约 + 指数退避）**

- ProductOperations Outbox 与 Notifications Delivery Worker 都通过短事务领取记录，将 RabbitMQ、Redis 和 Resend 等网络调用放在事务外
- Worker 使用租约、版本条件更新、最多 5 次指数退避；Pod 宕机或 ACK 超时后可安全重投或由其他副本接管

**🔐 身份认证（IdentityServer + Admin Web）**

- Admin Web 使用 OIDC Authorization Code Flow 登录，并维护服务端会话与令牌刷新
- 支持用户注册、Resend 邮箱确认与 Redis 频率限制；`NormalizedEmail` 上的唯一索引与 `RequireUniqueEmail` 共同保证邮箱大小写无关的唯一性，即使并发注册绕过远程校验也不会产生重复账户；Products 路由要求有效 Bearer Token 和 `products-api` scope
- 唯一索引通过 EF Migration 管理，并由 IdentityServer 启动时的 `Database.Migrate()` 自动应用；EF 的迁移历史确保同一数据库只执行一次
- 登出时将访问令牌加入 Redis 拒绝列表，网关可配置为校验失败时拒绝访问

**📨 消息与通知可靠性**

- **原子写入**：Product Add/Delete/Update 与 `ProductOperationOutbox` 使用同一个 EF Core 工作单元，通过一次 `SaveChanges` 原子提交。
- **可靠发布**：Outbox Dispatcher 使用 publisher confirm 发布 `products.operation.completed`；`NotificationId`（UUID v7）同时作为 RabbitMQ `MessageId` 和端到端幂等键。
- **幂等消费与故障接管**：Notifications Consumer 按消息哈希幂等落库；Delivery Worker 使用 PostgreSQL 行租约协调多副本，RabbitMQ DLQ 处理无法投递的消息，Redis Pub/Sub 只承担实时路由，不承担持久化。
- **死信与人工重放**：Notifications 使用 quorum 主队列；队列声明设置 10 秒消息 TTL 和投递次数限制，RabbitMQ 将符合死信条件的消息送往 DLQ。操作人员手动运行 Azure DevOps 流水线，在 AKS 中启动一次性重放 Job。
- **在线与离线投递**

  - **投递规则**：在线投递等待浏览器 ACK，失败后重试并降级邮件；`SequenceNumber`、`Last-Event-ID` 和 BFF Presence 支持通知排序与断线补偿，用户离线时通过 Resend 发送邮件。
  - **通知工作流程**：

    1. Products Service 在同一数据库事务中提交商品操作和 `ProductOperationOutbox`。Outbox Dispatcher 将结果发布到 RabbitMQ；Notifications Consumer 校验消息，按 `NotificationId` 和 payload hash 幂等写入 Notifications DB，成功后确认 RabbitMQ 消息。
    2. 用户登录 Admin Web 后，浏览器通过 `EventSource` 请求 `/api/notifications/stream`。BFF 首次处理该进程的连接时，创建独立的 Redis 订阅连接，订阅 `notifications:bff:{instanceId}`；每个 BFF 进程共用一个频道，`instanceId` 由实例名称与随机 UUID 组成。
    3. 订阅成功后，BFF 为浏览器连接生成 `connectionId`，将 `{instanceId}:{connectionId}` 写入有序集合 `notifications:presence:{userId}`。分数是在线记录的过期时间；默认 TTL 为 45 秒。BFF 随后从 Notifications API 回放持久化通知，暂存回放期间收到的实时消息；回放结束后每 15 秒刷新在线记录，连接关闭时删除该成员。
    4. Delivery Worker 领取待投递通知，清理该用户已过期的在线成员。若仍有有效成员且尚未超过 SSE 尝试次数，Worker 提取 BFF 实例 ID，向对应频道发布通知。BFF 再按 `userId` 找到本进程的 SSE 连接并推送给浏览器；浏览器调用 ACK 接口后，通知变为 `DeliveredInApp`。
    5. 没有有效在线记录时，Worker 直接转入 `SendingEmail`；SSE 发布后若 5 秒内未收到 ACK，则再次尝试，默认最多尝试两次 SSE，再通过 Resend 发送邮件。Redis 调用失败会按投递重试策略处理。Pub/Sub 不保存消息；浏览器重连时使用 `Last-Event-ID` 或 `afterSequence` 从 Notifications DB 回放遗漏通知。

**🔍 可观测性（Observability Stack）**

- 所有服务接入 OpenTelemetry，Trace、Metrics、Logs 三路并行
- Grafana 统一展示指标与日志，可从日志 TraceID 直接跳转至 Jaeger Trace
- Alertmanager 触发告警并推送至 Slack

## 🧊 缓存策略

Products Service 通过 Redis 缓存商品数据，详情键与全量列表键分别管理：

- **读取**：按 ID 查询未命中时，从数据库读取并回填详情缓存；商品不存在时写入 `CacheOptions.NullValuePlaceholder` 负缓存。全量列表缓存采用逻辑过期，过期后由后台刷新。
- **写入成功**：首次新增商品成功后清除列表缓存，幂等重放不重复清除；更新或删除成功后，立即清除该商品的详情缓存和全量列表缓存。仅更新成功时，按 `Redis:DelayedDeleteMs` 配置（默认约 2 秒）再次删除这两个键。
- **版本冲突导致更新或删除失败（409）**：尽力清除详情和列表缓存，再原样抛出异常。
- **对象不存在导致更新或删除失败（404）**：始终尽力清除列表缓存；仅当详情键中存在商品数据，而非负缓存占位值时，额外清除详情键。普通 GET 的 404 和其他原因产生的 409 不适用这套失败写入失效规则。

PostgreSQL 与 Redis 不共享事务。缓存删除失败只记录日志；并发读取也可能在删除后回填旧值，因此上述失效不保证写入后的读取立即看到最新数据。高并发下反复发生版本冲突，或大量不存在 ID 的写入请求，可能增加全量列表的缓存未命中。

### 后续改进（尚未实现）

- **持久化缓存失效**：可将失效意图与 Product 更新写入同一个数据库事务，例如扩展现有 ProductOperations Outbox，再由后台任务重试 Redis 删除。这可提高失效最终完成的可靠性；后台任务执行前仍有读取旧缓存的窗口，不提供立即可见保证。
- **版本失效（Version Invalidation）**：在同一数据库事务中更新 Product，并递增详情或列表缓存对应的数据库版本；读取缓存前从权威数据库校验已提交版本，版本不一致就回源读取最新数据。这可保证更新提交后开始的新读取看到最新值；代价是每次读取都要查询数据库版本，增加数据库负载。若版本只存于 Redis，则不能保证强一致性。

## 📁 项目结构

```
.agents/
  agents/                        # 项目级 agent，例如 C# Expert、Expert React Frontend Engineer
  skills/                        # 项目级 skill，例如 csharp-test-gen、ef-migration、products-code-review
src/backend/
  Gateway/ApiGateway/          # Ocelot API Gateway，路由规则见 ocelot.json
  IdentityServer/              # OIDC/OAuth 2.0、用户注册、邮箱确认与令牌签发
  Services/Products/           # Products 微服务
    ProductsMicroservice.Core/           # 业务逻辑、接口契约与 AutoMapper
    ProductsMicroservice.Infrastructure/ # EF Core、Redis 缓存、RabbitMQ 发布、Scrutor 装饰器
    ProductsMicroService.API/            # 控制器、中间件、Consul 注册、OTEL 配置
  Services/Notifications/       # Notifications 的 Core、Infrastructure 与 API 项目
  BuildingBlocks/CommonService/ # 跨服务共用组件：RabbitMQ 基类、TraceContext 中间件
src/frontend/admin-web/        # Next.js 管理台，接入 OTEL
aks/                           # 多环境 Kubernetes 清单与 Azure Pipelines
configs/                       # 监控、告警、日志、数据库配置
docker/                        # 本地开发和演示部署的 Compose 配置
tests/                         # Products、Notifications 与 IdentityServer 单元测试
```

## 🚀 快速启动

**⚡️ 环境要求**：Docker Desktop。完整离线邮件投递需配置 Resend API Token 与已验证发件地址；若需在宿主机编译或运行测试，还需安装 .NET 9 SDK 和 Node.js 20+。

**📑 本地开发环境**（首次启动先创建本地配置）：

```powershell
if (-not (Test-Path docker/dev/.env)) { Copy-Item docker/dev/.env.example docker/dev/.env }
# 编辑 docker/dev/.env，替换示例凭据；真实邮件投递还需填写 Resend 配置
docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml up
```

`docker/dev/.env` 已被 Git 忽略，请勿提交真实密码或 Resend API Token。IdentityServer 本地开发端口为 `8485`。

PostgreSQL 初始化 SQL 只创建逻辑数据库；Products 和 ProductOperations Outbox 表及索引由 EF Core Migration 管理。

### Products 数据库迁移与种子数据

Products API 通过 `MigrateDatabaseAsync()` 先调用 EF Core `Database.MigrateAsync()` 应用所有待执行迁移，再从 Infrastructure 程序集内嵌的 `SeedData/products.json` 导入示例产品。迁移或种子过程失败会记录 Critical 日志并继续抛出异常，因此 API 不会在数据库准备失败时启动。

种子逻辑校验 JSON 数据，按 `ProductId` 查询数据库，只插入缺少的产品；已有产品不会被种子文件覆盖或更新。保存通过一次 `SaveChangesAsync()` 完成。多实例启动时，种子程序使用 Redis 分布式锁 `lock:products-seed-data` 串行执行，并在拿到锁后重新检查已有 ID；锁等待上限为一分钟。运行迁移/种子的 Job 因此同时需要 PostgreSQL 和 Redis 配置与连接。

本地直接运行和 Docker Compose 未设置 `ProductsMigration:RunOnStartup` 时默认在 API 启动阶段执行上述流程。迁移 Job 使用相同 API 镜像及 `--migrate` 参数，完成迁移和种子数据后正常退出，不启动 HTTP 服务。AKS 的五套 Products Deployment 显式设置 `ProductsMigration__RunOnStartup=false`，所以扩容和容器重启不会重复运行初始化；Azure Pipelines 为每次发布创建独立 Job，等待 Job 完成后才部署 API。Job 失败或等待超时会中止该次发布，并收集 Job 状态和日志。

手动部署 AKS 时，先用目标 API 版本的镜像运行 `aks/manifests/shared/backend/products-database-migration.yaml` 对应的 Job，确认成功后再更新 Deployment。Azure DevOps 的 dev、qa、uat、staging、prod 环境需分别启用 **Exclusive lock** 检查；流水线的 `lockBehavior: sequential` 依赖此环境检查来串行化同环境发布。

**📦 演示部署环境**（拉取预构建镜像）：

```powershell
if (-not (Test-Path docker/deploy/.env)) { Copy-Item docker/deploy/.env.example docker/deploy/.env }
# 编辑 docker/deploy/.env，并配置真实的 IdentityServer .pfx 签名证书路径和密码
docker compose --env-file docker/deploy/.env -f docker/deploy/docker-compose.yml up -d
```

默认拉取 `latest`。如需固定到某次 CI 产物，请在 `docker/deploy/.env` 中将 `PRODUCTS_IMAGE_TAG`、`APIGATEWAY_IMAGE_TAG`、`IDENTITYSERVER_IMAGE_TAG`、`NOTIFICATIONS_IMAGE_TAG`、`ADMINWEB_IMAGE_TAG` 改为对应的 `sha-<commit>` tag，然后重新启动：

```powershell
docker compose --env-file docker/deploy/.env -f docker/deploy/docker-compose.yml up -d
```

> 演示部署使用 Production 配置，必须提供仓库外生成的 IdentityServer `.pfx` 签名证书；不要把证书或密码提交到 Git。

**🌐 常用访问地址**：

| 服务 | 地址 |
| --- | --- |
| Admin Web | http://localhost:3000 |
| IdentityServer（开发 / 演示部署） | http://localhost:8485 / http://localhost:8085 |
| API Gateway | http://localhost:9080 |
| Jaeger UI | http://localhost:16686 |
| Grafana | http://localhost:13000 |
| Prometheus | http://localhost:9090 |
| Consul UI | http://localhost:8500 |
| RabbitMQ Management(账户/密码：guest) | http://localhost:15672 |

**📄 推荐演示顺序**：

1. 在Postman中导入 [MicroservicesDemo.postman_collection.json](MicroservicesDemo.postman_collection.json)
2. 通过 Admin Web 或 Postman 调用产品接口
3. 在 Jaeger 中查看请求链路（可观察 Redis、RabbitMQ 的 Span）
4. 在 Grafana 中查看指标与日志，通过 TraceID 从日志跳转至 Trace
5. 在 Consul 中确认服务注册，在 RabbitMQ 中查看队列状态

### RabbitMQ quorum 队列与死信重放

`notifications.products.operations` 由 Notifications 消费者声明为 quorum 队列，同时设置 `x-message-ttl = 10000`（10 秒）与 `x-delivery-limit = 3`，并配置死信路由。死信队列没有 TTL 或长度上限。TTL 只限制消息在主队列中的等待时间，过期消息仍可人工重放。调整这些队列声明参数时，需要重新创建主队列。

Prometheus 的 RabbitMQ 规则只对通知主队列检查待消费消息数和消费者数；`notifications.products.operations.dead-letter` 有消息持续 1 分钟时触发严重告警，经 Alertmanager 发送到 Slack。死信队列平时没有常驻消费者，因此不应用“无消费者”告警。

本地启动时，Notifications 消费者会自动声明主队列及死信路由，无需单独应用 RabbitMQ policy：

```powershell
docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml up -d
```

Notifications 流水线另外构建 `notifications-dlq-replay:<BuildId>` 镜像，但不会自动运行重放。运维人员确认消费者已恢复并检查 DLQ 后，在 Azure DevOps 注册一次 [Notifications DLQ Replay 流水线](aks/pipelines/azure-pipelines-notifications-dlq-replay.yaml)；之后每次从目标环境对应的分支手动点击 **Run pipeline**，选择 `targetEnvironment` 即可。重放流水线通过 Azure DevOps API 查找 `NotificationsMicroservice` 在该分支最近一次成功运行的 `BuildId`，用于选择重放镜像；若没有成功运行则停止。随后流水线使用目标环境的 Kubernetes Service Connection 创建一次性 Job，等待运行结束，并在流水线日志中显示重放结果。该流水线设置了 `trigger: none`，不会随代码提交自动重放。

本地调试时，可在本机 RabbitMQ 启动后直接运行控制台程序。连接和限量参数位于 `NotificationsMicroservice.DlqReplay/appsettings.json`，也可通过环境变量覆盖：

```powershell
dotnet run --project src/backend/Services/Notifications/NotificationsMicroservice.DlqReplay/NotificationsMicroservice.DlqReplay.csproj
```

Job 从 `notifications-microservice-secrets` 读取 RabbitMQ 连接信息。工具默认每次最多检查 10 条、运行 60 秒；仅自动重放死信原因是 `expired` 或 `delivery_limit` 的消息，`rejected` 等原因留在 DLQ 等待排查。同一 `MessageId` 在一次运行中最多重放一次，自定义 `manual-replay-count` header 限制最多 3 轮。发布确认后才 ACK 原死信；如果发布确认和 ACK 之间故障，可能再次发布，Notification 数据库按 `NotificationId` 和原始 payload 去重。

## ❓ 常见问题

1. **Docker Desktop 未启动**
   - 症状：执行 `docker compose up` 或 `docker ps` 时提示无法连接 Docker daemon，或容器始终无法创建。
   - 解决方案：启动 Docker Desktop，确认 Docker Engine 正常运行后重新执行 compose 命令。

2. **端口占用导致 Container 启动失败**
   - 症状：启动时出现 `port is already allocated`、`bind for 0.0.0.0:xxxx failed` 等错误。
   - 解决方案：修改 `docker-compose.yml` 中对应服务的端口映射，避开已占用端口后重新启动。

3. **依赖服务未启动或不健康**
   - 症状：业务容器立即退出，或因 PostgreSQL、Redis、RabbitMQ、Consul 等依赖未就绪而反复重启。
   - 解决方案：配置启动依赖与健康检查，并适当增加重试次数、超时时间和启动宽限期。

4. **Slack Webhook URL 配置不正确**
   - 症状：告警规则已触发，但 Slack 没有收到通知。
   - 解决方案：将有效 Webhook URL 写入 Git 忽略的 `configs/secrets/alertmanager-slack-webhook.txt`，然后重启 Alertmanager。

5. **Grafana Dashboard 的 metric name 与实际不一致**
   - 症状：应用和采集链路正常，但 Dashboard 面板显示 `No data`。
   - 解决方案：在 Prometheus 中确认当前指标名称，并同步更新 Grafana dashboard 查询；OpenTelemetry package 升级可能改变导出的指标名。

6. **RabbitMQ 启动时报 `.erlang.cookie: eacces`**
   - 原因：Docker volume 中残留的权限不一致，容器内 `rabbitmq` 用户无法读写 `.erlang.cookie`。
   - 解决方案：删除 `rabbitmq_data` volume 后重新启动，Docker 会以正确权限重建 volume。

   ```powershell
   docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml down
   docker volume rm <your-compose-project-name>_rabbitmq_data
   docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml up
   ```

7. **PostgreSQL Seed Data 未正确导入**
   - 症状：数据库能够正常启动，但预期的初始数据没有导入，或数据库中的 Seed Data 与当前代码不一致。
   - 原因：PostgreSQL 使用已有的 `postgres_data` volume 时会跳过首次初始化脚本；修改 Seed Data 后，已有数据库也不会自动重新导入。
   - 解决方案：确认不再需要本地数据库中的现有数据后，停止 Compose 并删除对应的 `postgres_data` volume，再重新启动环境。PostgreSQL 会重新创建数据库并执行初始化与 Seed Data 导入。

   ```powershell
   docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml down
   docker volume rm <your-compose-project-name>_postgres_data
   docker compose --env-file docker/dev/.env -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml up
   ```

   > 删除 volume 会清空本地 PostgreSQL 数据，仅适用于开发环境或已确认数据可以重建的情况。

## ✅ 测试与验证

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

后端测试覆盖产品 CRUD、消息幂等、API Controller、异常处理中间件、AutoMapper 映射、Redis 缓存装饰器与 OpenTelemetry 装饰器，以及 IdentityServer 的登录、注册、邮箱确认与重发流程；前端使用 ESLint、Vitest 和 Next.js production build 验证。

## 💪 工程能力

- **微服务拆分与分层设计**：明确 Admin Web、API Gateway、Products Service、Notifications Service 的职责边界；Products 服务严格遵循 Clean Architecture
- **同步与异步通信**：Ocelot 统一同步路由，本地使用 Consul、AKS 使用 Kubernetes Service DNS；RabbitMQ 主队列与 DLQ 解耦并保护异步链路
- **缓存策略设计**：通过 Scrutor Decorator 链透明叠加 Redis 缓存，并处理更新、删除场景下的缓存失效
- **可观测性方案搭建**：前后端接入 OpenTelemetry，由 OTEL Collector 分发 Trace、Metrics、Logs，Grafana 与 Alertmanager 提供统一观测和告警
- **配置与密钥管理**：Options 模式管理组件配置，Azure Key Vault、Variable Groups 与 Kubernetes Secrets 管理敏感值
- **单元测试与可维护性**：使用 xUnit、Moq、FluentAssertions 与 AutoFixture 覆盖核心行为

## 🎯 后续可扩展方向

- **Product 详情页面**：在 Admin Web 增加按 ID 查询的商品详情页，展示商品信息，并复用现有的按 ID 查询接口及详情缓存。
- **Products Pod 自动伸缩（HPA）**：后续可为 dev、qa、uat、staging、prod 环境的 Products Deployment 配置 Horizontal Pod Autoscaler，根据 CPU 利用率自动调整 Pod 数量。启用前需确保各容器设置合理的 CPU requests，且 AKS 提供资源指标 API；实际扩容还受节点可用容量和 PostgreSQL 连接及处理能力限制。增加副本会消耗更多集群资源并提高数据库并发负载，因此需结合负载测试设定伸缩范围。当前尚未部署 HPA。
- 引入 Saga 模式处理跨服务分布式一致性问题
- 增加可选的 TOTP 多因素认证：在 IdentityServer 中提供账户安全设置页，允许普通用户绑定 Google Authenticator 或 Microsoft Authenticator 等验证器 App；登录时在密码验证后校验 6 位动态验证码，并提供一次性恢复码、重置验证器和安全审计记录。当前 Demo 暂不强制启用 2FA；若面向管理员或高敏感操作，可将 TOTP 调整为强制策略。邮箱验证码可作为恢复或过渡方案，但不作为最终的强认证方式

## 🖼️ 截图与证据说明

以下截图展示管理后台、身份邮件、CI/CD、云端资源、AKS 运行状态，以及网关路由、服务发现、链路追踪、指标监控、日志关联、异步消息与告警链路的实际运行结果。

### 🖥️ 管理后台与身份邮件

#### 📦 Products 管理页面

该页面展示登录后的产品工作区，包括库存数量、库存总价值、平均价格，以及产品新增、编辑和删除入口，证明 Admin Web 已与受保护的 Products API 完成集成。

![Products Management Page](images/ProductsListPage.png)

#### ✉️ Resend 邮件投递

Resend 控制台中的投递记录展示中英文账户确认邮件均已成功送达，验证 IdentityServer 注册与邮箱确认链路可用。

![Resend Email Delivery](images/ResendService.png)

### 🔄 CI/CD 与 Azure 交付证据

#### ✅ Azure Pipelines 运行总览

流水线总览展示 Admin Web、IdentityServer、API Gateway、Products、Notifications Service、基础设施、Ingress 与集群附加组件等流水线的运行状态。

![Azure Pipelines Run Overview](images/AllPipelinesRunResult.png)

#### 🧪 Products Microservice 流水线

Products 流水线依次完成镜像构建与推送、单元测试和 dev 环境部署；截图同时展示测试通过率、代码覆盖率以及按条件控制的后续环境部署阶段。

![Products Microservice Pipeline Run Detail](images/ProductsMicroservicePipelineRunDetail.png)

#### 🏗️ 基础设施流水线

基础设施流水线成功完成 dev 环境资源部署，证明 Azure 基础设施定义能够通过独立 Pipeline 重复执行。

![Infrastructure Pipeline Run Detail](images/InfrastructurePipelineRunDetial.png)

#### 🔐 Azure DevOps Variable Groups

Variable Groups 按应用和职责拆分全局配置与 Key Vault 配置，为不同服务的部署流水线提供集中、可复用的环境变量来源。

![Azure DevOps Variable Groups](images/AllVariableGroups.png)

#### 📦 Azure Container Registry

ACR 中已创建 Admin Web、API Gateway、IdentityServer、Products 和 Notifications Service 镜像仓库，证明应用流水线能够发布各服务的容器镜像。

![Azure Container Registry Repositories](images/AzureContainerRegistry.png)

#### 🔑 Azure Key Vault

Key Vault 中的敏感值名称已在截图中遮挡；启用状态证明部署所需密钥由集中式密钥库管理，而非直接写入仓库或流水线定义。

![Azure Key Vault Secrets](images/AzureKeyVault.png)

### ☸️ AKS 运行状态

#### 🚀 dev Namespace Pods

`dev` Namespace 中的前端、网关、身份服务、业务服务、数据组件及可观测性组件均处于 `Running` 且容器已 Ready，展示完整环境在 AKS 中的实际部署状态。

![AKS dev Namespace Pods](images/AllPods.png)

#### 🌐 Nginx Ingress Pod

AKS 应用路由 Namespace 中的 Nginx Pod 处于 `Running` 和 Ready 状态，为外部域名流量提供 Ingress 入口。

![AKS Nginx Ingress Pod](images/NginxPod.png)

### 🔍 服务发现证据

**看点：** Consul 截图证明本地 Docker Compose 环境已实现动态注册与发现；AKS 部署则使用 Kubernetes Service DNS。

![Consul](images/Consul.png)

### 🔭 链路追踪、指标与日志

**看点：** Jaeger 截图证明 OIDC 登录、令牌签发以及业务请求已经接入分布式追踪，并展示请求穿过网关、API、Redis 及 RabbitMQ 相关 Span；Grafana 与日志跳转链路证明指标和日志可围绕同一个分布式 Trace 上下文联动排查。

#### 🔐 IdentityServer OIDC 登录流程

该截图展示一次登录操作产生的四段认证 Trace：`POST /Account/Login` 验证用户并建立登录会话，授权回调继续原始 Authorize 请求，Discovery 请求获取 OIDC 元数据，最后由 BFF 调用 Token Endpoint。浏览器前端通道与 BFF 后端通道经过重定向和独立 HTTP 请求，因此在 Jaeger 中显示为多条 Trace。

![IdentityServer OIDC Login Flow](images/JaegerIdentityServerLoginFlow.png)

#### 🎫 Authorization Code Token Exchange

Token Endpoint Trace 展示 BFF 使用一次性 Authorization Code 换取 Token 的内部过程：IdentityServer 读取并删除授权码、验证 Client 与 Scope，随后创建 Access Token、Refresh Token 和 Identity Token，并使用签名凭据生成 JWT。授权码兑换后立即删除，可防止同一授权码被重复使用。

<details>
<summary>展开查看完整 Token Exchange Trace</summary>

![IdentityServer Authorization Code Token Exchange](images/JaegerIdentityServerTokenExchange.png)

</details>

#### 📊 Jaeger POST 链路

该历史截图展示 Jaeger 对 RabbitMQ 消息处理过程的捕获；当前实现使用 `products.operation.completed`，并由 Notifications Consumer 创建消费 Span。

![Jaeger POST Flow](images/JaegerTracePostFlow.png)

#### 📊 Jaeger GET 链路

![Jaeger GET Flow](images/JaegerTraceGetFlow.png)

#### 📊 Jaeger DELETE 链路

![Jaeger DELETE Flow](images/JaegerTraceDeleteFlow.png)

#### 🔗 Jaeger Trace 关联日志

该截图展示从 Jaeger Trace 直接定位日志条目，连接分布式链路与应用日志以辅助根因分析。

![Jaeger Trace to Log](images/JaegerTraceToLog.png)

#### 🔄 日志跳转 Jaeger Trace

该截图展示反向关联：从日志条目通过 TraceID 跳转至对应的 Jaeger Trace。

![Log to Jaeger Trace](images/LogToJaegerTrace.png)

#### 📈 Jaeger Monitor

![Jaeger Monitor](images/JaegerMonitor.png)

#### 📊 Grafana OTEL 指标

![Grafana OTEL Metrics](images/GrafanaOTELMetrics.png)

### 📨 消息队列与告警

**看点：** RabbitMQ Exchange 与 Queue 截图展示异步路由和缓冲；Slack 截图展示消息异常信号可转化为可执行告警。

#### 🔄 RabbitMQ Exchange

![RabbitMQ Exchange](images/RabbitMQ_Exchange.png)

#### 📦 RabbitMQ Queue

![RabbitMQ Queue](images/RabbitMQ_Queue.png)

#### 📢 RabbitMQ Slack 告警消息

![Slack Alert Message](images/SlackAlertMessageFromRabbitMQ.png)

## 🤝 参与贡献

欢迎贡献！提交前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)，了解分支策略、提交信息格式、代码质量要求与 PR 指南。修改文档时，请同步更新 [英文 README](README.en.md)。

## 📄 许可证

本项目采用 [MIT License](LICENSE)。
