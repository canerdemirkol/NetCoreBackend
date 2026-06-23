# Core.Test

Combines two roles in a single project:

1. **Test helpers shipped to consuming apps** — `BaseFakeData`, `MockRepositoryHelper`, `BaseMockRepository`, `ValidationErrorCodes`. Consumed via NuGet.
2. **The framework's own regression test suite** — xUnit tests run with `dotnet test` (cascade guard, validation race, paginate, AES-GCM, outbox, etc.).

> The test SDK packages (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `coverlet.collector`, `FluentValidation`, `Microsoft.EntityFrameworkCore.InMemory`) are referenced with `PrivateAssets="all"` — they do not flow transitively to the consuming application. The consumer only gets the `Moq`, `AutoMapper`, and `Microsoft.EntityFrameworkCore` dependencies.

## Running

```bash
# Framework tests
dotnet test Core.Test/Core.Test.csproj

# The whole solution
dotnet test
```

---

## Helpers (consumer-facing)

## BaseFakeData

An abstract class that generates test data for an entity:

```csharp
public class ProductFakeData : BaseFakeData<Product, Guid>
{
    public override List<Product> CreateFakeData()
    {
        return new Faker<Product>()
            .RuleFor(p => p.Id, f => f.Random.Guid())
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Price, f => f.Finance.Amount())
            .Generate(10);
    }
}
```

## MockRepositoryHelper

Creates a Moq-based repository mock. The standard CRUD methods are set up automatically:

```csharp
var mockRepo = MockRepositoryHelper.GetRepository<IProductRepository, Product, Guid>(fakeDataList);

// Automatically set up methods:
// GetListAsync, GetAsync, AddAsync, UpdateAsync, DeleteAsync, AnyAsync
```

## BaseMockRepository

A test base class that combines mapper + mock repository + business rules:

```csharp
public class GetProductTests : BaseMockRepository<
    IProductRepository,
    Product,
    Guid,
    ProductMappingProfile,
    ProductBusinessRules,
    ProductFakeData>
{
    private readonly ProductFakeData _fakeData = new();

    public GetProductTests() : base(new ProductFakeData()) { }

    [Fact]
    public async Task GetProduct_WhenExists_ReturnsProduct()
    {
        var existingId = _fakeData.Data[0].Id;
        var handler = new GetProductQueryHandler(MockRepository.Object, Mapper, BusinessRules);
        var result = await handler.Handle(new GetProductQuery { Id = existingId }, CancellationToken.None);
        Assert.NotNull(result);
    }
}
```

## ValidationErrorCodes

FluentValidation error code constants:

```csharp
ValidationErrorCodes.NotEmptyValidator    // "NotEmptyValidator"
ValidationErrorCodes.MinimumLengthValidator // "MinimumLengthValidator"
ValidationErrorCodes.EmailValidator       // "EmailValidator"
```

Used when asserting on validation results:

```csharp
var errors = validator.Validate(command).Errors;
Assert.Contains(errors, e => e.ErrorCode == ValidationErrorCodes.NotEmptyValidator);
```

---

## Framework regression suite (internal)

The xUnit tests under `Core.Test/Application/`, `Persistence/`, `Security/`, and `Outbox/` protect the framework's own behavior. These **do not** go into the consumer package — they are compiled and run only at `dotnet test` time.

| Test class | Scope |
|---|---|
| `RequestValidationBehaviorTests` | R2 fix regression — per-context isolation in concurrent validators |
| `TenantCascadeTests` | R1 fix regression — soft-delete cascade tenant guard + `TenantSetter null` hard error |
| `PaginateTests` | R3 fix regression — `size <= 0` and `from > index` guards |
| `AesGcmEncryptionHelperTests` | round-trip, tamper detection, key mismatch, associated-data mismatch |
| `EfOutboxStoreTests` | `FetchDueAsync` ordering + filtering, `AppendAsync` non-save semantics, `RecordFailureAsync` bookkeeping |
