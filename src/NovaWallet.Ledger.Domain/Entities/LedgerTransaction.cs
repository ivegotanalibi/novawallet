namespace NovaWallet.Ledger.Domain.Entities;

/// <summary>
/// Records every balance mutation on a wallet. Immutable after creation.
/// </summary>
public class LedgerTransaction
{
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public TransactionType Type { get; private set; }
    public long AmountKobo { get; private set; }
    public long BalanceAfterKobo { get; private set; }
    public string? Reference { get; private set; }
    public Guid? LinkedTransactionId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private LedgerTransaction() { } // EF Core

    public LedgerTransaction(
        Guid walletId,
        TransactionType type,
        long amountKobo,
        long balanceAfterKobo,
        string? reference = null,
        Guid? linkedTransactionId = null)
    {
        Id = Guid.NewGuid();
        WalletId = walletId;
        Type = type;
        AmountKobo = amountKobo;
        BalanceAfterKobo = balanceAfterKobo;
        Reference = reference;
        LinkedTransactionId = linkedTransactionId;
        CreatedAt = DateTime.UtcNow;
    }
}
