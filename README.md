# ATAMO

**A .NET substrate for routing work to agents — human, code, or AI — with per-user identity and full auditability.**

> _And Then A Miracle Occurs_

ATAMO is a library (with an optional standalone service host) for .NET applications that need to route messages to one or more agents, collect their responses, and keep a full audit trail of what happened and why. It is designed to be embedded inside an existing application with sensible in-process defaults, and to scale up by swapping individual layers — transport, inbox, persistence, credential storage, agent runtime — when the application grows into them.

## Status

🚧 **Early design. No production code yet.**

This repository is a 2026 reboot of an earlier 2015 effort. The current focus is design documentation, ADRs, and a first working sample. The public API is not yet stable and should not be considered usable. CI is wired for .NET 9 and will exercise `dotnet restore` / `build` / `test` once the first project lands.

If you are returning from the 2015 codebase, the component vocabulary has changed — see [ADR 0001](docs/adr/0001-rename-2015-vocabulary.md) for the rename map (Controller → Host, EventProvider → Source, ConfigurationProvider → Routing rule provider, etc.). The legacy design lives under [`docs/OldVersion/`](docs/OldVersion/) for reference and is no longer authoritative.

## What ATAMO is for

A developer using ATAMO can:

- Post a message into a hub and let it route to any number of registered agents — fire-and-forget, with retries and audit handled for them.
- Post a request and receive a stream of responses back as agents work — including responses that themselves become messages other agents can react to.
- Register agents that act on behalf of a specific user, with credentials and quotas scoped to that user.
- Mix in-process agents with disconnected agents (humans, batch jobs, remote services) without changing how dispatch works.
- Plug in their own routing rules, agents, and configuration providers without forking the core.
- Observe everything that happens through a Governor layer with configurable audit depth.

## What ATAMO is not

- **Not a message bus.** If you need an industrial-strength bus, use Wolverine, MassTransit, RabbitMQ, or NATS directly. ATAMO sits one layer above transport and is designed so you can swap its in-process default for one of those when the time comes.
- **Not a workflow engine.** If you need durable, crash-safe long-running workflows, use Temporal, Dapr Workflows, or Azure Durable Functions. ATAMO can be hosted on top of those; it does not try to replace them.
- **Not an LLM framework.** ATAMO's core knows nothing about LLMs. LLM routing is a use case built on top, not a feature of the substrate.

## Conceptual sketch

```
  ┌─────────────┐         ┌──────────────────────────────┐         ┌─────────────┐
  │   Source    │ ──msg─▶ │             Hub              │ ──▶ inbox ──▶ Agent A │
  │ (your app)  │         │  routing · streaming · audit │         │             │
  │             │ ◀─resp─ │                              │ ──▶ inbox ──▶ Agent B │
  └─────────────┘         └──────────────┬───────────────┘         └─────────────┘
                                         │
                                         ▼
                                   ┌───────────┐
                                   │ Governor  │  audit · policy · telemetry
                                   └───────────┘
```

**Sources** push messages in. **Agents** pull messages out of their per-agent **inboxes** at their own pace. The **Hub** routes between them using **routing rule providers**. The **Governor** observes everything. Source and Agent are _roles_, not types — a single component can play one or both. Disconnection (humans, batch jobs, flaky networks) is a difference of degree, not kind: every agent has an inbox; only the consumption rate differs.

Concrete shapes that fit:

- An inbound email becomes a message; an LLM agent triages it; the response becomes a message; an outbound email agent sends the reply. A message-store agent silently records the content of every message that flows through.
- A web request asks for a dashboard; three agents start streaming partial results back; the UI updates progressively as deltas arrive.
- A flagged email lands in a human-review agent's inbox; a reviewer picks it up hours later; their decision flows back through the system as a normal message. The same audit trail covers the LLM's earlier processing and the human's later decision.

## Documentation

The authoritative design lives under [`docs/`](docs/):

- [Documentation index](docs/README.md) — start here for the full map.
- [Vision](docs/design/vision.md) — what ATAMO is, who it is for, what it deliberately is not.
- [Architecture](docs/design/architecture.md) — high-level overview, with links to per-component detail in [`docs/design/components/`](docs/design/components/).
- [Principles](docs/design/principles.md) — the rules the codebase holds itself to.
- [Open questions](docs/design/open-questions.md) — decisions still in flight.
- [ADRs](docs/adr/) — architectural decisions, dated and numbered.
- [First sample](docs/design/first-sample.md) — the email-triage scenario that drives the v0 API.
- [Recipes](docs/recipes/) — short, goal-oriented guides for solving specific problems.

## Building

Once code lands, the canonical commands match the CI workflow ([`.github/workflows/dotnet-ci.yml`](.github/workflows/dotnet-ci.yml)):

```bash
dotnet restore
dotnet build --configuration Release
dotnet test  --configuration Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./TestResults/
```

Target framework: **.NET 9.0**. There are no `.csproj` or `.sln` files yet — they will arrive with the first scaffolding work.

## Contributing

Contributions, design feedback, and challenges to the framing are all welcome — particularly while the design is still settling. Please open an issue before starting significant work so the direction can be discussed first. Decisions that shape the codebase are recorded as ADRs; if you are proposing something that contradicts an existing ADR, please say so explicitly.

## License

[MIT](LICENSE)
