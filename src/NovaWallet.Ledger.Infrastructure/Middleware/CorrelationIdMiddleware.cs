using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NovaWallet.Ledger.Infrastructure.Middleware;

/// <summary>
/// Adds a Correlation-Id and Trace-Id to every request for structured logging.
/// - Correlation-Id: passed in by the caller (X-Correlation-Id header) or auto-generated.
/// - Trace-Id: ASP.NET Core's built-in Activity trace ID for distributed tracing.
/// Both values are added to the logger scope so every log entry includes them.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        // Use caller-provided correlation ID or generate one
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        // ASP.NET Core's built-in distributed trace ID
        var traceId = context.TraceIdentifier;

        // Store on context for downstream access
        context.Items["CorrelationId"] = correlationId;
        context.Items["TraceId"] = traceId;

        // Echo correlation ID back in the response
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Wrap the entire request in a logging scope
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = traceId,
            ["RequestId"] = context.TraceIdentifier,
            ["RequestPath"] = context.Request.Path.Value!,
            ["RequestMethod"] = context.Request.Method
        }))
        {
            await _next(context);
        }
    }
}
