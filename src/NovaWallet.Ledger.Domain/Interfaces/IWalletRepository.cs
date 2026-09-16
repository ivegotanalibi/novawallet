using NovaWallet.Ledger.Domain.Entities;

namespace NovaWallet.Ledger.Domain.Interfaces;

/// <summary>
/// Repository for wallet aggregate. Handles concurrency-safe reads via pessimistic locking.
/// </summary>
public interface IWalletRepository
{
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Locks the wallet row (SELECT ... FOR UPDATE) inside a transaction.
    /// Use for transfers to prevent concurrent balance modification.
    /// </summary>
    Task<Wallet?> GetWithLockAsync(Guid id, CancellationToken ct = default);

    Task<Wallet?> GetByCustomerIdAsync(string customerId, CancellationToken ct = default);

    Task AddAsync(Wallet wallet, CancellationToken ct = default);

    /// <summary>
    /// Returns total outbound (debit) amount for a wallet on a given date (WAT).
    /// Used for daily limit enforcement.
    /// </summary>
    Task<long> GetDailyDebitTotalAsync(Guid walletId, DateOnly date, CancellationToken ct = default);
}
