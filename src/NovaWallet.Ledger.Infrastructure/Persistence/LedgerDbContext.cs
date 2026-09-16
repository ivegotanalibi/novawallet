using Microsoft.EntityFrameworkCore;
using NovaWallet.Ledger.Domain.Entities;

namespace NovaWallet.Ledger.Infrastructure.Persistence;

public class LedgerDbContext : DbContext
{
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Wallet ──────────────────────────────────────────────
        modelBuilder.Entity<Wallet>(e =>
        {
            e.ToTable("wallets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired().HasMaxLength(100);
            e.Property(x => x.BalanceKobo).HasColumnName("balance_kobo").IsRequired();
            e.Property(x => x.Currency).HasColumnName("currency").IsRequired().HasMaxLength(3);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            e.HasIndex(x => x.CustomerId).IsUnique();
            e.Ignore(x => x.Transactions);
        });

        // ── LedgerTransaction ───────────────────────────────────
        modelBuilder.Entity<LedgerTransaction>(e =>
        {
            e.ToTable("ledger_transactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.WalletId).HasColumnName("wallet_id").IsRequired();
            e.Property(x => x.Type).HasColumnName("type").IsRequired().HasConversion<string>();
            e.Property(x => x.AmountKobo).HasColumnName("amount_kobo").IsRequired();
            e.Property(x => x.BalanceAfterKobo).HasColumnName("balance_after_kobo").IsRequired();
            e.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(200);
            e.Property(x => x.LinkedTransactionId).HasColumnName("linked_transaction_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            e.HasIndex(x => x.WalletId);
            e.HasIndex(x => new { x.WalletId, x.CreatedAt });
        });

        // ── AuditEntry (append-only — no update/delete) ─────────
        modelBuilder.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.WalletId).HasColumnName("wallet_id").IsRequired();
            e.Property(x => x.Action).HasColumnName("action").IsRequired().HasMaxLength(50);
            e.Property(x => x.AmountKobo).HasColumnName("amount_kobo").IsRequired();
            e.Property(x => x.BalanceBeforeKobo).HasColumnName("balance_before_kobo").IsRequired();
            e.Property(x => x.BalanceAfterKobo).HasColumnName("balance_after_kobo").IsRequired();
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id").IsRequired().HasMaxLength(100);
            e.Property(x => x.Timestamp).HasColumnName("timestamp").IsRequired();

            e.HasIndex(x => x.WalletId);
            e.HasIndex(x => x.CorrelationId);
        });

        // ── IdempotencyRecord ───────────────────────────────────
        modelBuilder.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("idempotency_records");
            e.HasKey(x => x.IdempotencyKey);
            e.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(255);
            e.Property(x => x.RequestHash).HasColumnName("request_hash").IsRequired().HasMaxLength(64);
            e.Property(x => x.StatusCode).HasColumnName("status_code").IsRequired();
            e.Property(x => x.ResponseBody).HasColumnName("response_body").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        });
    }
}
