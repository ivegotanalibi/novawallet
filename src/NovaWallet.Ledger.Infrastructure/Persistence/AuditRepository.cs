using NovaWallet.Ledger.Domain.Entities;
using NovaWallet.Ledger.Domain.Interfaces;

namespace NovaWallet.Ledger.Infrastructure.Persistence;

public class AuditRepository : IAuditRepository
{
    private readonly LedgerDbContext _db;

    public AuditRepository(LedgerDbContext db) => _db = db;

    /// <summary>
    /// Append-only: inserts a new audit entry. No update or delete is exposed.
    /// </summary>
    public async Task AddAsync(AuditEntry entry, CancellationToken ct = default)
        => await _db.AuditEntries.AddAsync(entry, ct);
}
