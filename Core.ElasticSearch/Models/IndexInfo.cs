namespace NetCoreBackend.NArchitecture.Core.ElasticSearch.Models;

// Provider-neutral snapshot of an Elasticsearch index, returned from GetIndexList.
// Wrapped here so consumers don't need to take a dependency on the Elastic.Clients types
// just to read index metadata.
public sealed record IndexInfo(string Name, IReadOnlyCollection<string> Aliases);
