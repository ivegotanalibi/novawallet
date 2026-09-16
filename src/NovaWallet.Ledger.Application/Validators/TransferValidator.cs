using FluentValidation;
using NovaWallet.Ledger.Application.Commands;

namespace NovaWallet.Ledger.Application.Validators;

public class TransferValidator : AbstractValidator<TransferCommand>
{
    public TransferValidator()
    {
        RuleFor(x => x.FromWalletId)
            .NotEmpty().WithMessage("FromWalletId is required.");

        RuleFor(x => x.ToWalletId)
            .NotEmpty().WithMessage("ToWalletId is required.")
            .NotEqual(x => x.FromWalletId).WithMessage("Source and destination wallets must differ.");

        RuleFor(x => x.AmountKobo)
            .GreaterThan(0).WithMessage("Amount must be a positive integer (kobo).");
    }
}
