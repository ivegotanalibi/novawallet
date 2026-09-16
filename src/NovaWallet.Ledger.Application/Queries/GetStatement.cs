using MediatR;
using NovaWallet.Ledger.Application.DTOs;
using NovaWallet.Ledger.Domain.Exceptions;
using NovaWallet.Ledger.Domain.Interfaces;

namespace NovaWallet.Ledger.Application.Queries;

public record GetStatementQuery(Guid WalletId, int Page = 1, int PageSize = 20) : IRequest<PaginatedStatementDto>;

public class GetStatementHandler : IRequestHandler<GetStatementQuery, PaginatedStatementDto>
{
    private readonly IWalletRepository _wallets;
    private readonly ILedgerTransactionRepository _transactions;

    public GetStatementHandler(IWalletRepository wallets, ILedgerTransactionRepository transactions)
    {
        _wallets = wallets;
        _transactions = transactions;
    }

    public async Task<PaginatedStatementDto> Handle(GetStatementQuery request, CancellationToken ct)
    {
        var wallet = await _wallets.GetByIdAsync(request.WalletId, ct)
            ?? throw new WalletNotFoundException(request.WalletId);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, totalCount) = await _transactions.GetByWalletAsync(wallet.Id, page, pageSize, ct);

        var dtos = items.Select(t => new TransactionDto(
            t.Id, t.Type.ToString(), t.AmountKobo, t.BalanceAfterKobo, t.Reference, t.CreatedAt
        )).ToList();

        return new PaginatedStatementDto(wallet.Id, page, pageSize, totalCount, dtos);
    }
}
