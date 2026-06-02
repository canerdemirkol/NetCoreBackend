# Core.Test

İki rolü tek projede birleştirir:

1. **Consuming app'lere shipping edilen test helpers** — `BaseFakeData`, `MockRepositoryHelper`, `BaseMockRepository`, `ValidationErrorCodes`. NuGet üzerinden tüketilir.
2. **Framework'ün kendi regression test suite'i** — `dotnet test` ile çalışan xUnit testleri (cascade guard, validation race, paginate, AES-GCM, outbox vs.).

> Test SDK paketleri (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `coverlet.collector`, `FluentValidation`, `Microsoft.EntityFrameworkCore.InMemory`) `PrivateAssets="all"` ile referans edilir — tüketici uygulamaya transitive olarak akmaz. Consumer sadece `Moq`, `AutoMapper`, `Microsoft.EntityFrameworkCore` bağımlılıklarını alır.

## Çalıştırma

```bash
# Framework testleri
dotnet test Core.Test/Core.Test.csproj

# Tüm solution
dotnet test
```

---

## Helpers (consumer-facing)

## BaseFakeData

Entity için test verisi üreten soyut sınıf:

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

Moq tabanlı repository mock'u oluşturur. Standart CRUD metotları otomatik setup edilir:

```csharp
var mockRepo = MockRepositoryHelper.GetRepository<IProductRepository, Product, Guid>(fakeDataList);

// Otomatik setup edilen metotlar:
// GetListAsync, GetAsync, AddAsync, UpdateAsync, DeleteAsync, AnyAsync
```

## BaseMockRepository

Mapper + mock repository + business rules'u birleştiren test base sınıfı:

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

FluentValidation hata kodu sabitleri:

```csharp
ValidationErrorCodes.NotEmptyValidator    // "NotEmptyValidator"
ValidationErrorCodes.MinimumLengthValidator // "MinimumLengthValidator"
ValidationErrorCodes.EmailValidator       // "EmailValidator"
```

Validation sonuçlarını assert ederken kullanılır:

```csharp
var errors = validator.Validate(command).Errors;
Assert.Contains(errors, e => e.ErrorCode == ValidationErrorCodes.NotEmptyValidator);
```

---

## Framework regression suite (internal)

`Core.Test/Application/`, `Persistence/`, `Security/`, `Outbox/` altındaki xUnit testleri framework'ün kendi davranışını koruyor. Bunlar consumer paketine **gitmez** — sadece `dotnet test` zamanında compile ve çalıştırılırlar.

| Test sınıfı | Kapsam |
|---|---|
| `RequestValidationBehaviorTests` | R2 fix regression — concurrent validator'larda per-context isolation |
| `TenantCascadeTests` | R1 fix regression — soft-delete cascade tenant guard + `TenantSetter null` hard error |
| `PaginateTests` | R3 fix regression — `size <= 0` ve `from > index` guard'ları |
| `AesGcmEncryptionHelperTests` | round-trip, tamper detection, key mismatch, associated-data mismatch |
| `EfOutboxStoreTests` | `FetchDueAsync` ordering + filtering, `AppendAsync` non-save semantik, `RecordFailureAsync` bookkeeping |
