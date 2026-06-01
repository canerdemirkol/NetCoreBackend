namespace NetCoreBackend.NArchitecture.Core.Application.Pipelines.Logging;

// Marker interface for requests whose payload must NOT be serialized into the log.
// Apply to commands carrying secrets (passwords, tokens, PII, payment data) so
// LoggingBehavior records only the request type name instead of the full body.
public interface ISensitiveRequest { }
