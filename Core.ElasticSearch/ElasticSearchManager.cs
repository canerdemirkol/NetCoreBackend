using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport.Products.Elasticsearch;
using NetCoreBackend.NArchitecture.Core.ElasticSearch.Constants;
using NetCoreBackend.NArchitecture.Core.ElasticSearch.Models;

namespace NetCoreBackend.NArchitecture.Core.ElasticSearch;

// Elastic.Clients.Elasticsearch 8.x (System.Text.Json native) implementation.
//
// Migration from NEST 7.x:
//   - ElasticClient → ElasticsearchClient
//   - ConnectionSettings → ElasticsearchClientSettings
//   - ISearchResponse<T> → SearchResponse<T>; response.Hits.Hits is the list of HitMetadata<T>
//   - response.IsValid → response.IsValidResponse (8.x naming)
//   - Errors: response.ElasticsearchServerError?.Error?.Reason / Type replaces NEST's ServerError
//   - Serializer: System.Text.Json is the default; no Newtonsoft glue layer needed.
public class ElasticSearchManager : IElasticSearch
{
    private readonly ElasticsearchClient _client;

    public ElasticSearchManager(ElasticSearchConfig configuration)
    {
        ElasticsearchClientSettings settings = new(new Uri(configuration.ConnectionString));
        _client = new ElasticsearchClient(settings);
    }

    public async Task<IReadOnlyCollection<IndexInfo>> GetIndexListAsync(CancellationToken cancellationToken = default)
    {
        GetIndexResponse response = await _client.Indices.GetAsync(new GetIndexRequest(Indices.All), cancellationToken).ConfigureAwait(false);
        return response.Indices
            .Select(kvp => new IndexInfo(
                Name: kvp.Key.ToString(),
                Aliases: kvp.Value.Aliases?.Select(a => a.Key.ToString()).ToArray() ?? Array.Empty<string>()))
            .ToArray();
    }

    public async Task<IElasticSearchResult> InsertManyAsync(string indexName, object[] items, CancellationToken cancellationToken = default)
    {
        // Sending an empty Bulk request still incurs a round trip; short-circuit so callers
        // that build the array conditionally don't have to add the same guard.
        if (items is null || items.Length == 0)
            return new ElasticSearchResult(success: true, message: ElasticSearchMessages.Success);

        GuardIndexName(indexName);
        BulkResponse response = await _client.BulkAsync(b => b.Index(indexName).IndexMany(items), cancellationToken).ConfigureAwait(false);
        return ToResult(response);
    }

    public async Task<IElasticSearchResult> CreateNewIndexAsync(IndexModel indexModel, CancellationToken cancellationToken = default)
    {
        GuardIndexName(indexModel.IndexName);

        // Exists-then-create is racy when two callers hit this concurrently: both observe
        // "not exists" and both attempt create, leaving one with an Elasticsearch-side
        // resource_already_exists_exception. Skip the pre-check and inspect the response —
        // an "already exists" error from a concurrent create satisfies the desired end state.
        CreateIndexResponse response = await _client.Indices.CreateAsync(
            indexModel.IndexName,
            c => c
                .Settings(s => s
                    .NumberOfReplicas(indexModel.NumberOfReplicas)
                    .NumberOfShards(indexModel.NumberOfShards))
                .Aliases(a => a.Add(indexModel.AliasName, _ => { })),
            cancellationToken).ConfigureAwait(false);

        if (response.IsValidResponse)
            return new ElasticSearchResult(success: true, message: ElasticSearchMessages.Success);

        bool alreadyExists = response.ElasticsearchServerError?.Error?.Type == "resource_already_exists_exception";
        if (alreadyExists)
            return new ElasticSearchResult(success: false, message: ElasticSearchMessages.IndexAlreadyExists);

        return new ElasticSearchResult(success: false, message: response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation);
    }

    public async Task<IElasticSearchResult> DeleteByElasticIdAsync(ElasticSearchModel model, CancellationToken cancellationToken = default)
    {
        GuardIndexName(model.IndexName);
        DeleteResponse response = await _client.DeleteAsync(model.IndexName, model.ElasticId, cancellationToken).ConfigureAwait(false);
        return ToResult(response);
    }

    public async Task<List<ElasticSearchGetModel<T>>> GetAllSearch<T>(SearchParameters parameters, CancellationToken cancellationToken = default)
        where T : class
    {
        GuardIndexName(parameters.IndexName);
        SearchResponse<T> response = await _client.SearchAsync<T>(s => s
            .Indices(parameters.IndexName)
            .From(parameters.From)
            .Size(parameters.Size)
            .Query(q => q.MatchAll(_ => { })),
            cancellationToken).ConfigureAwait(false);

        return ProjectHits(response);
    }

    public async Task<List<ElasticSearchGetModel<T>>> GetSearchByField<T>(SearchByFieldParameters fieldParameters, CancellationToken cancellationToken = default)
        where T : class
    {
        GuardIndexName(fieldParameters.IndexName);
        SearchResponse<T> response = await _client.SearchAsync<T>(s => s
            .Indices(fieldParameters.IndexName)
            .From(fieldParameters.From)
            .Size(fieldParameters.Size)
            .Query(q => q.Term(t => t.Field(fieldParameters.FieldName).Value(fieldParameters.Value))),
            cancellationToken).ConfigureAwait(false);

        return ProjectHits(response);
    }

    public async Task<List<ElasticSearchGetModel<T>>> GetSearchBySimpleQueryString<T>(SearchByQueryParameters queryParameters, CancellationToken cancellationToken = default)
        where T : class
    {
        const string analyzer = "standard";
        const string minimumShouldMatch = "30%";
        GuardIndexName(queryParameters.IndexName);

        SearchResponse<T> response = await _client.SearchAsync<T>(s => s
            .Indices(queryParameters.IndexName)
            .From(queryParameters.From)
            .Size(queryParameters.Size)
            .Query(q => q.SimpleQueryString(sqs => sqs
                .QueryName(queryParameters.QueryName)
                .Boost(1.1f)
                .Fields(Fields.FromStrings(queryParameters.Fields))
                .Query(queryParameters.Query)
                .Analyzer(analyzer)
                .DefaultOperator(Operator.Or)
                .Flags(SimpleQueryStringFlags.And | SimpleQueryStringFlags.Near)
                .Lenient(true)
                .AnalyzeWildcard(false)
                .MinimumShouldMatch(minimumShouldMatch)
                .FuzzyPrefixLength(0)
                .FuzzyMaxExpansions(50)
                .FuzzyTranspositions(true)
                .AutoGenerateSynonymsPhraseQuery(false))),
            cancellationToken).ConfigureAwait(false);

        return ProjectHits(response);
    }

    public async Task<IElasticSearchResult> InsertAsync(ElasticSearchInsertUpdateModel model, CancellationToken cancellationToken = default)
    {
        GuardIndexName(model.IndexName);
        IndexResponse response = await _client.IndexAsync(
            model.Item,
            i => i.Index(model.IndexName).Id(model.ElasticId).Refresh(Refresh.True),
            cancellationToken).ConfigureAwait(false);
        return ToResult(response);
    }

    public async Task<IElasticSearchResult> UpdateByElasticIdAsync(ElasticSearchInsertUpdateModel model, CancellationToken cancellationToken = default)
    {
        GuardIndexName(model.IndexName);
        // 8.x update API: doc is supplied as the partial object via a typed builder.
        UpdateResponse<object> response = await _client.UpdateAsync<object, object>(
            model.IndexName,
            model.ElasticId,
            u => u.Doc(model.Item),
            cancellationToken).ConfigureAwait(false);
        return ToResult(response);
    }

    private static List<ElasticSearchGetModel<T>> ProjectHits<T>(SearchResponse<T> response) where T : class =>
        response.Hits
            .Where(h => h.Source is not null)
            .Select(h => new ElasticSearchGetModel<T> { ElasticId = h.Id!, Item = h.Source! })
            .ToList();

    private static IElasticSearchResult ToResult(ElasticsearchResponse response) =>
        new ElasticSearchResult(
            response.IsValidResponse,
            message: response.IsValidResponse
                ? ElasticSearchMessages.Success
                : response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation);

    private static void GuardIndexName(string indexName)
    {
        // Previous code passed `indexName` (the VALUE) as paramName, throwing
        // ArgumentNullException with a useless empty string as the parameter name.
        // Also: empty string is not technically "null" so ArgumentException is the correct type.
        if (string.IsNullOrWhiteSpace(indexName))
            throw new ArgumentException(ElasticSearchMessages.IndexNameCannotBeNullOrEmpty, nameof(indexName));
    }
}
