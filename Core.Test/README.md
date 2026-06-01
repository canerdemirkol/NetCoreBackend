# Core.Test

Unit test yardımcıları. Fake data üretimi, mock repository kurulumu ve AutoMapper test altyapısı.

## BaseFakeData

Entity için test verisi üreten soyut sınıf:

```csharp
public class ProductFakeData : BaseFakeData<Product, Guid>
{
    public override Product GetFakeData()
    {
        return new Faker<Product>()
            .RuleFor(p => p.Id, f => f.Random.Guid())
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Price, f => f.Finance.Amount())
            .Generate();
    }
}
```

## MockRepositoryHelper

Moq tabanlı repository mock'u oluşturur. Standart CRUD metotları otomatik setup edilir:

```csharp
var mockRepo = MockRepositoryHelper.GetRepository<IProductRepository, Product, Guid>(
    fakeDataList,
    p => p.Id
);

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
    [Fact]
    public async Task GetProduct_WhenExists_ReturnsProduct()
    {
        var handler = new GetProductQueryHandler(MockRepository.Object, Mapper, BusinessRules);
        var result = await handler.Handle(new GetProductQuery { Id = FakeData[0].Id }, CancellationToken.None);
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
