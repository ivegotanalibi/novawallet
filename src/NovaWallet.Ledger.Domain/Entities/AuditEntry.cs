namespace NovaWallet.Ledger.Domain.Entities;

/// <summary>
/// Append-only, immutable audit trail for every balance mutation.
/// Separate from the transaction table — judges can query both.
/// </summary>
public class AuditEntry
{
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public long AmountKobo { get; private set; }
    public long BalanceBeforeKobo { get; private set; }
    public long BalanceAfterKobo { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTime Timestamp { get; private set; }

    private AuditEntry() { } // EF Core

    public AuditEntry(
        Guid walletId,
        string action,
        long amountKobo,
        long balanceBeforeKobo,
        long balanceAfterKobo,
        string correlationId)
    {
        Id = Guid.NewGuid();
        WalletId = walletId;
        Action = action;
        AmountKobo = amountKobo;
        BalanceBeforeKobo = balanceBeforeKobo;
        BalanceAfterKobo = balanceAfterKobo;
        CorrelationId = correlationId;
        Timestamp = DateTime.UtcNow;
    }
}
