using FluentValidation;
using NovaWallet.Ledger.Application.Commands;

namespace NovaWallet.Ledger.Application.Validators;

public class CreateWalletValidator : AbstractValidator<CreateWalletCommand>
{
    public CreateWalletValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");
    }
}
