using MediatR;
using NovaWallet.Ledger.Application.DTOs;
using NovaWallet.Ledger.Domain.Exceptions;
using NovaWallet.Ledger.Domain.Interfaces;

namespace NovaWallet.Ledger.Application.Queries;

public record GetBalanceQuery(Guid WalletId) : IRequest<BalanceDto>;

public class GetBalanceHandler : IRequestHandler<GetBalanceQuery, BalanceDto>
{
    private readonly IWalletRepository _wallets;

    public GetBalanceHandler(IWalletRepository wallets)
    {
        _wallets = wallets;
    }

    public async Task<BalanceDto> Handle(GetBalanceQuery request, CancellationToken ct)
    {
        var wallet = await _wallets.GetByIdAsync(request.WalletId, ct)
            ?? throw new WalletNotFoundException(request.WalletId);

        return new BalanceDto(wallet.Id, wallet.BalanceKobo, wallet.Currency);
    }
}
