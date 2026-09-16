using NovaWallet.Ledger.Domain.Entities;
using NovaWallet.Ledger.Domain.Exceptions;

namespace NovaWallet.Ledger.Tests;

/// <summary>
/// Unit tests for the Wallet domain entity — verifies business rules
/// around credit, debit, and balance integrity.
/// </summary>
public class WalletDomainTests
{
    [Fact]
    public void NewWallet_ShouldHaveZeroBalance()
    {
        var wallet = new Wallet("CUST-001");

        Assert.Equal(0, wallet.BalanceKobo);
        Assert.Equal("NGN", wallet.Currency);
        Assert.Equal("CUST-001", wallet.CustomerId);
    }

    [Fact]
    public void Credit_ShouldIncreaseBalance()
    {
        var wallet = new Wallet("CUST-001");

        var newBalance = wallet.Credit(50_000); // ₦500

        Assert.Equal(50_000, newBalance);
        Assert.Equal(50_000, wallet.BalanceKobo);
    }

    [Fact]
    public void Debit_ShouldDecreaseBalance()
    {
        var wallet = new Wallet("CUST-001");
        wallet.Credit(100_000); // ₦1,000

        var newBalance = wallet.Debit(30_000); // ₦300

        Assert.Equal(70_000, newBalance);
        Assert.Equal(70_000, wallet.BalanceKobo);
    }

    [Fact]
    public void Debit_WithInsufficientFunds_ShouldThrow()
    {
        var wallet = new Wallet("CUST-001");
        wallet.Credit(10_000); // ₦100

        var ex = Assert.Throws<DomainException>(() => wallet.Debit(20_000));
        Assert.Contains("Insufficient funds", ex.Message);
        Assert.Equal(10_000, wallet.BalanceKobo); // Balance unchanged
    }

    [Fact]
    public void Credit_WithNegativeAmount_ShouldThrow()
    {
        var wallet = new Wallet("CUST-001");

        Assert.Throws<DomainException>(() => wallet.Credit(-100));
    }

    [Fact]
    public void Debit_WithNegativeAmount_ShouldThrow()
    {
        var wallet = new Wallet("CUST-001");
        wallet.Credit(100_000);

        Assert.Throws<DomainException>(() => wallet.Debit(-100));
    }

    [Fact]
    public void Balance_ShouldNeverGoNegative()
    {
        var wallet = new Wallet("CUST-001");

        // Debit on zero balance must fail
        Assert.Throws<DomainException>(() => wallet.Debit(1));

        // Exact balance debit is fine
        wallet.Credit(500);
        wallet.Debit(500);
        Assert.Equal(0, wallet.BalanceKobo);

        // One kobo over should fail
        Assert.Throws<DomainException>(() => wallet.Debit(1));
    }

    [Fact]
    public void AmountsInKobo_AreAlwaysIntegers()
    {
        var wallet = new Wallet("CUST-001");

        // All operations use long (integer) — no floating point
        wallet.Credit(1);     // 1 kobo = ₦0.01
        wallet.Credit(99);    // 99 kobo
        Assert.Equal(100, wallet.BalanceKobo); // ₦1.00 exactly
    }
}
