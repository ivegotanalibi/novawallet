using MediatR;
using NovaWallet.Ledger.Application.DTOs;
using NovaWallet.Ledger.Domain.Entities;
using NovaWallet.Ledger.Domain.Exceptions;
using NovaWallet.Ledger.Domain.Interfaces;

namespace NovaWallet.Ledger.Application.Commands;

public record TransferCommand(
    Guid FromWalletId,
    Guid ToWalletId,
    long AmountKobo,
    string? Reference) : IRequest<TransferResultDto>;

/// <summary>
/// Handles atomic wallet-to-wallet transfers with pessimistic locking.
/// Uses SELECT ... FOR UPDATE to prevent concurrent balance modification.
/// </summary>
public class TransferHandler : IRequestHandler<TransferCommand, TransferResultDto>
{
    private const long DailyLimitKobo = 500_000_00; // ₦500,000 in kobo

    private readonly IWalletRepository _wallets;
    private readonly ILedgerTransactionRepository _transactions;
    private readonly IAuditRepository _audit;
    private readonly IUnitOfWork _unitOfWork;

    public TransferHandler(
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

    public async Task<TransferResultDto> Handle(TransferCommand request, CancellationToken ct)
    {
        if (request.FromWalletId == request.ToWalletId)
            throw new DomainException("Cannot transfer to the same wallet.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Lock both wallets — always lock in Guid order to prevent deadlocks
            var (first, second) = request.FromWalletId.CompareTo(request.ToWalletId) < 0
                ? (request.FromWalletId, request.ToWalletId)
                : (request.ToWalletId, request.FromWalletId);

            var lockedFirst = await _wallets.GetWithLockAsync(first, ct)
                ?? throw new WalletNotFoundException(first);
            var lockedSecond = await _wallets.GetWithLockAsync(second, ct)
                ?? throw new WalletNotFoundException(second);

            var fromWallet = lockedFirst.Id == request.FromWalletId ? lockedFirst : lockedSecond;
            var toWallet = lockedFirst.Id == request.ToWalletId ? lockedFirst : lockedSecond;

            // Enforce daily outbound limit (WAT = UTC+1)
            var todayWat = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(1));
            var dailyDebitSoFar = await _wallets.GetDailyDebitTotalAsync(fromWallet.Id, todayWat, ct);

            if (dailyDebitSoFar + request.AmountKobo > DailyLimitKobo)
                throw new DomainException($"Daily transfer limit of ₦500,000 exceeded. Used today: ₦{dailyDebitSoFar / 100.0:F2}");

            // Debit source, credit destination
            var fromBalanceBefore = fromWallet.BalanceKobo;
            var toBalanceBefore = toWallet.BalanceKobo;

            var fromNewBalance = fromWallet.Debit(request.AmountKobo);
            var toNewBalance = toWallet.Credit(request.AmountKobo);

            // Create linked transactions
            var debitTx = new LedgerTransaction(
                fromWallet.Id, TransactionType.Debit, request.AmountKobo, fromNewBalance, request.Reference);

            var creditTx = new LedgerTransaction(
                toWallet.Id, TransactionType.Credit, request.AmountKobo, toNewBalance,
                request.Reference, debitTx.Id);

            await _transactions.AddRangeAsync(new[] { debitTx, creditTx }, ct);

            // Audit entries for both sides
            var correlationId = debitTx.Id.ToString();
            await _audit.AddAsync(new AuditEntry(
                fromWallet.Id, "Transfer.Debit", request.AmountKobo,
                fromBalanceBefore, fromNewBalance, correlationId), ct);
            await _audit.AddAsync(new AuditEntry(
                toWallet.Id, "Transfer.Credit", request.AmountKobo,
                toBalanceBefore, toNewBalance, correlationId), ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            return new TransferResultDto(
                debitTx.Id, fromWallet.Id, toWallet.Id,
                request.AmountKobo, fromNewBalance, toNewBalance, debitTx.CreatedAt);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
