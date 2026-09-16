# AI Usage Disclosure

## Tools Used

| Tool | Purpose |
|------|---------|
| **Qoder (AI Coding Assistant)** | Project scaffolding, code generation, architecture guidance, documentation |
| **GitHub Copilot** | Inline code completions while editing handlers and middleware |

## Concrete Prompts & Outcomes

### Prompt 1: Project Architecture
> "Help me design a clean architecture wallet ledger service in C#/.NET with CQRS, replay protection, and concurrency-safe transfers for a Nigerian fintech context."

**Outcome**: Generated a 4-layer clean architecture (Domain → Application → Infrastructure → API) with MediatR CQRS pattern, FluentValidation pipeline, and repository pattern with unit of work. The structure was solid and I adopted it with minor modifications to match the task requirements.

### Prompt 2: Concurrency-Safe Transfer
> "Write a transfer handler that atomically moves funds between two wallets, preventing negative balances and double-spends under concurrent load. Use MySQL pessimistic locking."

**Outcome**: Generated a handler using `SELECT ... FOR UPDATE` with dead-lock prevention (locking in Guid order). The approach was correct. I added the daily limit check and dual audit entries on top.

### Prompt 3: Transfer Replay Middleware
> "Create ASP.NET Core middleware that prevents duplicate processing of transfer requests using an Idempotency-Key header. Same key + same payload replays; same key + different payload rejects with 409."

**Outcome**: Generated working middleware with SHA-256 body hashing and response buffering. I adjusted the path matching to be more precise and ensured the response body was correctly copied back to the client.

## Where AI Was Wrong, Unsafe, or Naive

### Critical Issue: AI Initially Used `double` for Money Amounts

When asked to generate the Wallet entity, the AI's first draft used `double` for the balance field:

```csharp
// ❌ AI's initial output — UNSAFE for financial systems
public double Balance { get; set; }

public double Credit(double amount)
{
    Balance += amount; // Floating-point drift: 0.1 + 0.2 ≠ 0.3
    return Balance;
}
```

**Why this is dangerous**: IEEE 754 floating-point arithmetic introduces rounding errors. In a financial ledger, even a 1-kobo drift per transaction compounds over millions of transactions into material discrepancies. Regulatory audits (CBN) require exact integer accounting.

**How I caught it**: The task brief explicitly states: *"All monetary amounts are stored and computed as integers in kobo — no float/double anywhere in the money path."* I immediately rejected the `double` approach and replaced it with `long` (Int64) throughout the entire codebase — entity properties, method parameters, DTOs, and database columns.

**Fix applied**:
```csharp
// ✅ Correct — integer kobo, zero drift
public long BalanceKobo { get; private set; }

public long Credit(long amountKobo)
{
    if (amountKobo <= 0)
        throw new DomainException("Credit amount must be positive.");
    BalanceKobo += amountKobo;
    return BalanceKobo;
}
```

### Secondary Issue: AI Missed Deadlock Prevention in Transfer Locking

The AI's initial transfer handler locked wallets in request order (source first, then destination). Under concurrent bidirectional transfers (A→B and B→A), this creates a classic deadlock:

```
Thread 1: Locks A, waits for B
Thread 2: Locks B, waits for A
→ Deadlock
```

**Fix**: I added deterministic lock ordering — always lock the wallet with the smaller Guid first, regardless of transfer direction. This eliminates the deadlock possibility.

### Third Issue: AI Did Not Account for WAT Timezone in Daily Limit

The AI computed the daily limit window using UTC midnight. The task specifies *"reset at midnight WAT"* (West Africa Time = UTC+1). Using UTC would shift the daily reset to 1:00 AM WAT, allowing users to exploit the gap.

**Fix**: Converted the daily date boundary to WAT explicitly:
```csharp
var todayWat = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(1));
```

## Lessons Learned

1. **AI is excellent for scaffolding and boilerplate**, but domain-critical constraints (money precision, concurrency safety, timezone handling) require human judgment and careful review.
2. **Always verify financial calculations use integers** — never trust AI defaults that may use floating-point types.
3. **Concurrency edge cases need explicit specification** — AI tends to produce happy-path code unless you specifically ask about deadlocks, race conditions, and interleaving scenarios.
