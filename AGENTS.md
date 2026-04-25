# Project Guidelines

## Overview

.NET 9 microservices demo using Clean Architecture, Ocelot API Gateway, Consul service discovery, RabbitMQ, PostgreSQL, Redis, and OpenTelemetry. See [README.md](README.md) for setup instructions.

## Build & Test

```bash
# Build entire solution
dotnet build MicroservicesDemo.sln

# Run unit tests
dotnet test tests/ProductsServiceUnitTests/ProductsServiceUnitTests.csproj

# Start full environment (all infra + services)
docker compose -f docker/dev/docker-compose.yml -f docker/dev/docker-compose.override.yml up
```

## Architecture

### Clean Architecture Layers (Products Microservice)

| Layer | Project | Responsibility |
|-------|---------|----------------|
| Core | `ProductsMicroservice.Core` | Entities, DTOs, service interfaces, repository contracts, AutoMapper profiles, Polly policies |
| Infrastructure | `ProductsMicroservice.Infrastructure` | EF Core repos, Redis caching, RabbitMQ publishers, Scrutor decorators, DI registration |
| API | `ProductsMicroService.API` | Controllers, middleware, configuration, Dockerfile |

Dependencies flow inward: API → Infrastructure → Core. Never reference outer layers from inner layers.

### Shared Library

`CommonService` (`src/backend/BuildingBlocks/CommonService/`) — cross-cutting concerns: RabbitMQ base classes, middleware, shared messages.

### Communication Patterns

- **Sync**: HTTP via Ocelot gateway + Consul service discovery (see [ocelot.json](src/backend/Gateway/ApiGateway/ocelot.json))
- **Async**: RabbitMQ events (e.g., `products.add` published by ProductsAdderService)
- **Caching**: Redis with `RedisDataWrapper<T>` serialization

## Naming Conventions

| Element | Pattern | Example |
|---------|---------|---------|
| Service interface | `I[Entity][Action]Service` | `IProductsAdderService` |
| Service impl | `[Entity][Action]Service` | `ProductsAdderService` |
| Repository | `I[Entity]Repository` / `[Entity]Repository` | `IProductsRepository` |
| Controller | `[Entity]sController` | `ProductsController` |
| Decorator | `[Entity][Action][Concern]Decorator` | `ProductsAdderTelemetryDecorator` |
| AutoMapper profile | `[Source]To[Dest]MappingProfile` | `ProductAddRequestToProductMappingProfile` |
| DTOs | `[Entity][Action]Request`, `[Entity]Response` | `ProductAddRequest`, `ProductResponse` |
| Test class | `[ServiceName]Tests` | `ProductsAdderServiceTests` |

## DI Registration

Each layer exposes an extension method on `IServiceCollection`:

```csharp
builder.Services.AddProductsMicroserviceCore(builder.Configuration);
builder.Services.ProductsMicroserviceInfrastructure(builder.Configuration);
```

- **Scoped**: Services (per-request)
- **Singleton**: Repositories, RabbitMQ publishers, Polly policies
- **Scrutor**: Used for decorator chaining (caching → telemetry → service)

## Configuration

- Options pattern with strongly-typed classes: `PostgresOptions`, `RedisOptions`, etc.
- Environment variable binding via `__` separator (e.g., `POSTGRES__HOST`)
- See [docker-compose.override.yml](docker/dev/docker-compose.override.yml) for all env vars

## Error Handling

- `ExceptionHandlingMiddleware` catches unhandled exceptions globally
- Response shape: `{ Message, Type, Detail }`
- Use `ArgumentNullException.ThrowIfNull()` for null-guard at service entry points

## Testing Conventions

- **Framework**: xUnit (`[Fact]` attributes) with FluentAssertions
- **Mocking**: Moq for all dependencies
- **Data**: AutoFixture for test data generation
- **Structure**: One test file per service, mock fields + constructor setup, section comments (`#region`)
- Tests live in `tests/ProductsServiceUnitTests/`

When writing tests, follow the pattern in existing test files — inject mocks via constructor, assert with FluentAssertions `.Should()` syntax.

## Adding a New Microservice

1. Create three projects under `src/backend/Services/[Name]/`: `[Name].Core`, `[Name].Infrastructure`, `[Name].API`
2. Follow the Products service as a reference for layering and DI registration
3. Add Consul registration in `Program.cs` for service discovery
4. Add route in [ocelot.json](src/backend/Gateway/ApiGateway/ocelot.json) for gateway routing
5. Add service definition to both docker-compose files under `docker/`
