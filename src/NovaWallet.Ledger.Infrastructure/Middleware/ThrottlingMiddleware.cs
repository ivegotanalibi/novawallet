using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace NovaWallet.Ledger.Infrastructure.Middleware;

/// <summary>
/// Simple sliding-window request throttle on the transfer endpoint.
/// Allows max 10 requests per second per wallet.
/// </summary>
public class ThrottlingMiddleware
{
    private const int MaxRequestsPerWindow = 10;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    private static readonly ConcurrentDictionary<string, SlidingWindow> Windows = new();

    private readonly RequestDelegate _next;

    public ThrottlingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.Value?.Contains("/transfer") == true
            || context.Request.Method != "POST")
        {
            await _next(context);
            return;
        }

        // Throttle per authenticated user (sub claim) or IP
        var identifier = context.User.FindFirst("sub")?.Value
                         ?? context.Connection.RemoteIpAddress?.ToString()
                         ?? "anonymous";

        var window = Windows.GetOrAdd(identifier, _ => new SlidingWindow());

        if (!window.TryAcquire())
        {
            context.Response.StatusCode = 429;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(
                """{"type":"https://httpstatuses.com/429","title":"Too Many Requests","status":429,"detail":"Rate limit exceeded. Try again shortly."}""");
            return;
        }

        await _next(context);
    }

    private class SlidingWindow
    {
        private readonly Queue<DateTime> _timestamps = new();
        private readonly object _lock = new();

        public bool TryAcquire()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var cutoff = now - Window;

                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                    _timestamps.Dequeue();

                if (_timestamps.Count >= MaxRequestsPerWindow)
                    return false;

                _timestamps.Enqueue(now);
                return true;
            }
        }
    }
}
