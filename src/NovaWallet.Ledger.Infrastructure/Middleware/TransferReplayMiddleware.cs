using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NovaWallet.Ledger.Domain.Entities;
using NovaWallet.Ledger.Domain.Interfaces;

namespace NovaWallet.Ledger.Infrastructure.Middleware;

/// <summary>
/// Middleware that prevents duplicate processing of transfer requests.
/// Uses an Idempotency-Key header to track processed requests:
/// - Same key + same payload → returns cached response (safe replay)
/// - Same key + different payload → returns 409 Conflict
/// - New key → processes normally and stores the result
/// </summary>
public class TransferReplayMiddleware
{
    private readonly RequestDelegate _next;

    public TransferReplayMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to the transfer endpoint
        if (!context.Request.Path.Value?.Contains("/transfer") == true
            || context.Request.Method != "POST")
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues))
        {
            await _next(context);
            return;
        }

        var key = keyValues.ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            await _next(context);
            return;
        }

        var idempotencyRepo = context.RequestServices.GetRequiredService<IIdempotencyRepository>();
        var unitOfWork = context.RequestServices.GetRequiredService<IUnitOfWork>();

        // Read and hash the request body
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        var requestHash = ComputeSha256Hash(body);

        var existing = await idempotencyRepo.GetByKeyAsync(key);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                // Same key, different payload → reject
                context.Response.StatusCode = 409;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    type = "https://httpstatuses.com/409",
                    title = "Idempotency Conflict",
                    status = 409,
                    detail = "This idempotency key was already used with a different request payload."
                }));
                return;
            }

            // Same key, same payload → replay cached response
            context.Response.StatusCode = existing.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(existing.ResponseBody);
            return;
        }

        // New key → capture the response
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);
        }
        catch
        {
            // Restore original body so the exception middleware can write the error
            context.Response.Body = originalBody;
            throw;
        }

        // Only cache successful responses (2xx)
        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            buffer.Position = 0;
            var responseBody = await new StreamReader(buffer).ReadToEndAsync();

            var record = new IdempotencyRecord(key, requestHash, context.Response.StatusCode, responseBody);
            await idempotencyRepo.AddAsync(record);
            await unitOfWork.SaveChangesAsync();

            // Copy response back to client
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
        }

        context.Response.Body = originalBody;
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
