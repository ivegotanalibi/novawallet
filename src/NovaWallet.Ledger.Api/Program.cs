using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NovaWallet.Ledger.Application;
using NovaWallet.Ledger.Infrastructure;
using NovaWallet.Ledger.Infrastructure.Middleware;
using NovaWallet.Ledger.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ── Structured logging ───────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
    options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
});

// ── Application & Infrastructure layers ──────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── JWT Authentication (simplified mock issuer) ──────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

// ── Swagger / OpenAPI ────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NovaWallet Ledger Service",
        Version = "v1",
        Description = "Simplified wallet ledger for the NovaWallet module — FirstBank NovaPay"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Health checks ─────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LedgerDbContext>();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────
app.UseMiddleware<CorrelationIdMiddleware>();  // first — wraps entire request in logging scope
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<ThrottlingMiddleware>();
app.UseMiddleware<TransferReplayMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NovaWallet Ledger v1"));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// ── Ensure database is created on startup ─────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
    db.Database.EnsureCreated();
}

app.Run();

// Make the implicit Program class visible to integration tests
public partial class Program { }
