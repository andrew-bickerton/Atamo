# ATAMO

**A .NET substrate for routing work to agents — human, code, or AI — with per-user identity and full auditability.**

> _And Then A Miracle Occurs_

ATAMO is a library (and optional standalone service) for .NET applications that need to route messages to one or more agents, collect their responses, and keep a full audit trail of what happened and why. It is designed to be embedded inside an existing application with sensible in-process defaults, and to scale up by swapping out individual layers — transport, inbox, persistence, credential storage, agent runtime — when the application grows into them.

## Status

🚧 **Early design.** This repository is being (re)started in 2026 after an earlier 2015 effort. The current focus is on design documentation and a first working sample; the public API is not yet stable and should not be considered usable.

The design documents under [`docs/design/`](docs/design/) describe where ATAMO is heading. Decisions that shape the codebase are recorded as ADRs under [`docs/adr/`](docs/adr/).

## What ATAMO is for

A developer using ATAMO can:

- Post a message into a hub and let it route to any number of registered agents, fire-and-forget, with retries and audit handled for them.
- Post a request and receive a stream of responses back as agents work — including responses that themselves become messages other agents can react to.
- Register agents that act on behalf of a specific user, with credentials and quotas scoped to that user.
- Mix in-process agents with disconnected agents (humans, batch jobs, remote services) without changing how dispatch works.
- Plug in their own routing rules, their own agents, and their own configuration providers without forking the core.
- Observe everything that happens through a Governor layer with configurable audit depth.

## What ATAMO is not

- **Not a message bus.** If you need an industrial-strength bus, use Wolverine, MassTransit, RabbitMQ, or NATS directly. ATAMO sits one layer above transport and is designed so you can swap its in-process default for one of those when the time comes.
- **Not a workflow engine.** If you need durable, crash-safe long-running workflows, use Temporal, Dapr Workflows, or Azure Durable Functions. ATAMO can be hosted on top of one of those; it does not try to replace them.
- **Not an LLM framework.** ATAMO's core knows nothing about LLMs. LLM routing is a use case built on top, not a feature of the substrate.

## A quick conceptual sketch

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

Concrete examples of what fits this shape:

- An inbound email becomes a message; an LLM agent triages it; the response becomes a message; an outbound email agent sends the reply. A message-store agent silently records the content of every message that flows through.
- A web request asks for a dashboard; three agents start streaming partial results back; the UI updates progressively as deltas arrive.
- A flagged email lands in a human-review agent's inbox; a reviewer picks it up hours later from a web UI; their decision flows back through the system as a normal message. The same audit trail covers both the LLM's earlier processing and the human's later decision.

## Documentation

- [Vision](docs/design/vision.md) — what ATAMO is, who it is for, and what it deliberately is not
- [Architecture](docs/design/architecture.md) — high-level overview of the system, with links to per-component detail
  - [Components](docs/design/components/) — one page per core abstraction (Hub, Source, Agent, Inbox, Governor, Routing, Host)
- [Recipes](docs/recipes/) — short, goal-oriented guides for solving specific problems with ATAMO
- [First sample](docs/design/first-sample.md) — the email triage scenario that drives the v0 API
- [Second sample](docs/design/second-sample.md) — history-and-live review form, building on v0
- [Third sample](docs/design/third-sample.md) — disconnected human-review agent, building on v1
- [Principles](docs/design/principles.md) — design rules the codebase should hold itself to
- [Open questions](docs/design/open-questions.md) — decisions still in flight
- [Critique](docs/design/critique.md) — register of concerns about the design and the project around it
- [ADRs](docs/adr/) — architectural decisions, dated and numbered

## Contributing

Contributions, design feedback, and challenges to the framing are all welcome — particularly while the design is still settling. Please open an issue before starting significant work so the direction can be discussed first.

## License

[MIT](LICENSE)
