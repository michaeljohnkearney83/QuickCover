# QuickCover

A small taxi-insurance quote-and-buy system, built to practise the Acorn Insurance tech stack:
**.NET 8, Clean Architecture, Mediator, MassTransit + RabbitMQ, SQL Server, MongoDB, Redis, Docker**,
deploying later to **AKS** with Azure-managed equivalents.

Steps 1 to 4 are done: the backend, a Next.js frontend and a docs worker that produces policy PDFs, all running on
your machine with Docker Compose. Terraform for the Azure environment is in [infra/terraform](infra/terraform/README.md).
It has been written and validated but not yet applied.

## Architecture

The browser talks only to the Next.js web app. Its `/api/*` route handler proxies to the Quote API
(a backend-for-frontend), so the API needs no CORS settings and can stay internal.

```
Browser ──► Web (Next.js, /api/* proxy) ──► Quote API

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
                                                                                         │
                                                                            PolicyIssued │
                                                                                         ▼
                                                     Blob storage ◄──── PDF ──────── Docs worker
                                                  (Azurite locally)                (QuestPDF, no SQL)
                                                         │
                       GET /api/policies/{id}/document ◄─┘  (API checks the policy is yours, then streams it)
```

| Project | Layer | What lives there |
|---|---|---|
| `QuickCover.Domain` | Domain | `Quote`, `Purchase`, `Policy`, `PricingEngine`. No dependencies. |
| `QuickCover.Contracts` | Shared | Integration events (`QuotePurchased`, `PolicyIssued`). |
| `QuickCover.Application` | Application | Commands/queries + handlers (Mediator), validation pipeline, ports (interfaces). |
| `QuickCover.Infrastructure` | Infrastructure | EF Core + outbox, Mongo repository, Redis cache, MassTransit setup, blob document store. |
| `QuickCover.QuoteApi` | Host | Minimal API endpoints, ProblemDetails error handling, Swagger. |
| `QuickCover.PolicyWorker` | Host | `QuotePurchasedConsumer`, which issues policies. |
| `QuickCover.DocsWorker` | Host | `PolicyIssuedConsumer`, which renders the policy schedule PDF and stores it in blob storage. |
| `QuickCover.Web` | Frontend | Next.js 16 (App Router, Tailwind): quote form, buy, "My policies", plus a proxy to the API. |
| `QuickCover.UnitTests` | Tests | Pricing and quote rules, PDF rendering. |
| `infra/terraform` | Infrastructure as code | AKS, ACR, Azure SQL, DocumentDB, Managed Redis, Storage, Key Vault. |

Dependencies point inwards: Domain ← Application ← Infrastructure ← hosts.

## Run it

Prerequisites: Docker Desktop. For running outside Docker or running tests, the .NET 8 SDK.

```bash
docker compose up --build
```

- Web app: http://localhost:3080
- Swagger: http://localhost:5080/swagger
- RabbitMQ management: http://localhost:15673 (guest / guest)

Redis, RabbitMQ and the web app are published on host ports 6380, 5673, 15673 and 3080 so they do not clash with other local stacks. Override them with `REDIS_PORT`, `RABBITMQ_PORT`, `RABBITMQ_UI_PORT` and `WEB_PORT` in `.env` (see `.env.example`). Azurite (blob storage) is on 10000 (`AZURITE_PORT`); browse it with Azure Storage Explorer using the local emulator connection.

> **Apple Silicon:** the SQL Server image is x86-only. In Docker Desktop, turn on
> *Settings → General → Use Rosetta for x86/amd64 emulation*.

Run the frontend outside Docker (Node 20+; needs the API on http://localhost:5080, or set `API_URL`):

```bash
cd src/QuickCover.Web
npm install
npm run dev      # http://localhost:3080
```

Run the tests:

```bash
dotnet test
```

## Deploy to Azure

See [infra/terraform/README.md](infra/terraform/README.md). In short: run `scripts/bootstrap-state.ps1` once, then
`terraform init` / `plan` / `apply` with `environments/dev.tfvars`. Applying creates billable resources.

## Try the flow

In the browser: open http://localhost:3080, get a price, pay (simulated), and watch the policy appear a second
later. Then click **Download policy schedule (PDF)**. The header shows a demo customer id: click it to switch customer and check you can't see their quotes.

Or with curl:

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

# 4. List the customer's policies
curl -s http://localhost:5080/api/policies -H "X-Customer-Id: cust-123"

# 5. Download the policy schedule (use the policyId from step 3 or 4)
curl -s -o policy.pdf http://localhost:5080/api/policies/<policyId>/document -H "X-Customer-Id: cust-123"
```

Things worth trying:

- **Buy the same quote twice.** You get a `409 Conflict`. The unique index on `Purchases.QuoteId` guarantees it even under a race.
- **Stop the worker** (`docker compose stop policy-worker`), buy a quote, then start it again. The message waits in RabbitMQ and the policy appears when the worker comes back.
- **Stop Redis** and get a quote. It still works, just slower: the cache falls back to the source.
- **Watch the outbox**: query `OutboxMessage` in SQL Server right after a purchase.
- **Stop the docs worker** (`docker compose stop docs-worker`) and buy a quote. The policy is issued but the PDF
  download says it is still being prepared. Start the worker again and the document appears.

## Design decisions (interview talking points)

- **Transactional outbox.** The purchase row and the `QuotePurchased` message are saved in one SQL transaction. MassTransit's delivery service sends the message afterwards. No "paid but no policy", no "policy but not paid".
- **Idempotent consumer.** Inbox deduplication, plus an existence check, plus a unique index on `Policies.QuoteId`.
- **Polyglot persistence.** Quotes are flexible, high-volume and short-lived, so they go in a document store. Money and policies are transactional, so they go in SQL. SQL is the source of truth for purchases. The Mongo status update is best-effort.
- **Cache-aside with graceful degradation.** Rating factors are cached in Redis for 10 minutes. A Redis outage slows quoting but doesn't stop it.
- **Ports and adapters.** Handlers depend on `IQuoteRepository`, `IEventPublisher` and similar interfaces. Swapping RabbitMQ for Azure Service Bus is a config change: `Messaging__Transport=AzureServiceBus` plus `ConnectionStrings__ServiceBus`.
- **Mediator, not MediatR.** It's source-generated (no reflection) and MIT-licensed. MediatR moved to a commercial licence in 2025.
- **MassTransit 8.x.** It's Apache 2.0. v9 requires a commercial licence.
- **Validation pipeline.** One Mediator `IPipelineBehavior` validates every command before its handler runs.
- **Backend-for-frontend proxy.** The Next.js route handler reads `API_URL` per request, so one web image is promoted
  through every environment. It forwards only `/api/quotes/*` and `/api/policies/*`. Entra tokens get attached here later.
- **Async issuance in the UI.** Purchase returns `202 Accepted`. The quote page polls the policy endpoint and, after
  30 seconds, falls back to "it will appear in My policies".
- **Event-carried state transfer.** `PolicyIssued` carries the premium and dates, so the docs worker builds the PDF
  from the event alone. It has no SQL connection at all: a smaller blast radius and one less thing to scale together.
- **Idempotency without an inbox.** The docs worker writes `policies/{policyId}.pdf`. A redelivered message
  overwrites the same blob with the same document, so it needs no SQL inbox/outbox (`AddMessaging(..., useSqlOutbox: false)`).
- **Private documents.** The blob container is private. The API checks the policy belongs to the caller before it
  streams the file, so storage never needs public access or customer-aware rules.
- **QuestPDF licence.** Free Community licence under $1M annual revenue. A larger business needs a paid licence, or
  can swap the renderer for PDFsharp/MigraDoc (MIT). Only `PolicyDocumentRenderer` would change.
- **Same config keys everywhere.** Key Vault secrets are named `ConnectionStrings--Sql` and so on, which the .NET Key
  Vault provider maps to `ConnectionStrings:Sql`. The apps read the same keys in Docker and in Azure.
- **No passwords for platform plumbing.** AKS pulls from ACR with its kubelet identity (AcrPull). Local accounts are
  disabled, so people reach the cluster with Entra ID through Azure RBAC. Terraform state uses Entra auth, not storage keys.
- **Network shape decided up front.** The VNet and AKS subnet exist from day one, because moving a cluster to another
  subnet means rebuilding it. A subnet is already reserved for step 6's private endpoints.
- **ProblemDetails.** Errors come back as RFC 7807 JSON. A 500 never leaks internals.

## Known shortcuts (deliberate, for step 1)

- `EnsureCreated()` instead of EF Core migrations. Before AKS, switch to migrations:
  `dotnet ef migrations add Initial -p src/QuickCover.Infrastructure -s src/QuickCover.QuoteApi`
- The customer comes from an `X-Customer-Id` header (the web app keeps a demo id in localStorage). This gets replaced by Entra ID tokens in a later step.
- Payment is faked.
- Blob access uses a connection string (Azurite's well-known dev key). In Azure it becomes a managed identity with
  the Storage Blob Data role, and no key at all.
- Policies issued before the docs worker existed have no PDF. There's no backfill job.
- Connection strings are in config. In Azure they move to Key Vault, read via managed identity.

## Roadmap

1. ✅ Backend running locally
2. ✅ Next.js frontend: quote form, buy, "my policies"
3. ✅ Docs worker: consume `PolicyIssued`, write a PDF to Blob storage (Azurite locally)
4. ✅ Terraform: AKS, ACR, Azure SQL, DocumentDB, Managed Redis, Key Vault, Storage (written and validated, not applied)
5. CI/CD: build, test, push to ACR, Helm deploy
6. Workload identity + Key Vault + Private Link; Entra sign-in
7. Grafana + Loki + Prometheus (or Managed Grafana); OpenTelemetry tracing
8. Stretch: Service Bus transport, App Gateway + WAF
