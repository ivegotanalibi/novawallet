namespace NovaWallet.Ledger.Api.Contracts;

/// <summary>
/// Request DTOs for wallet API endpoints.
/// </summary>
public record CreateWalletRequest(string CustomerId);
public record CreditRequest(long AmountKobo, string? Reference);
public record TransferRequest(Guid ToWalletId, long AmountKobo, string? Reference);
