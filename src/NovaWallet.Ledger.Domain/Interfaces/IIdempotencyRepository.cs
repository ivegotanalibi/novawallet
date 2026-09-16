using NovaWallet.Ledger.Domain.Entities;

namespace NovaWallet.Ledger.Domain.Interfaces;

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task AddAsync(IdempotencyRecord record, CancellationToken ct = default);
}
