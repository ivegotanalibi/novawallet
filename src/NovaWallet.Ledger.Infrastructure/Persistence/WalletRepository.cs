using Microsoft.EntityFrameworkCore;
using NovaWallet.Ledger.Domain.Entities;
using NovaWallet.Ledger.Domain.Interfaces;

namespace NovaWallet.Ledger.Infrastructure.Persistence;

public class WalletRepository : IWalletRepository
{
    private readonly LedgerDbContext _db;
    private readonly bool _isSqlite;

    public WalletRepository(LedgerDbContext db)
    {
        _db = db;
        _isSqlite = db.Database.IsSqlite();
    }

    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Wallets.FirstOrDefaultAsync(w => w.Id == id, ct);

    /// <summary>
    /// Locks the wallet row inside the current transaction.
    /// MySQL: uses SELECT ... FOR UPDATE (InnoDB row-level lock).
    /// SQLite: writes are serialized natively, so a regular read suffices.
    /// </summary>
    public async Task<Wallet?> GetWithLockAsync(Guid id, CancellationToken ct = default)
    {
        if (_isSqlite)
            return await _db.Wallets.FirstOrDefaultAsync(w => w.Id == id, ct);

        return await _db.Wallets
            .FromSqlRaw("SELECT * FROM wallets WHERE id = {0} FOR UPDATE", id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<Wallet?> GetByCustomerIdAsync(string customerId, CancellationToken ct = default)
        => _db.Wallets.FirstOrDefaultAsync(w => w.CustomerId == customerId, ct);

    public async Task AddAsync(Wallet wallet, CancellationToken ct = default)
        => await _db.Wallets.AddAsync(wallet, ct);

    public Task<long> GetDailyDebitTotalAsync(Guid walletId, DateOnly date, CancellationToken ct = default)
    {
        // WAT = UTC+1, so convert date boundaries to UTC
        var startWat = date.ToDateTime(TimeOnly.MinValue);
        var startUtc = DateTime.SpecifyKind(startWat, DateTimeKind.Utc).AddHours(-1);
        var endUtc = startUtc.AddDays(1);

        return _db.Set<LedgerTransaction>()
            .Where(t => t.WalletId == walletId
                        && t.Type == TransactionType.Debit
                        && t.CreatedAt >= startUtc
                        && t.CreatedAt < endUtc)
            .SumAsync(t => (long?)t.AmountKobo, ct)
            .ContinueWith(t => t.Result ?? 0, ct);
    }
}
