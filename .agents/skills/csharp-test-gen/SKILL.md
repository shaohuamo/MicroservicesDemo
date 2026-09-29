---
name: csharp-test-gen
description: Generate comprehensive xUnit test suites with fixtures and edge cases for C# code. Use this skill whenever the user needs to create unit tests for C# classes, methods, or services. Include test cases for happy paths, edge cases, error conditions, and use mocking (Moq) for dependencies. Output complete test files with fixture setup and teardown, parametrized tests, and test data builders.
---

# C# Unit Test Generator (xUnit)

Generate production-ready xUnit test suites with comprehensive coverage, fixtures, mocking, and edge case handling for C# code.

## What This Skill Does

When given a C# class or method, this skill:
1. **Analyzes the code** to identify methods, dependencies, return types, and edge cases
2. **Generates xUnit test class** with IAsyncLifetime fixtures where appropriate
3. **Creates test cases** covering:
   - Happy path (normal operation)
   - Edge cases (null, empty, boundary values)
   - Error conditions (exceptions, invalid input)
   - Async patterns if methods are async
4. **Generates mock fixtures** using Moq for dependencies
5. **Produces test data builders** for complex objects

## When to Use This Skill

- Writing tests for a new C# class
- Expanding test coverage for existing methods
- Creating tests for services with dependencies (repositories, external APIs)
- Generating tests for business logic with multiple edge cases
- Setting up mocking for unit tests

Do NOT use this skill for:
- Integration tests (these need real databases, APIs)
- UI/WinForms/WPF testing (use different frameworks)
- Load testing or performance benchmarks

## Input Format

Provide:
1. **The C# code to test** - class, method, or service
2. **Dependencies or interfaces** - any external dependencies the code uses
3. **Test scenarios you care about** (optional) - specific edge cases or bugs to cover

Example input:
```csharp
public class OrderService
{
    private readonly IOrderRepository _repository;
    private readonly IPaymentProcessor _payment;

    public OrderService(IOrderRepository repository, IPaymentProcessor payment)
    {
        _repository = repository;
        _payment = payment;
    }

    public async Task<Order> CreateOrder(CreateOrderRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Items.Count == 0) throw new InvalidOperationException("Order must have items");
        
        var order = new Order { Id = Guid.NewGuid(), Items = request.Items };
        await _payment.ProcessAsync(order.Total);
        await _repository.SaveAsync(order);
        return order;
    }
}
```

## Output Format

The skill generates:

### 1. Test Class (`OrderServiceTests.cs`)
- Uses xUnit with Moq for mocking
- IAsyncLifetime fixture for async setup/teardown
- Theory tests for parametrized scenarios
- Clear, descriptive test names following `MethodName_Scenario_ExpectedResult` pattern

Example structure:
```csharp
public class OrderServiceTests : IAsyncLifetime
{
    private readonly Mock<IOrderRepository> _repositoryMock;
    private readonly Mock<IPaymentProcessor> _paymentMock;
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _repositoryMock = new Mock<IOrderRepository>();
        _paymentMock = new Mock<IPaymentProcessor>();
        _service = new OrderService(_repositoryMock.Object, _paymentMock.Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateOrder_WithValidRequest_ReturnsOrder()
    {
        // Arrange
        var request = TestOrderBuilder.Default().Build();
        _paymentMock.Setup(x => x.ProcessAsync(It.IsAny<decimal>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CreateOrder(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateOrder_WithInvalidRequest_ThrowsException(CreateOrderRequest request)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CreateOrder(request));
    }
}
```

### 2. Test Data Builders (in same file or separate file)
- Fluent builder pattern for creating test objects
- Sensible defaults
- Methods to override specific properties

Example:
```csharp
public class TestOrderBuilder
{
    private string _orderId = Guid.NewGuid().ToString();
    private List<OrderItem> _items = new() { new OrderItem { Sku = "TEST-001", Qty = 1 } };
    private decimal _total = 99.99m;

    public static TestOrderBuilder Default() => new();

    public TestOrderBuilder WithOrderId(string id)
    {
        _orderId = id;
        return this;
    }

    public TestOrderBuilder WithItems(List<OrderItem> items)
    {
        _items = items;
        return this;
    }

    public CreateOrderRequest Build() => new()
    {
        OrderId = _orderId,
        Items = _items,
        Total = _total
    };
}
```

## Test Case Patterns

For each public method, the skill generates:

| Pattern | Purpose | Example |
|---------|---------|---------|
| **Happy Path** | Normal operation succeeds | Valid input → correct result |
| **Null/Empty Input** | Null arguments are rejected | `CreateOrder(null)` → throws |
| **Boundary Cases** | Edge values handled correctly | Empty collection, zero, max int |
| **Exception Cases** | Failures handled gracefully | Repository throws → handled |
| **Async Patterns** | Async methods await properly | `async Task<T>` → uses async test |
| **Mock Verification** | Dependencies called correctly | Verify `_repository.Save()` was called |

## Best Practices

1. **One assertion per test** (or grouped related assertions)
2. **Descriptive test names** — read like documentation
3. **AAA pattern** — Arrange, Act, Assert clearly separated
4. **Mock setup is minimal** — only mock what's needed
5. **Test data builders** — readable, maintainable test setup
6. **Async properly** — use `async Task` test methods for async code

## Dependencies Required in Project

```xml
<ItemGroup>
    <PackageReference Include="xunit" Version="2.6.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.0" />
    <PackageReference Include="Moq" Version="4.20.0" />
</ItemGroup>
```

## Example Output

Given a simple validator:
```csharp
public class EmailValidator
{
    public bool IsValid(string email) => !string.IsNullOrEmpty(email) && email.Contains("@");
}
```

The skill generates a complete test file with:
- Happy path test (valid email)
- Null/empty edge cases
- Missing @ symbol case
- Multiple @ symbols case
- Whitespace handling
- All with clear names and proper structure
