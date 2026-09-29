---
name: products-code-review
description: Review ProductsMicroservice changes for telemetry placement, cancellation flow, dependency design, and cross-layer consistency.
---

# ProductsMicroservice Code Review

Use this skill for read-only review of ProductsMicroservice code changes. Review the complete relevant diff and surrounding call sites, then report only concrete, actionable regressions introduced by the change. Do not modify files, create commits, or stage changes.

## Required review rules

### 1. Controller telemetry boundary

`ProductsController` must not carry business-operation Telemetry instrumentation. It may validate requests, call application services, and construct HTTP responses, but it must not add operation-specific `Activity` tags, metrics, telemetry events, or telemetry logs.

### 2. Core service telemetry boundary

Products Core Services must not carry Telemetry instrumentation. Do not accept or use `Activity.Current`, `ActivitySource`, meters, counters, histograms, or operation-specific telemetry logging in Core application services. Core services may return business outcome data required by an outer decorator, such as `ProductAddResult.IsReplay`.

### 3. Products telemetry placement

For Products operations other than Hosted Services, Telemetry instrumentation must be implemented in the corresponding Infrastructure `TelemetryDecorator` under:

```text
src/backend/Services/Products/ProductsMicroservice.Infrastructure/Decorators/Observability/
```

The decorator should own operation tags, metrics, timing, outcome classification, and telemetry-related logs. Products Hosted Services are the explicit exception: their background-operation instrumentation may remain in the Hosted Service because there is no request decorator pipeline.

When reviewing an operation, verify that the decorator is actually part of the DI decorator chain and that telemetry is not duplicated in the Controller or Core service.

### 4. Observability extension boundary

Any AddOpenTelemetry registration must be contained within an extension method named AddObservability. Do not register AddOpenTelemetry directly in Program.cs or in an extension method with another name. When reviewing observability setup, verify that the application startup calls AddObservability and that the complete OpenTelemetry configuration remains inside that method.
### 4. CancellationToken flow

Do not flag every `CancellationToken`. Identify redundant propagation in the request-facing Products service layer. A Service method should not expose or forward a request `CancellationToken` when the implementation does not need cancellation semantics and the cancellation is only being passed through the application pipeline.

Cancellation tokens remain appropriate for genuine infrastructure and lifecycle operations, including EF Core, Redis, RabbitMQ, repository I/O, and Hosted Service shutdown via `stoppingToken`.

When a Service signature changes, inspect its interface, implementation, decorators, Controller call sites, DI registration, and tests for consistency.

### 5. Avoid unnecessary production dependencies for tests

Do not introduce a production abstraction or dependency solely to make a simple implementation easier to test. In particular, do not require `TimeProvider` in Products business code only to control the clock in tests when `DateTimeOffset.UtcNow` is sufficient for the current requirement.

Review constructor dependencies, DI registrations, and test-only accommodations together. A dependency is justified only when it provides production behavior or an explicitly required runtime policy, not merely because it makes tests easier.

### 6. Cross-layer synchronization

After any implementation change, inspect all related layers and call sites:

- Service contracts and interfaces
- Core service implementations
- Infrastructure decorators
- Infrastructure DI/decorator registration
- Controllers and other callers
- Unit and integration tests

Flag stale overloads, mismatched signatures, missing decorator registration, obsolete constructor arguments, tests that no longer exercise the changed contract, or production code that still contains the old behavior.

## Review workflow

1. Read the applicable `AGENTS.md` instructions.
2. Inspect the full requested diff and relevant surrounding files.
3. Search Products code for `Activity`, `ActivitySource`, `Meter`, `Counter`, `Histogram`, `CancellationToken`, and changed method names.
4. Trace changed service contracts through implementations, decorators, DI, Controllers, and tests.
5. Verify that any retained exception, such as Hosted Service telemetry or infrastructure cancellation, is justified by its execution boundary.
6. Report findings first, ordered by severity. Do not report style-only issues or pre-existing behavior.

Use this finding format:

```text
[P1] Imperative finding title - path/to/file.cs:line

Short explanation of the concrete scenario, the violated rule, and why the author should fix it.
```

Use `P0` for a critical release blocker, `P1` for an urgent defect, `P2` for an ordinary actionable defect, and `P3` for a lower-impact actionable issue. If no qualifying issues exist, state `No findings.` Then provide a brief overall assessment and mention material test gaps or residual risks.

