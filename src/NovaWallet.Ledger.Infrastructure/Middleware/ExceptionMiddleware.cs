using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NovaWallet.Ledger.Domain.Exceptions;

namespace NovaWallet.Ledger.Infrastructure.Middleware;

/// <summary>
/// Global exception handler — converts exceptions to RFC 7807 Problem Details JSON.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException vex => (
                HttpStatusCode.BadRequest,
                "Validation Error",
                string.Join("; ", vex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))),

            WalletNotFoundException => (
                HttpStatusCode.NotFound,
                "Resource Not Found",
                exception.Message),

            DomainException => (
                HttpStatusCode.UnprocessableEntity,
                "Business Rule Violation",
                exception.Message),

            DbUpdateConcurrencyException => (
                HttpStatusCode.Conflict,
                "Concurrency Conflict",
                "The resource was modified by another request. Please retry."),

            _ => (
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred.")
        };

        if (status == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception");
        else
            _logger.LogWarning(exception, "{Title}: {Detail}", title, detail);

        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{(int)status}",
            Title = title,
            Status = (int)status,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        await context.Response.WriteAsync(json);
    }
}
