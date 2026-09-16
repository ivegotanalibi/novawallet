namespace NovaWallet.Ledger.Domain.Entities;

/// <summary>
/// Tracks processed idempotency keys to prevent duplicate processing.
/// </summary>
public class IdempotencyRecord
{
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public string ResponseBody { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private IdempotencyRecord() { } // EF Core

    public IdempotencyRecord(string key, string requestHash, int statusCode, string responseBody)
    {
        IdempotencyKey = key;
        RequestHash = requestHash;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        CreatedAt = DateTime.UtcNow;
    }
}
