using MediatR;
using NovaWallet.Ledger.Application.DTOs;
using NovaWallet.Ledger.Domain.Entities;
using NovaWallet.Ledger.Domain.Interfaces;

namespace NovaWallet.Ledger.Application.Commands;

public record CreateWalletCommand(string CustomerId) : IRequest<WalletDto>;

public class CreateWalletHandler : IRequestHandler<CreateWalletCommand, WalletDto>
{
    private readonly IWalletRepository _wallets;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWalletHandler(IWalletRepository wallets, IUnitOfWork unitOfWork)
    {
        _wallets = wallets;
        _unitOfWork = unitOfWork;
    }

    public async Task<WalletDto> Handle(CreateWalletCommand request, CancellationToken ct)
    {
        var wallet = new Wallet(request.CustomerId);
        await _wallets.AddAsync(wallet, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new WalletDto(wallet.Id, wallet.CustomerId, wallet.BalanceKobo, wallet.Currency, wallet.CreatedAt);
    }
}
