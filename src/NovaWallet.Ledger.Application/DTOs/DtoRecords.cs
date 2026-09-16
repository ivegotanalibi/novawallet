namespace NovaWallet.Ledger.Application.DTOs;

public record WalletDto(
    Guid WalletId,
    string CustomerId,
    long BalanceKobo,
    string Currency,
    DateTime CreatedAt);

public record BalanceDto(
    Guid WalletId,
    long BalanceKobo,
    string Currency);

public record TransactionDto(
    Guid TransactionId,
    string Type,
    long AmountKobo,
    long BalanceAfterKobo,
    string? Reference,
    DateTime CreatedAt);

public record PaginatedStatementDto(
    Guid WalletId,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<TransactionDto> Transactions);

public record TransferResultDto(
    Guid TransactionId,
    Guid FromWalletId,
    Guid ToWalletId,
    long AmountKobo,
    long FromBalanceKobo,
    long ToBalanceKobo,
    DateTime CreatedAt);

public record CreditResultDto(
    Guid TransactionId,
    Guid WalletId,
    long AmountKobo,
    long BalanceKobo,
    DateTime CreatedAt);
