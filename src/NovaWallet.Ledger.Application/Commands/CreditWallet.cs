using MediatR;
using NovaWallet.Ledger.Application.DTOs;
using NovaWallet.Ledger.Domain.Entities;
using NovaWallet.Ledger.Domain.Exceptions;
using NovaWallet.Ledger.Domain.Interfaces;

namespace NovaWallet.Ledger.Application.Commands;

public record CreditWalletCommand(Guid WalletId, long AmountKobo, string? Reference) : IRequest<CreditResultDto>;

public class CreditWalletHandler : IRequestHandler<CreditWalletCommand, CreditResultDto>
{
    private readonly IWalletRepository _wallets;
    private readonly ILedgerTransactionRepository _transactions;
    private readonly IAuditRepository _audit;
    private readonly IUnitOfWork _unitOfWork;

    public CreditWalletHandler(
        IWalletRepository wallets,
        ILedgerTransactionRepository transactions,
        IAuditRepository audit,
        IUnitOfWork unitOfWork)
    {
        _wallets = wallets;
        _transactions = transactions;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreditResultDto> Handle(CreditWalletCommand request, CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var wallet = await _wallets.GetByIdAsync(request.WalletId, ct)
                ?? throw new WalletNotFoundException(request.WalletId);

            var balanceBefore = wallet.BalanceKobo;
            var newBalance = wallet.Credit(request.AmountKobo);

            var transaction = new LedgerTransaction(
                wallet.Id, TransactionType.Credit, request.AmountKobo, newBalance, request.Reference);
            await _transactions.AddAsync(transaction, ct);

            await _audit.AddAsync(new AuditEntry(
                wallet.Id, "Credit", request.AmountKobo, balanceBefore, newBalance,
                ct.GetHashCode().ToString()), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            return new CreditResultDto(transaction.Id, wallet.Id, request.AmountKobo, newBalance, transaction.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
