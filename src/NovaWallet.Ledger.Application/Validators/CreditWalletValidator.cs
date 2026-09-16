using FluentValidation;
using NovaWallet.Ledger.Application.Commands;

namespace NovaWallet.Ledger.Application.Validators;

public class CreditWalletValidator : AbstractValidator<CreditWalletCommand>
{
    public CreditWalletValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty().WithMessage("WalletId is required.");

        RuleFor(x => x.AmountKobo)
            .GreaterThan(0).WithMessage("Amount must be a positive integer (kobo).");
    }
}
