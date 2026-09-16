# NovaWallet Ledger Service

A simplified wallet ledger backend for the **NovaWallet** module of FirstBank NovaPay, built with .NET 9 and MySQL.

## Architecture

```
┌───────────────────────────────────────────────────┐
│                   API Layer                        │
│  Controllers · JWT Auth · Swagger · Middleware     │
├───────────────────────────────────────────────────┤
│               Application Layer                    │
│  MediatR CQRS · FluentValidation · Behaviors       │
├───────────────────────────────────────────────────┤
│                 Domain Layer                        │
│  Entities · Interfaces · Exceptions · Business Rules│
├───────────────────────────────────────────────────┤
│             Infrastructure Layer                    │
│  EF Core · MySQL · Repositories · Middleware       │
└───────────────────────────────────────────────────┘
```

**Clean Architecture** with four projects following dependency inversion:

| Layer | Responsibility |
|-------|---------------|
| **Domain** | Pure entities, business rules, repository interfaces — zero dependencies |
| **Application** | CQRS commands/queries via MediatR, validators via FluentValidation |
| **Infrastructure** | EF Core DbContext, MySQL, repository implementations, middleware |
| **Api** | ASP.NET Core controllers, JWT auth, Swagger, startup configuration |

### CQRS Structure
Each command/query lives in a single file alongside its handler — the standard MediatR pattern. For example, `Commands/Transfer.cs` contains both `TransferCommand` (the request record) and `TransferHandler` (the handler class). This keeps related code together and easy to navigate.

## Key Design Decisions

### Concurrency Safety (Pessimistic Locking)
Transfers use MySQL's `SELECT ... FOR UPDATE` (InnoDB row-level locking) inside a database transaction. This prevents two concurrent transfers from reading the same balance and both proceeding — eliminating double-spend and negative balance scenarios.

**Deadlock prevention**: When locking two wallets in a transfer, we always lock in ascending Guid order regardless of which is source/destination.

### Monetary Precision
All amounts are stored as **integers in kobo** (`long` in C#, `bigint` in MySQL). No `float` or `double` is used anywhere in the money path. This eliminates floating-point drift entirely.

### Transfer Replay Protection
The transfer endpoint accepts an `Idempotency-Key` header. The `TransferReplayMiddleware` handles this:
- **Same key + same payload** → returns the cached response (safe replay)
- **Same key + different payload** → returns `409 Conflict`
- **New key** → processes normally and stores the result

### Daily Limit Enforcement
Server-side daily outbound transfer limit of ₦500,000 per wallet, resetting at midnight WAT (UTC+1). Calculated by summing all debit transactions for the current WAT day.

### Audit Trail
Every balance mutation is recorded in an append-only `audit_entries` table — separate from `ledger_transactions`. Each entry captures: wallet ID, action type, amount, balance before/after, and a correlation ID linking both sides of a transfer.

### Error Handling
All errors return **RFC 7807 Problem Details** JSON via global exception middleware:
- `400` — Validation errors
- `404` — Wallet not found
- `409` — Replay conflict / concurrency conflict
- `422` — Business rule violation (insufficient funds, daily limit exceeded)
- `429` — Throttling limit exceeded

### Stretch Goals Implemented
- **Throttling** — Sliding-window request throttle on the transfer endpoint (10 req/s per user)
- **Health/readiness** — `/health` endpoint via ASP.NET Core health checks
- **Structured errors** — RFC 7807 Problem Details throughout

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/auth/token` | Issue a JWT (mock issuer) |
| `POST` | `/api/wallets` | Create a wallet |
| `GET` | `/api/wallets/{id}/balance` | Get balance |
| `POST` | `/api/wallets/{id}/credit` | Deposit funds |
| `POST` | `/api/wallets/{id}/transfer` | Transfer to another wallet |
| `GET` | `/api/wallets/{id}/statement` | Paginated history |
| `GET` | `/health` | Health check |

## How to Run

### Prerequisites
- Docker Desktop (with Docker Compose)

### Start with Docker Compose
```bash
docker compose up
```

This starts:
- **MySQL 8** on port 3306
- **NovaWallet API** on port 8080

The database schema is auto-created on startup via EF Core `EnsureCreated()`.

### Access Swagger UI
Open: http://localhost:8080/swagger

### Quick Test Flow
```bash
# 1. Get a JWT token
curl -X POST http://localhost:8080/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"customerId": "CUST-001", "customerName": "Damilola"}'

# 2. Create a wallet (use the token from step 1)
curl -X POST http://localhost:8080/api/wallets \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"customerId": "CUST-001"}'

# 3. Credit the wallet
curl -X POST http://localhost:8080/api/wallets/<walletId>/credit \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"amountKobo": 100000, "reference": "NIP-DEPOSIT"}'

# 4. Transfer (with replay protection)
curl -X POST http://localhost:8080/api/wallets/<fromWalletId>/transfer \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: unique-key-123" \
  -d '{"toWalletId": "<toWalletId>", "amountKobo": 50000, "reference": "P2P"}'
```

## Running Tests

```bash
# Unit tests (no DB needed)
dotnet test --filter "FullyQualifiedName!~Concurrency"

# All tests including concurrency (requires MySQL running via docker compose)
dotnet test
```

### Concurrency Test
The `ConcurrencyTests.ConcurrentTransfers_ShouldNeverAllowNegativeBalance` test fires 20 concurrent transfers against a wallet with only enough balance for 10, then asserts:
- No more than 10 succeed
- Balance is never negative
- Total money is conserved (source balance + destination balance = original deposit)

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 9 |
| Language | C# 13 |
| Database | MySQL 8 |
| ORM | Entity Framework Core 9 (Pomelo provider) |
| CQRS | MediatR 12 |
| Validation | FluentValidation 11 |
| Auth | JWT Bearer (mock issuer) |
| API Docs | Swashbuckle (Swagger) |
| Testing | xUnit + Moq + FluentAssertions |
| Containerization | Docker + Docker Compose |

## Assumptions & Trade-offs

| Decision | Rationale |
|----------|-----------|
| `EnsureCreated()` instead of EF migrations | Simpler for demo; schema auto-created from model on first run |
| Mock JWT issuer | Task specifies simplified auth is fine; focus is on middleware and claims handling |
| In-memory throttle | Sufficient for single-instance demo; would use Redis in production |
| No outbox pattern | Listed as stretch goal; focused on core correctness first |
| Pessimistic locking (FOR UPDATE) | Stronger guarantee than optimistic locking for financial systems |
