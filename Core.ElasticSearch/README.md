# Core.ElasticSearch

Elasticsearch (NEST client) entegrasyonu. Index yönetimi, tam metin arama ve CRUD operasyonları.

## Kurulum

```csharp
// Program.cs
builder.Services.Configure<ElasticSearchConfig>(config.GetSection("ElasticSearchConfig"));
builder.Services.AddSingleton<IElasticSearch, ElasticSearchManager>();
```

```json
{
  "ElasticSearchConfig": {
    "ConnectionString": "http://localhost:9200",
    "UserName": "",
    "Password": ""
  }
}
```

## Operasyonlar

```csharp
// Index oluşturma
await elasticSearch.CreateNewIndexAsync(new IndexModel
{
    IndexName = "products",
    AliasName = "products-alias",
    NumberOfReplicas = 1,
    NumberOfShards = 3
});

// Doküman ekleme
await elasticSearch.InsertAsync(new ElasticSearchInsertUpdateModel
{
    IndexName = "products",
    Item = product
});

// Toplu ekleme
await elasticSearch.InsertManyAsync(new ElasticSearchInsertManyModel
{
    IndexName = "products",
    Items = productList.Cast<object>().ToArray()
});

// Güncelleme
await elasticSearch.UpdateByElasticIdAsync(new ElasticSearchInsertUpdateModel
{
    IndexName = "products",
    ElasticId = "elastic-doc-id",
    Item = updatedProduct
});

// Silme
await elasticSearch.DeleteByElasticIdAsync(new ElasticSearchModel
{
    IndexName = "products",
    ElasticId = "elastic-doc-id"
});
```

## Arama

```csharp
// Tüm sonuçlar (sayfalı)
IList<ElasticSearchGetModel<Product>> results = await elasticSearch.GetAllSearch<Product>(
    new SearchParameters { IndexName = "products", From = 0, Size = 10 });

// Field bazlı arama
IList<ElasticSearchGetModel<Product>> results = await elasticSearch.GetSearchByField<Product>(
    new SearchByFieldParameters
    {
        IndexName = "products",
        FieldName = "name",
        Value = "laptop",
        From = 0, Size = 10
    });

// Basit query string arama
IList<ElasticSearchGetModel<Product>> results = await elasticSearch.GetSearchBySimpleQueryString<Product>(
    new SearchByQueryParameters
    {
        IndexName = "products",
        QueryName = "product-search",
        Fields = ["name", "description"],
        Query = "gaming laptop",
        From = 0, Size = 10
    });
```

## Modeller

```
ElasticSearchModel              ← IndexName, ElasticId
  └── ElasticSearchInsertUpdateModel  ← + Item (object)
  └── ElasticSearchInsertManyModel    ← + Items (object[])

ElasticSearchGetModel<T>        ← ElasticId + T Item (sonuçlar için)

SearchParameters                ← IndexName, From, Size
  └── SearchByFieldParameters   ← + FieldName, Value
  └── SearchByQueryParameters   ← + QueryName, Fields[], Query
```
