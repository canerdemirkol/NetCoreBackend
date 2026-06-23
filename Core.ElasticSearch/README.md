# Core.ElasticSearch

Elasticsearch (NEST client) integration. Index management, full-text search, and CRUD operations.

## Installation

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

## Operations

```csharp
// Create index
await elasticSearch.CreateNewIndexAsync(new IndexModel
{
    IndexName = "products",
    AliasName = "products-alias",
    NumberOfReplicas = 1,
    NumberOfShards = 3
});

// Insert document
await elasticSearch.InsertAsync(new ElasticSearchInsertUpdateModel
{
    IndexName = "products",
    Item = product
});

// Bulk insert
await elasticSearch.InsertManyAsync("products", productList.Cast<object>().ToArray());

// Update
await elasticSearch.UpdateByElasticIdAsync(new ElasticSearchInsertUpdateModel
{
    IndexName = "products",
    ElasticId = "elastic-doc-id",
    Item = updatedProduct
});

// Delete
await elasticSearch.DeleteByElasticIdAsync(new ElasticSearchModel
{
    IndexName = "products",
    ElasticId = "elastic-doc-id"
});
```

## Search

```csharp
// All results (paginated)
IList<ElasticSearchGetModel<Product>> results = await elasticSearch.GetAllSearch<Product>(
    new SearchParameters { IndexName = "products", From = 0, Size = 10 });

// Field-based search
IList<ElasticSearchGetModel<Product>> results = await elasticSearch.GetSearchByField<Product>(
    new SearchByFieldParameters
    {
        IndexName = "products",
        FieldName = "name",
        Value = "laptop",
        From = 0, Size = 10
    });

// Simple query string search
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

## Models

```
ElasticSearchModel              ← IndexName, ElasticId
  └── ElasticSearchInsertUpdateModel  ← + Item (object)
  └── ElasticSearchInsertManyModel    ← + Items (object[])

ElasticSearchGetModel<T>        ← ElasticId + T Item (for results)

SearchParameters                ← IndexName, From, Size
  └── SearchByFieldParameters   ← + FieldName, Value
  └── SearchByQueryParameters   ← + QueryName, Fields[], Query
```
