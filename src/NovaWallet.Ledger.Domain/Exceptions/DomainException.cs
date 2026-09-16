namespace NovaWallet.Ledger.Domain.Exceptions;

/// <summary>
/// Domain-level business rule violation.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
