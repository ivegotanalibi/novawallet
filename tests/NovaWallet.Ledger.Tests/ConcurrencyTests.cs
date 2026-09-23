using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaWallet.Ledger.Infrastructure.Persistence;

namespace NovaWallet.Ledger.Tests;

/// <summary>
/// Integration test that exercises the concurrency edge case under load.
/// Fires 20 concurrent transfer requests against the same wallet, each from a
/// DIFFERENT authenticated user (so the throttling layer is not involved —
/// this test isolates the locking mechanism), and verifies:
///   1. Exactly 10 succeed — the balance only covers 10. No double-spend.
///   2. The other 10 are rejected with 422 Insufficient Funds.
///   3. No request ends in an unexpected state (429/409/500).
///   4. Money is conserved: source + destination balances = original credit.
///
/// Requires MySQL on port 3307 — run 'docker compose up mysql -d' first.
/// </summary>
public class ConcurrencyTests : IClassFixture<ConcurrencyTests.TestFactory>
{
    private readonly TestFactory _factory;

    private const string ConnectionString =
        "Server=localhost;Port=3307;Database=novawallet_ledger_test;User=root;Password=root_secret;";

    public ConcurrencyTests(TestFactory factory) => _factory = factory;

    [Fact]
    public async Task ConcurrentTransfers_ShouldNeverAllowDoubleSpend()
    {
        // Unique per run — keeps idempotency keys and wallets fresh on every execution
        var runId = Guid.NewGuid().ToString("N")[..8];

        // ── Arrange ── two wallets; source funded with ₦10,000 (1,000,000 kobo)
        using var setupClient = _factory.CreateClient();
        var setupToken = await GetTokenAsync(setupClient, $"CUST-SETUP-{runId}");
        setupClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", setupToken);

        var walletA = await CreateWalletAsync(setupClient, $"CUST-SOURCE-{runId}");
        var walletB = await CreateWalletAsync(setupClient, $"CUST-RECEIVER-{runId}");

        var credit = await setupClient.PostAsJsonAsync($"/api/wallets/{walletA}/credit",
            new { amountKobo = 1_000_000, reference = $"FUND-{runId}" });
        credit.EnsureSuccessStatusCode();

        // ── Act ── 20 distinct users each fire one ₦1,000 transfer, all at once.
        // Total demand: ₦20,000 from a ₦10,000 wallet.
        const int transferAmount = 100_000; // ₦1,000 in kobo

        using var loadClient = _factory.CreateClient();
        var tokens = new List<string>();
        for (var i = 1; i <= 20; i++)
            tokens.Add(await GetTokenAsync(loadClient, $"CUST-USER-{runId}-{i}"));

        var requests = Enumerable.Range(1, 20).Select(i =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{walletA}/transfer")
            {
                Content = JsonContent.Create(new
                {
                    toWalletId = walletB,
                    amountKobo = transferAmount,
                    reference = $"CONCURRENT-{runId}-{i}"
                })
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens[i - 1]);
            req.Headers.Add("Idempotency-Key", $"idem-{runId}-{i}");
            return loadClient.SendAsync(req);
        }).ToList();

        var responses = await Task.WhenAll(requests);

        // ── Assert ── exactly 10 succeed, exactly 10 rejected, nothing unexpected
        var successes = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rejections = responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
        var unexpected = responses
            .Where(r => r.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity))
            .Select(r => (int)r.StatusCode)
            .ToList();

        Assert.True(unexpected.Count == 0,
            $"No request should end in an unexpected state, got: {string.Join(", ", unexpected)}");
        Assert.Equal(10, successes);   // balance covers exactly 10 — no more can possibly pass
        Assert.Equal(10, rejections);  // the rest must hit 'Insufficient funds' (422)

        // Money is conserved — no kobo created or destroyed by the race
        var balanceA = await GetBalanceAsync(setupClient, walletA);
        var balanceB = await GetBalanceAsync(setupClient, walletB);
        Assert.True(balanceA >= 0, $"Balance must never be negative! Got: {balanceA}");
        Assert.Equal(1_000_000, balanceA + balanceB);
    }

    private static async Task<string> GetTokenAsync(HttpClient client, string customerId)
    {
        var resp = await client.PostAsJsonAsync("/api/auth/token",
            new { customerId, customerName = "Test" });
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    private static async Task<Guid> CreateWalletAsync(HttpClient client, string customerId)
    {
        var resp = await client.PostAsJsonAsync("/api/wallets", new { customerId });
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(json.GetProperty("walletId").GetString()!);
    }

    private static async Task<long> GetBalanceAsync(HttpClient client, Guid walletId)
    {
        var json = await client.GetFromJsonAsync<JsonElement>($"/api/wallets/{walletId}/balance");
        return json.GetProperty("balanceKobo").GetInt64();
    }

    /// <summary>
    /// Custom WebApplicationFactory that overrides the DB connection string
    /// to use the isolated test database instead of the development one.
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

                var connectionString =
                    Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTION")
                    ?? ConnectionString;

                services.AddDbContext<LedgerDbContext>(options =>
                    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
            });
        }
    }
}
