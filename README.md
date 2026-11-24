# QuickCover

A small taxi-insurance quote-and-buy system, built to practise the Acorn Insurance tech stack:
**.NET 8, Clean Architecture, Mediator, MassTransit + RabbitMQ, SQL Server, MongoDB, Redis, Docker**,
deploying later to **AKS** with Azure-managed equivalents.

This is **step 1**: the backend, running entirely on your machine with Docker Compose.

## Architecture

```
            POST /api/quotes                          POST /api/quotes/{id}/purchase
Customer ─────────────────────► Quote API ◄──────────────────────────────────────── Customer
                                   │  │  │
           rating factors (cache)  │  │  │  purchase + outbox message (ONE SQL transaction)
                    Redis ◄────────┘  │  └──────────► SQL Server ──(outbox delivery)──► RabbitMQ
                                      │                                                  │
                     quotes ──────────┘                                    QuotePurchased│
                    MongoDB                                                              ▼
                                                                                  Policy worker
                                                                     creates Policy in SQL, publishes
                                                                     PolicyIssued (via outbox)
```

| Project | Layer | What lives there |
|---|---|---|
| `QuickCover.Domain` | Domain | `Quote`, `Purchase`, `Policy`, `PricingEngine`. No dependencies. |
| `QuickCover.Contracts` | Shared | Integration events (`QuotePurchased`, `PolicyIssued`). |
| `QuickCover.Application` | Application | Commands/queries + handlers (Mediator), validation pipeline, ports (interfaces). |
| `QuickCover.Infrastructure` | Infrastructure | EF Core + outbox, Mongo repository, Redis cache, MassTransit setup. |
| `QuickCover.QuoteApi` | Host | Minimal API endpoints, ProblemDetails error handling, Swagger. |
| `QuickCover.PolicyWorker` | Host | `QuotePurchasedConsumer`, which issues policies. |
| `QuickCover.UnitTests` | Tests | Pricing and quote rules. |

Dependencies point inwards: Domain ← Application ← Infrastructure ← hosts.

## Run it

Prerequisites: Docker Desktop. For running outside Docker or running tests, the .NET 8 SDK.

```bash
docker compose up --build
```

- Swagger: http://localhost:5080/swagger
- RabbitMQ management: http://localhost:15673 (guest / guest)

Redis and RabbitMQ are published on host ports 6380, 5673 and 15673 so they do not clash with other local stacks. Override them with `REDIS_PORT`, `RABBITMQ_PORT` and `RABBITMQ_UI_PORT` in `.env` (see `.env.example`).

> **Apple Silicon:** the SQL Server image is x86-only. In Docker Desktop, turn on
> *Settings → General → Use Rosetta for x86/amd64 emulation*.

Run the tests:

```bash
dotnet test
```

## Try the flow

```bash
# 1. Get a quote
curl -s -X POST http://localhost:5080/api/quotes \
  -H "Content-Type: application/json" -H "X-Customer-Id: cust-123" \
  -d '{"driverAge":34,"yearsLicensed":12,"claimsLast5Years":1,"vehicleValue":18000,"use":"PrivateHire","postcodeArea":"L"}'

# 2. Buy it (use the quoteId from step 1; startDate within the next 30 days)
curl -s -X POST http://localhost:5080/api/quotes/<quoteId>/purchase \
  -H "Content-Type: application/json" -H "X-Customer-Id: cust-123" \
  -d '{"startDate":"2026-12-01"}'

# 3. Poll for the policy (404 for a second or two while the worker issues it)
curl -s http://localhost:5080/api/quotes/<quoteId>/policy -H "X-Customer-Id: cust-123"
```

Things worth trying:

- **Buy the same quote twice.** You get a `409 Conflict`. The unique index on `Purchases.QuoteId` guarantees it even under a race.
- **Stop the worker** (`docker compose stop policy-worker`), buy a quote, then start it again. The message waits in RabbitMQ and the policy appears when the worker comes back.
- **Stop Redis** and get a quote. It still works, just slower: the cache falls back to the source.
- **Watch the outbox**: query `OutboxMessage` in SQL Server right after a purchase.

## Design decisions (interview talking points)

- **Transactional outbox.** The purchase row and the `QuotePurchased` message are saved in one SQL transaction. MassTransit's delivery service sends the message afterwards. No "paid but no policy", no "policy but not paid".
- **Idempotent consumer.** Inbox deduplication, plus an existence check, plus a unique index on `Policies.QuoteId`.
- **Polyglot persistence.** Quotes are flexible, high-volume and short-lived, so they go in a document store. Money and policies are transactional, so they go in SQL. SQL is the source of truth for purchases. The Mongo status update is best-effort.
- **Cache-aside with graceful degradation.** Rating factors are cached in Redis for 10 minutes. A Redis outage slows quoting but doesn't stop it.
- **Ports and adapters.** Handlers depend on `IQuoteRepository`, `IEventPublisher` and similar interfaces. Swapping RabbitMQ for Azure Service Bus is a config change: `Messaging__Transport=AzureServiceBus` plus `ConnectionStrings__ServiceBus`.
- **Mediator, not MediatR.** It's source-generated (no reflection) and MIT-licensed. MediatR moved to a commercial licence in 2025.
- **MassTransit 8.x.** It's Apache 2.0. v9 requires a commercial licence.
- **Validation pipeline.** One Mediator `IPipelineBehavior` validates every command before its handler runs.
- **ProblemDetails.** Errors come back as RFC 7807 JSON. A 500 never leaks internals.

## Known shortcuts (deliberate, for step 1)

- `EnsureCreated()` instead of EF Core migrations. Before AKS, switch to migrations:
  `dotnet ef migrations add Initial -p src/QuickCover.Infrastructure -s src/QuickCover.QuoteApi`
- The customer comes from an `X-Customer-Id` header. This gets replaced by Entra ID tokens in a later step.
- Payment is faked.
- Connection strings are in config. In Azure they move to Key Vault, read via managed identity.

## Roadmap

1. ✅ Backend running locally (this step)
2. Next.js frontend: quote form, buy, "my policies"
3. Docs worker: consume `PolicyIssued`, write a PDF to Blob storage (Azurite locally)
4. Terraform: AKS, ACR, Azure SQL, DocumentDB, Managed Redis, Key Vault, Storage
5. CI/CD: build, test, push to ACR, Helm deploy
6. Workload identity + Key Vault + Private Link; Entra sign-in
7. Grafana + Loki + Prometheus (or Managed Grafana); OpenTelemetry tracing
8. Stretch: Service Bus transport, App Gateway + WAF
