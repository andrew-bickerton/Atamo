# Architecture

This document is the high-level overview of ATAMO's architecture. It introduces the core abstractions, the message flow that connects them, and the deliberate seams where industrial-strength tooling can replace the defaults. Detail on each component lives in its own page under [`components/`](components/).

## Contents

- [Core abstractions](#core-abstractions)
- [How the pieces fit together](#how-the-pieces-fit-together)
- [Message flow](#message-flow)
- [Swap points](#swap-points)
- [Embedded vs standalone](#embedded-vs-standalone)
- [What this document does not specify](#what-this-document-does-not-specify)

## Core abstractions

ATAMO has fewer primitives than the original 2015 design. Each abstraction below has its own page covering its responsibilities, contract, and open questions. The summaries here are deliberately short.

**[Message](components/message.md)** — the unit of work flowing through ATAMO. Events, requests, and responses are all messages, distinguished by semantics rather than class hierarchy. Messages are immutable and carry a correlation ID, a principal, a type/key, and a payload.

**[Hub](components/hub.md)** — the routing engine. Receives messages, evaluates routing rules, delivers them to agent inboxes, and routes responses back. Stateless about message content, stateful about in-flight correlations and subscriptions.

**[Source](components/source.md)** — a role. A component playing the Source role injects messages into the hub on its own initiative. Web request handlers, IMAP pollers, CLI commands, timers, and other agents' responses are all sources.

**[Agent](components/agent.md)** — a role. A component playing the Agent role pulls messages from its inbox, processes them, and produces zero or more response messages. Agents are the only place ATAMO touches the outside world on the hub's behalf.

**[Inbox](components/inbox.md)** — every agent has one. A durable, per-agent queue managed by ATAMO that holds messages dispatched to an agent until it consumes them. The inbox is what allows agents to be disconnected, slow, restarted, or temporarily overwhelmed without messages being lost. Its contract aligns with industry-standard message-broker semantics so that the default SQLite-backed implementation can be swapped for RabbitMQ, NATS JetStream, Azure Service Bus, AWS SQS, or similar.

**[Routing rule provider](components/routing.md)** — decides, given a message, which agent inboxes should receive it and how the message should be shaped for each. The default provider does simple type-and-principal matching; consumers can register their own for richer logic.

**[Principal](components/principal.md)** — the identity on whose behalf a message is processed. Carries credentials and quotas scoped to the agents that act for it. Tenants and groups are built on principals.

**[Governor](components/governor.md)** — the audit, policy, and telemetry layer. Observes every message, routing decision, inbox event, and response. Storage is queryable; historical retrieval is a first-class capability.

**[Host](components/host.md)** — the application that contains the hub. Embedded (the consuming application) or standalone (ATAMO's own service host exposing HTTP/gRPC). Owns configuration, registration, and lifetime.

The 2015 _Controller_ collapses into Host. _EventProvider_ collapses into Source. _EventType_ and _ActionType_ collapse into message type definitions. _ConfigurationProvider_ becomes _routing rule provider_. _ConfigurationRule_ becomes _routing rule_. These renamings are confirmed in [ADR 0001](../adr/0001-rename-2015-vocabulary.md).

## How the pieces fit together

Sources push messages in. Agents pull messages out of their inboxes. The hub is the broker that connects them, applying routing rules to decide which inbox each message lands in. The Governor watches everything.

```
  ┌─────────────┐       ┌─────────────────────────────────────┐       ┌──────────────┐
  │   Source    │ ─────▶│                Hub                  │──▶ inbox ──▶ Agent A │
  │             │       │                                     │       │              │
  │             │       │  · routing rule providers           │──▶ inbox ──▶ Agent B │
  │             │       │  · correlation tracking             │       │              │
  │             │ ◀─────│  · response routing                 │──▶ inbox ──▶ Agent C │
  └─────────────┘       │  · recursion protection             │       └──────────────┘
                        └──────────────────┬──────────────────┘
                                           │
                                           ▼
                                     ┌───────────┐
                                     │ Governor  │
                                     └───────────┘
```

A few structural properties worth highlighting because they fall out of this shape:

**Disconnection is not a special case.** Because every agent pulls from an inbox rather than being invoked directly, an agent that takes hours to respond and an agent that takes microseconds use the same contract. The fifth scenario in [vision.md](vision.md) — disconnected agents — is enabled by inboxes existing at all.

**Responses are first-class messages.** When an agent produces a response, it re-enters the hub through the same routing path. Other agents can subscribe to it; the Governor records it; another agent's response can in turn trigger work elsewhere. Recursion protection prevents an agent from receiving its own output.

**Audit is structural, not bolted on.** The Governor is in the diagram because it is part of the architecture, not a logging library someone remembered to add. Its storage is queryable, which is what makes the persistence-retrieval and history-then-live patterns in the samples possible.

**Source and Agent are roles, not types.** A single component can play one or both, registered separately for each role. The roles have genuinely different contracts (Source owns its lifecycle; Agent reacts to its inbox), so unifying them would bloat the common case for the rare hybrid. Detail and rationale live on the [Agent page](components/agent.md). This is recorded in [ADR 0002](../adr/0002-source-and-agent-as-roles.md).

## Message flow

The canonical flow for a request with streaming response:

1. A **source** receives or constructs a message and submits it to the **hub** under a given **principal**.
2. The hub asks each registered **routing rule provider** to evaluate the message. Each provider returns zero or more (agent, shaped-message) pairs.
3. The hub **enqueues** each shaped message into the target agent's **inbox**, recording the enqueue with the **Governor**.
4. Each target agent, on its own schedule, **pulls** from its inbox, processes the message, and **acknowledges** it. The agent may produce zero or more **response messages**, each tagged with the originating correlation ID.
5. Response messages re-enter the hub as first-class messages. They are routed normally, _except_ they cannot be enqueued to the inbox of the agent that produced them (recursion protection).
6. Response messages reach the original source if the source subscribed to its own correlation ID; otherwise they are simply observable by other agents and the Governor.
7. The Governor records the full causal chain, including inbox lifecycle events.

The flow above carries a hard requirement: **the causal chain of any message must be identifiable with authority from the Governor alone.** Given any message — source-injected or agent-produced — the substrate must be able to answer "what messages and agent actions cascaded from this?" and the inverse "what caused this message to exist?" Both directions are first-class queries against the Governor's audit log. Application code does not thread its own correlation context to make this work; the substrate does it. See the [Governor page](components/governor.md) for the storage and query side of this contract.

Fire-and-forget is a degenerate case where no source subscribes to the correlation ID. Long-lived feeds are the same flow with no inherent termination signal. Disconnected agents are the same flow where the gap between step 3 and step 4 is hours or days rather than microseconds.

## Swap points

ATAMO's design commitment is that every layer below can be replaced without rewriting the application code that uses the hub. The default in each row is what `dotnet add package` gives you with no further configuration; the alternatives are what consumers swap in as their needs grow.

| Layer | Default | Alternatives |
|---|---|---|
| Transport | In-memory `System.Threading.Channels` | Wolverine, MassTransit, NATS, RabbitMQ, Azure Service Bus |
| Inbox | SQLite-backed durable queue | RabbitMQ, NATS JetStream, Azure Service Bus, AWS SQS, Redis Streams |
| Persistence (correlations, deferred results, audit log) | In-memory + SQLite | Postgres / Marten, SQL Server |
| Credential storage | Dev-only in-memory store | Azure Key Vault, AWS Secrets Manager, integrations like Nango or Composio |
| Agent runtime | In-process | Out-of-process worker over the standalone HTTP/gRPC interface, eventually WASM-isolated |
| Governor sink | Console + queryable SQLite | OpenTelemetry, file, database, custom |
| Routing rule provider | Type-and-principal matching with template substitution | Consumer-supplied providers with arbitrary logic |
| Host model | Embedded library | Standalone service with HTTP/gRPC API |

The seams are deliberately at boundaries where abstractions are well-understood and existing tools are strong. ATAMO does not try to compete with those tools; it tries to be a good citizen in front of them.

## Embedded vs standalone

ATAMO is designed to run in two deployment shapes from the same codebase:

**Embedded** — referenced as a NuGet package, registered into the consuming application's DI container, sharing its process and lifetime. This is the default and the one the API ergonomics are tuned for. In-process by default does not mean in-process forever; the agent contract assumes remote-ness so that any agent can later be hoisted out of the host process without API changes.

**Standalone** — ATAMO's own service host, exposing the hub over HTTP and/or gRPC. Sources submit messages over the network; agents pull from their inboxes over the network. The standalone host is essentially the embedded library wrapped in a thin transport adapter.

The duality is real, not aspirational: the standalone host should be implementable in a single project that depends on the embedded library and adds nothing to the public API. If the standalone case needs primitives the embedded case does not have, that is a signal the embedded API is incomplete, not that the standalone case needs special treatment.

The inbox model directly enables the standalone case. Because agents pull from inboxes rather than being dispatched into via function call, "agent on the other side of an HTTP connection" is a natural shape rather than a complication. Detail lives on the [Host page](components/host.md).

## What this document does not specify

This document is a high-level overview. It does not commit to:

- Exact interface names or method signatures (those will live in code).
- The wire protocol of the standalone host (HTTP vs gRPC vs both — see open questions).
- The persistence schema for correlations, inbox state, or audit (driven by the persistence-layer choice).
- The sandboxing strategy for user-submitted code (see open questions).
- The exact relationship between Governor storage, inbox storage, and general persistence (see open questions).
- The full set of inbox policies in v0 vs later (see open questions).

Each of these will be addressed either on the relevant component page or in a dedicated ADR when the decision is made.
