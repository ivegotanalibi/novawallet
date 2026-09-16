using Microsoft.EntityFrameworkCore;
using NovaWallet.Ledger.Domain.Entities;
using NovaWallet.Ledger.Domain.Interfaces;

namespace NovaWallet.Ledger.Infrastructure.Persistence;

public class LedgerTransactionRepository : ILedgerTransactionRepository
{
    private readonly LedgerDbContext _db;

    public LedgerTransactionRepository(LedgerDbContext db) => _db = db;

    public async Task AddAsync(LedgerTransaction transaction, CancellationToken ct = default)
        => await _db.LedgerTransactions.AddAsync(transaction, ct);

    public async Task AddRangeAsync(IEnumerable<LedgerTransaction> transactions, CancellationToken ct = default)
        => await _db.LedgerTransactions.AddRangeAsync(transactions, ct);

    public async Task<(IReadOnlyList<LedgerTransaction> Items, int TotalCount)> GetByWalletAsync(
        Guid walletId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.LedgerTransactions
            .Where(t => t.WalletId == walletId)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
