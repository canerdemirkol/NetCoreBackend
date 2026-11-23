namespace NetCoreBackend.NArchitecture.Core.Payments.Simulator;

/// <summary>A card the simulated provider keeps for a merchant's customer; its ending decides how a later charge ends.</summary>
internal sealed record SimulatedCard(string MerchantId, string OwnerKey, string Token, string Last4);
