---
name: telemetry-instrumentation
description: Apply ProductsMicroservice telemetry instrumentation in TelemetryDecorators rather than controllers or domain services.
---

# ProductsMicroservice Telemetry Instrumentation

Use this skill whenever adding, moving, or reviewing observability instrumentation for the ProductsMicroservice.

## Required placement

- Put request and operation telemetry in the appropriate `TelemetryDecorator` under `ProductsMicroservice.Infrastructure/Decorators/Observability/`.
- Do not add telemetry instrumentation to Controllers, Core application services, repositories, entities, DTOs, or other business logic locations.
- Controllers may perform request validation and return HTTP results, but must not set operation-specific `Activity` tags, metrics, logs, or tracing events.
- Core services may expose the business result needed by the decorator, such as `ProductAddResult.IsReplay`; they must not own telemetry concerns.

## Implementation guidance

- Use the existing decorator chain and `Activity.Current` for trace tags and events.
- Keep telemetry concerns centralized in the decorator that corresponds to the operation, for example `ProductsAdderTelemetryDecorator` for AddProduct.
- Record operation identity and request context at the beginning of the decorator so failed executions are still attributable.
- Record outcome-specific tags after the inner operation returns, including replay or committed status when applicable.
- Record business metrics in the decorator and avoid duplicate metrics in the Controller or Core service.
- Do not put sensitive request bodies, authorization tokens, or unrestricted user input into logs, metric labels, baggage, or trace attributes.
- Reuse existing diagnostic configuration and naming conventions before introducing new meters, tags, or log fields.

## Verification

- Search the Controller and Core service for operation-specific `Activity`, meter, and telemetry logging added by the change; move it to the decorator if found.
- Verify the decorator is registered in the existing DI/decorator chain.
- Update relevant decorator tests and run the Products service test project.
