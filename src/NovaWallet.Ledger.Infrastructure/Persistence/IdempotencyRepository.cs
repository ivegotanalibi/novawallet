using Microsoft.EntityFrameworkCore;
using NovaWallet.Ledger.Domain.Entities;
using NovaWallet.Ledger.Domain.Interfaces;

namespace NovaWallet.Ledger.Infrastructure.Persistence;

public class IdempotencyRepository : IIdempotencyRepository
{
    private readonly LedgerDbContext _db;

    public IdempotencyRepository(LedgerDbContext db) => _db = db;

    public Task<IdempotencyRecord?> GetByKeyAsync(string key, CancellationToken ct = default)
        => _db.IdempotencyRecords.FirstOrDefaultAsync(r => r.IdempotencyKey == key, ct);

    public async Task AddAsync(IdempotencyRecord record, CancellationToken ct = default)
        => await _db.IdempotencyRecords.AddAsync(record, ct);
}
