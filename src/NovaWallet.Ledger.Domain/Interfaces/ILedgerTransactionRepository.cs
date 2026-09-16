using NovaWallet.Ledger.Domain.Entities;

namespace NovaWallet.Ledger.Domain.Interfaces;

public interface ILedgerTransactionRepository
{
    Task AddAsync(LedgerTransaction transaction, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<LedgerTransaction> transactions, CancellationToken ct = default);

    /// <summary>
    /// Paginated transaction history, newest first.
    /// </summary>
    Task<(IReadOnlyList<LedgerTransaction> Items, int TotalCount)> GetByWalletAsync(
        Guid walletId, int page, int pageSize, CancellationToken ct = default);
}
