using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaWallet.Ledger.Infrastructure.Persistence;

namespace NovaWallet.Ledger.Tests;

/// <summary>
/// Integration test that exercises the concurrency edge case under load.
/// Fires 20 concurrent transfer requests against the same wallet and verifies
/// the balance never goes negative and no double-spend occurs.
///
/// Requires MySQL — run 'docker compose up' first, then remove this Skip attribute.
/// </summary>
public class ConcurrencyTests : IClassFixture<ConcurrencyTests.TestFactory>
{
    private readonly TestFactory _factory;
    private const string ConnectionString =
        "Server=localhost;Port=3306;Database=novawallet_ledger_test;User=novawallet;Password=novawallet_secret;";

    public ConcurrencyTests(TestFactory factory) => _factory = factory;

    [Fact(Skip = "Requires MySQL. Run 'docker compose up' first, then remove this Skip attribute.")]
    public async Task ConcurrentTransfers_ShouldNeverAllowNegativeBalance()
    {
        // Arrange — create two wallets and credit the source
        using var client = _factory.CreateClient();
        var token = await GetTokenAsync(client, "CUST-CONCURRENCY");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var walletA = await CreateWalletAsync(client, "CUST-CONCURRENCY");
        var walletB = await CreateWalletAsync(client, "CUST-RECEIVER");

        // Credit wallet A with ₦10,000 (1,000,000 kobo)
        await client.PostAsJsonAsync($"/api/wallets/{walletA}/credit",
            new { amountKobo = 1_000_000, reference = "FUND" });

        // Act — fire 20 concurrent transfers of ₦1,000 each (total would be ₦20,000 if not protected)
        var transferAmount = 100_000; // ₦1,000 in kobo
        var tasks = Enumerable.Range(1, 20).Select(i =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{walletA}/transfer")
            {
                Content = JsonContent.Create(new
                {
                    toWalletId = walletB,
                    amountKobo = transferAmount,
                    reference = $"CONCURRENT-{i}"
                })
            };
            req.Headers.Add("Idempotency-Key", $"idem-key-{i}");
            return client.SendAsync(req);
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert — count successes vs rejections
        var successes = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejections = responses.Count(r =>
            r.StatusCode == HttpStatusCode.UnprocessableEntity ||
            r.StatusCode == HttpStatusCode.Conflict);

        // With ₦10,000 balance and ₦1,000 per transfer, max 10 should succeed
        Assert.True(successes <= 10, $"Expected ≤10 successful transfers, got {successes}");
        Assert.True(successes + rejections == 20, "All requests should either succeed or be rejected");

        // Verify balance is never negative
        var balanceResp = await client.GetAsync($"/api/wallets/{walletA}/balance");
        var balance = await balanceResp.Content.ReadFromJsonAsync<BalanceResponse>();
        Assert.NotNull(balance);
        Assert.True(balance!.BalanceKobo >= 0, $"Balance must never be negative! Got: {balance.BalanceKobo}");

        // Verify: balance + transferred amount = original amount
        var balanceB = await (await client.GetAsync($"/api/wallets/{walletB}/balance"))
            .Content.ReadFromJsonAsync<BalanceResponse>();
        Assert.Equal(1_000_000, balance.BalanceKobo + balanceB!.BalanceKobo);
    }

    private static async Task<string> GetTokenAsync(HttpClient client, string customerId)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/token",
            new { customerId, customerName = "Test" });
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    private static async Task<Guid> CreateWalletAsync(HttpClient client, string customerId)
    {
        var resp = await client.PostAsJsonAsync("/api/wallets",
            new { customerId });
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(json.GetProperty("walletId").GetString()!);
    }

    private record BalanceResponse(Guid WalletId, long BalanceKobo, string Currency);

    /// <summary>
    /// Custom WebApplicationFactory that overrides the DB connection string.
    /// </summary>
    public class TestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // Remove existing DB context registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<LedgerDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // Use test database (MySQL)
                services.AddDbContext<LedgerDbContext>(options =>
                    options.UseMySql(
                        Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTION")
                        ?? ConnectionString,
                        ServerVersion.AutoDetect(
                            Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTION")
                            ?? ConnectionString)));
            });
        }
    }
}
