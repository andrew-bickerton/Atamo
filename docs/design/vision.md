# Vision

This document describes what ATAMO is, who it is for, and — equally importantly — what it deliberately is not. It is the document that other design docs and ADRs should be checked against. If a proposed feature does not serve this vision, it probably belongs somewhere else.

## Contents

- [Positioning](#positioning)
- [Audience](#audience)
- [Primary scenarios](#primary-scenarios)
- [Design philosophy](#design-philosophy)
- [Where ATAMO sits in the .NET ecosystem](#where-atamo-sits-in-the-net-ecosystem)
- [Non-goals](#non-goals)
- [What success looks like](#what-success-looks-like)

## Positioning

ATAMO is **a .NET substrate for routing work to agents — human, code, or AI — with per-user identity and full auditability.**

That sentence does several deliberate things:

- **Substrate**, not framework. ATAMO provides primitives and seams; it does not dictate application structure.
- **Routing work to agents**, not transporting messages. The mental model is "this work needs to happen, who can do it?", not "this byte sequence needs to arrive somewhere."
- **Human, code, or AI.** An agent might be a stored procedure, a REST call, a local LLM, a queue of human reviewers, or another ATAMO hub. The substrate does not privilege any of these.
- **Per-user identity.** Every action runs on behalf of a known principal. This is a first-class concern, not an afterthought.
- **Full auditability.** The Governor layer is part of the core architecture, not a plugin you remember to install.

## Audience

ATAMO is for **.NET developers building products on top of it**. That includes:

- Application developers who want responsive UX through async fan-out without standing up a full bus.
- Service developers who want to expose work to internal or external agents over HTTP without building the routing themselves.
- Platform developers who want a multi-tenant integration layer where tenants can submit their own rules and agents.
- Developers building agentic AI features in .NET who need orchestration with delegated identity and audit.

It is explicitly _not_ aimed at end users, no-code tool builders, or operations teams looking for a turnkey integration platform.

## Primary scenarios

ATAMO is designed around six scenarios. These are the scenarios the API should make easy and the ones every design decision should be evaluated against.

### 1. Fire-and-forget event with multi-agent reaction

A source posts an event. Zero or more agents have registered interest in events of that shape. Each reacts independently. The source does not wait, does not need to stay connected, and is not informed of agent-level outcomes by default — though it can opt in to telemetry if it wants.

Routing decisions are made by configuration rules, not hardcoded. A new agent can be added without the source knowing.

### 2. Request with response stream

A source posts a request and stays connected (logically, not necessarily physically). Agents that can service the request begin producing responses. Responses arrive as a stream of messages — initial values, deltas, and completion signals — rather than a single reply. Multiple agents may contribute to the same response stream.

Crucially, **response messages are themselves messages on the hub.** Other agents can subscribe to them, transform them, log them, or trigger further work — subject to recursion protection so a response never reaches the agent that produced it.

### 3. Request with deferred retrieval

A source posts a request and disconnects. Later — possibly from a different process, possibly hours later — a client returns with the request's correlation ID and retrieves the result. Results have a configurable keep-alive before they are evicted.

This scenario shares plumbing with scenario 2 but adds persistence and explicit retrieval semantics. It is the bridge between in-process orchestration and distributed deferred work.

### 4. Long-lived feed

A source subscribes to an ongoing feed produced by one or more agents. The feed continues until the source disconnects or every contributing agent terminates it. This is scenario 2 without an implied end.

### 5. Disconnected agent

An agent that is not always available — a human reviewer, a batch job, a service on a flaky network, an agent under maintenance — receives messages that wait in its inbox until it is ready. The system continues to function, audit is preserved, and other agents are unaffected. The disconnected agent processes work at its own pace and produces responses when it can.

This scenario is structurally identical to the others; the inbox abstraction makes disconnection a difference of degree, not kind.

### 6. Responsive client application

A desktop, mobile, or rich-web client app needs to do work that does not belong on its UI thread — loading data from multiple sources, running a long search, kicking off a report. ATAMO is embedded in the app itself; the UI plays the Source role, posts a request, and stays responsive while agents fan out and stream results back. The same correlation/cancellation/streaming machinery that serves a web request handler in scenario 2 serves the UI thread here.

This scenario shares plumbing with scenario 2 but sits in a materially different deployment shape: there is no server, no network, and the audience is the application developer who wants async fan-out without writing the threading and correlation themselves. It is included because "make this UI responsive" is one of the most common reasons a developer reaches for ATAMO, and because the in-process embedded shape — rather than the standalone service shape — is the one most exercised here.

## Design philosophy

A few commitments the codebase should hold itself to. These are stated briefly here and elaborated in [principles.md](principles.md).

**Sensible defaults, swappable layers.** The first 30 seconds of using ATAMO should produce a working in-process hub with no external dependencies. The first 30 minutes should reveal that every layer — transport, inbox, persistence, credentials, agent runtime, audit sink — can be swapped without rewriting application code.

**The core knows nothing about its use cases.** ATAMO's core has no concept of LLMs, email, databases, or HTTP. Those exist as agents and providers built on the public API. If a use case cannot be expressed purely as a consumer of that API, the abstractions are leaking and the design is wrong.

**Build one delightful path before generalizing.** Pluggability that is designed up front, before any single use case is delightful, almost always produces an architecture that is coherent on paper and unpleasant to use. ATAMO will get one scenario right, then a second, and let the swap points emerge from the differences.

**Every agent contract assumes remote, even when in-process.** Agents communicate by message-passing only. There is no shared state, no callback into hub internals, no implicit ordering across agents. The in-process default must not develop assumptions that the out-of-process and remote cases cannot satisfy.

**The Governor is a first-class layer, not bolted-on observability.** Audit, policy, and telemetry are part of the substrate. A consumer should be able to ask "what happened, why, and on whose behalf?" and get a complete answer without instrumenting their own code.

## Where ATAMO sits in the .NET ecosystem

ATAMO is one layer above transport and one layer below workflow. The shape of the landscape it lives in:

| Layer | Examples | ATAMO's relationship |
|---|---|---|
| Transport | RabbitMQ, NATS, Azure Service Bus, in-memory channels | ATAMO uses one. The default is in-memory; production deployments swap it. |
| Bus / mediator | Wolverine, MassTransit, MediatR | Adjacent. ATAMO can be hosted on top of these or use its own minimal default. |
| **Substrate** | **ATAMO** | **Routing, agents, inboxes, per-user identity, Governor.** |
| Workflow | Temporal, Dapr Workflows, Durable Functions | ATAMO can run on top of these for durability when scenario 3 needs to outlive a process. |
| Integration platform | Logic Apps, n8n, Zapier | Different audience (end users vs developers). ATAMO is a library; these are products. |

The closest existing tools in spirit are Wolverine (philosophy of embeddable .NET-native messaging) and Dapr (philosophy of swappable building blocks). ATAMO differs from both by treating delegated identity, multi-agent fan-out, streaming-response-as-message, and disconnected agents as core concerns rather than features.

## Non-goals

Stated explicitly so feature requests and scope creep can be evaluated against them.

- ATAMO is not trying to be a better message bus. If you need durable queues, exactly-once semantics, or geo-replication, use a real broker underneath.
- ATAMO is not trying to be a durable workflow engine. Long-running, crash-safe orchestration belongs to Temporal or its peers.
- ATAMO is not trying to provide its own LLM abstractions, prompt templates, or vector storage. LLM features are use cases, built by consumers on top of the substrate.
- ATAMO's core does not ship generic agent implementations (HTTP, SQL, email, LLM). The 2015 design imagined those in the substrate; the 2026 principle "core knows nothing about its use cases" rules them out of the core. Reusable agent patterns are still welcome — but as a separate `Atamo.Agents.Common` companion package rather than as part of the substrate. That package, and the boundary between it and the core, is part of the project-structure decision recorded in [open questions](open-questions.md).
- ATAMO is not trying to be cross-language. Other-language clients can interact via the standalone HTTP service, but the library is .NET-native and designed for .NET ergonomics.
- ATAMO is not aiming for no-code or low-code use. The audience is developers writing code.

## What success looks like

For the v0 milestone:

- A developer can `dotnet add package` ATAMO into a console app and have a working in-process hub in under five minutes.
- The first sample (an inbound-email triage scenario combining an email integration, a local-LLM agent, and a message-store agent) runs end-to-end and is genuinely useful in its own right.
- The agent contract has been pressure-tested against at least two structurally different agents (one fast in-process, one slow disconnected) and survives without special cases.
- The Governor produces a complete, queryable audit trail of every message, route decision, agent action, and response.

Beyond v0, success is measured by whether developers building on ATAMO say it made something hard easier or something risky safer — and whether they stay when their systems grow into needing the swap points.
