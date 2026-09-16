using NovaWallet.Ledger.Domain.Entities;

namespace NovaWallet.Ledger.Domain.Interfaces;

/// <summary>
/// Append-only audit log. Entries cannot be modified or deleted.
/// </summary>
public interface IAuditRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken ct = default);
}
