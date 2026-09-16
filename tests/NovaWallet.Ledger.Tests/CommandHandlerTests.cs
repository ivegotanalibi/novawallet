using Moq;
using NovaWallet.Ledger.Application.Commands;
using NovaWallet.Ledger.Domain.Entities;
using NovaWallet.Ledger.Domain.Exceptions;
using NovaWallet.Ledger.Domain.Interfaces;

namespace NovaWallet.Ledger.Tests;

/// <summary>
/// Unit tests for MediatR command handlers using mocked repositories.
/// </summary>
public class CommandHandlerTests
{
    // ── CreateWallet ──────────────────────────────────────────

    [Fact]
    public async Task CreateWallet_ShouldPersistAndReturnDto()
    {
        var mockWallets = new Mock<IWalletRepository>();
        var mockUow = new Mock<IUnitOfWork>();
        var handler = new CreateWalletHandler(mockWallets.Object, mockUow.Object);

        var result = await handler.Handle(new CreateWalletCommand("CUST-001"), CancellationToken.None);

        Assert.Equal("CUST-001", result.CustomerId);
        Assert.Equal(0, result.BalanceKobo);
        Assert.Equal("NGN", result.Currency);
        mockWallets.Verify(w => w.AddAsync(It.IsAny<Wallet>(), It.IsAny<CancellationToken>()), Times.Once);
        mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── CreditWallet ──────────────────────────────────────────

    [Fact]
    public async Task CreditWallet_ShouldUpdateBalanceAndRecordTransaction()
    {
        var wallet = new Wallet("CUST-001");
        var mockWallets = new Mock<IWalletRepository>();
        mockWallets.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        var mockTx = new Mock<ILedgerTransactionRepository>();
        var mockAudit = new Mock<IAuditRepository>();
        var mockUow = new Mock<IUnitOfWork>();

        var handler = new CreditWalletHandler(
            mockWallets.Object, mockTx.Object, mockAudit.Object, mockUow.Object);

        var result = await handler.Handle(
            new CreditWalletCommand(wallet.Id, 50_000, "NIP-REF-001"), CancellationToken.None);

        Assert.Equal(50_000, result.BalanceKobo);
        Assert.Equal(50_000, result.AmountKobo);
        mockTx.Verify(t => t.AddAsync(It.IsAny<LedgerTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        mockAudit.Verify(a => a.AddAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreditWallet_NonExistentWallet_ShouldThrow()
    {
        var mockWallets = new Mock<IWalletRepository>();
        mockWallets.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Wallet?)null);

        var mockTx = new Mock<ILedgerTransactionRepository>();
        var mockAudit = new Mock<IAuditRepository>();
        var mockUow = new Mock<IUnitOfWork>();

        var handler = new CreditWalletHandler(
            mockWallets.Object, mockTx.Object, mockAudit.Object, mockUow.Object);

        await Assert.ThrowsAsync<WalletNotFoundException>(() =>
            handler.Handle(new CreditWalletCommand(Guid.NewGuid(), 50_000, null), CancellationToken.None));
    }

    [Fact]
    public async Task CreditWallet_ShouldRecordAuditWithCorrectBeforeAfter()
    {
        var wallet = new Wallet("CUST-001");
        wallet.Credit(100_000); // Start with ₦1,000

        var mockWallets = new Mock<IWalletRepository>();
        mockWallets.Setup(w => w.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        var capturedAudit = new List<AuditEntry>();
        var mockAudit = new Mock<IAuditRepository>();
        mockAudit.Setup(a => a.AddAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => capturedAudit.Add(entry));

        var mockTx = new Mock<ILedgerTransactionRepository>();
        var mockUow = new Mock<IUnitOfWork>();

        var handler = new CreditWalletHandler(
            mockWallets.Object, mockTx.Object, mockAudit.Object, mockUow.Object);

        await handler.Handle(new CreditWalletCommand(wallet.Id, 25_000, "REF"), CancellationToken.None);

        Assert.Single(capturedAudit);
        Assert.Equal(100_000, capturedAudit[0].BalanceBeforeKobo);
        Assert.Equal(125_000, capturedAudit[0].BalanceAfterKobo);
        Assert.Equal("Credit", capturedAudit[0].Action);
    }
}
