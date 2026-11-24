# QuickCover

A practice taxi-insurance quote-and-buy system: .NET 8 Clean Architecture backend, Mediator, MassTransit + RabbitMQ,
SQL Server, MongoDB, Redis, with a Next.js frontend, all run with Docker Compose. It will later be deployed to AKS. See README.md for
the architecture diagram, design decisions and roadmap.

## Commands

```bash
docker compose up --build        # whole stack: API :5080, web :3080, RabbitMQ UI :15673
dotnet build                     # from repo root (QuickCover.sln)
dotnet test                      # unit tests in tests/QuickCover.UnitTests
cd src/QuickCover.Web && npm run dev    # frontend outside Docker; API_URL defaults to http://localhost:5080
cd src/QuickCover.Web && npm run lint && npm run build
```

## Layout and rules

- `src/QuickCover.Domain`: entities and pricing. No dependencies on other projects or packages.
- `src/QuickCover.Contracts`: integration events shared by the API and workers.
- `src/QuickCover.Application`: Mediator commands/queries + handlers, ports in `Common/Abstractions.cs`.
- `src/QuickCover.Infrastructure`: adapters (EF Core + outbox, Mongo, Redis, MassTransit).
- `src/QuickCover.QuoteApi`, `src/QuickCover.PolicyWorker`: hosts.
- `src/QuickCover.Web`: Next.js **16** (App Router, TypeScript, Tailwind 4, Cache Components on). This is newer than
  most training data: read `node_modules/next/dist/docs/` before changing Next code (see its AGENTS.md). Pages that read
  route params on the client need a `<Suspense>` boundary, and the React lint rule forbids setState directly in effects.
  The browser only calls `/api/*` on the web app. A catch-all route handler (`app/api/[...path]/route.ts`) proxies to the Quote API using `API_URL` read at
  runtime, so the API needs no CORS settings and stays internal in AKS.
- Dependencies point inwards: Domain ← Application ← Infrastructure ← hosts. Handlers depend on ports, never on
  infrastructure types.
- Use **Mediator** (source-generated, MIT), not MediatR. Keep **MassTransit on 8.x** (v9 is commercial).
  Package versions are central in `Directory.Packages.props`.
- API request/response records live in the host (`QuoteEndpoints.cs`), separate from domain types.
- Errors are RFC 7807 ProblemDetails via `ApiExceptionHandler`. Throw `ValidationException`, `NotFoundException`,
  `ConflictException` or `DomainException`, and don't return error results by hand.
- Another customer's resource returns 404, not 403.
- Customer identity is the `X-Customer-Id` header for now (Entra ID later). The web app keeps a demo customer id
  in localStorage and sends it on every request.

## Local environment notes

- Host ports are remapped because another local stack (ms-video-downloader) holds the defaults: Redis **6380**,
  RabbitMQ **5673 / 15673**, web **3080** (3000 is its Grafana; 8080 is also taken). Override with `.env`
  (see `.env.example`). Containers still use the standard ports between themselves.
- The schema is created with `EnsureCreated()` in Development. New SQL tables/columns need
  `docker compose down -v` (or migrations, which are on the roadmap).

## Housekeeping

When a change affects how to run, configure or use the project, update README.md and this file in the same change.
