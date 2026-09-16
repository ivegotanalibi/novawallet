using NovaWallet.Ledger.Domain.Exceptions;

namespace NovaWallet.Ledger.Domain.Entities;

/// <summary>
/// Represents a customer's NovaWallet.
/// Balance is always stored in kobo (integer) to avoid floating-point drift.
/// </summary>
public class Wallet
{
    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public long BalanceKobo { get; private set; }
    public string Currency { get; private set; } = "NGN";
    public DateTime CreatedAt { get; private set; }

    // Navigation property
    public IReadOnlyCollection<LedgerTransaction> Transactions => _transactions;
    private readonly List<LedgerTransaction> _transactions = new();

    private Wallet() { } // EF Core

    public Wallet(string customerId)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        BalanceKobo = 0;
        Currency = "NGN";
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Credit funds to this wallet. Returns the new balance.
    /// </summary>
    public long Credit(long amountKobo)
    {
        if (amountKobo <= 0)
            throw new DomainException("Credit amount must be positive.");

        BalanceKobo += amountKobo;
        return BalanceKobo;
    }

    /// <summary>
    /// Debit funds from this wallet. Returns the new balance.
    /// Throws if insufficient funds.
    /// </summary>
    public long Debit(long amountKobo)
    {
        if (amountKobo <= 0)
            throw new DomainException("Debit amount must be positive.");

        if (BalanceKobo < amountKobo)
            throw new DomainException("Insufficient funds.");

        BalanceKobo -= amountKobo;
        return BalanceKobo;
    }
}
