# Principles

These are the rules ATAMO holds itself to. They exist to make design decisions easier when something is ambiguous and to make it visible when something has gone wrong. They are not aspirations; if a principle is consistently being violated, either the principle is wrong and should be revised, or the code is wrong and should be fixed.

## Contents

- [Sensible defaults, swappable layers](#sensible-defaults-swappable-layers)
- [The core knows nothing about its use cases](#the-core-knows-nothing-about-its-use-cases)
- [The substrate stores metadata; consumers store content](#the-substrate-stores-metadata-consumers-store-content)
- [Build one delightful path before generalising](#build-one-delightful-path-before-generalising)
- [Every agent contract assumes remote, even when in-process](#every-agent-contract-assumes-remote-even-when-in-process)
- [The Governor is a first-class layer, not bolted-on observability](#the-governor-is-a-first-class-layer-not-bolted-on-observability)
- [Compose, don't subsume](#compose-dont-subsume)
- [API ergonomics are a feature, not a finishing touch](#api-ergonomics-are-a-feature-not-a-finishing-touch)
- [Multi-tenancy is a design concern, not a future feature](#multi-tenancy-is-a-design-concern-not-a-future-feature)
- [Safety scales with trust](#safety-scales-with-trust)
- [When in doubt, defer](#when-in-doubt-defer)

## Sensible defaults, swappable layers

The first 30 seconds of using ATAMO must produce a working hub with no external dependencies. The first 30 minutes must reveal that every layer can be swapped — transport, inbox, persistence, credentials, agent runtime, audit sink — without rewriting application code.

This is the central commitment. A consumer should be able to start with the in-process default for a prototype, ship to production on the same code, and only reach for industrial-strength infrastructure when their problem actually demands it. ATAMO loses its reason to exist if either end of that range is unpleasant.

In practice this means: every swap point is an interface, every default is a sensible implementation of that interface, and the registration API treats the default and the alternatives identically.

## The core knows nothing about its use cases

ATAMO's core has no concept of LLMs, email, databases, HTTP, or any other domain. Those exist as agents and providers built on the public API.

The test is concrete: if a use case cannot be expressed purely as a consumer of ATAMO's public surface, the abstraction is leaking. When in doubt, the right move is to make the use case work as an external consumer first, and only generalise into the core once the same shape has appeared two or three times.

This principle is what protects ATAMO from becoming yet another framework that solves its author's specific problem and nobody else's.

## The substrate stores metadata; consumers store content

The Governor records what happened: message IDs, types, principals, routing decisions, inbox lifecycle events, timestamps, causal chains. It does not record what messages contained — payloads, email bodies, LLM prompts, user data.

If a consumer wants to persist message content, they write an agent for it. The agent subscribes to whatever message types matter, writes content to whatever store fits the use case (SQLite, blob storage, Kafka, a search index), and exposes that store however it wants — including by handling query messages routed through the substrate. From ATAMO's perspective this is just an agent with routing rules; the substrate doesn't need to know it exists.

This split has real consequences:

- The Governor's storage stays bounded and fast. Audit metadata has a small, regular shape; payloads do not. Conflating them would push the Governor toward being the wrong kind of database.
- Sensitive data stays out of the substrate by default. A consumer that handles user data, credentials, or regulated information chooses what to persist and where; ATAMO does not silently retain payloads they did not opt into storing.
- Content-storage requirements (encryption at rest, retention policy, redaction rules, geographic constraints) are application concerns handled by application code, not framework concerns hidden inside the substrate.

The "compose, don't subsume" principle applies here too. There are excellent existing tools for storing and querying message content; ATAMO does not need to provide one. It needs to make it easy for consumers to plug one in.

## Build one delightful path before generalising

Pluggability that is designed up front, before any single use case feels good, almost always produces an architecture that is coherent on paper and tedious in code. ATAMO will get one scenario right, then a second, and let the swap points emerge from the differences between them.

The first sample exists for exactly this reason. Generalisation that is not driven by a real second use case is speculative and almost always wrong. When a third scenario lands and a swap point is still cleanly factored, that is when it has earned its place.

## Every agent contract assumes remote, even when in-process

Agents communicate by message-passing only. There is no shared state, no callback into hub internals, no implicit ordering across agents. The in-process default must not develop assumptions that the out-of-process and remote cases cannot satisfy.

The temptation to expose "just this one" piece of internal state for performance, or "just this one" synchronous callback for convenience, is constant. Each such concession looks small in isolation and makes the standalone host strictly worse. The discipline is to refuse them by default and only relent with a recorded ADR when the cost of doing so is genuinely understood.

The inbox model is the structural commitment that makes this principle hold: every agent pulls from a queue, even when the queue happens to live in-process. Direct function-call dispatch is the abstraction that "remote-ness" would have to break to be cheap; eliminating it eliminates the temptation.

## The Governor is a first-class layer, not bolted-on observability

Audit, policy, and telemetry are part of the substrate. A consumer asking "what happened, why, and on whose behalf?" must get a complete answer without instrumenting their own code.

This is not a synonym for "log a lot." Logging is a Governor sink; the Governor itself is the part of the architecture that knows _what_ to record and _why_ — message receipt, routing decisions, inbox lifecycle, response correlation, policy enforcement, principal context. If a Governor sink is replaced with a no-op, the answers to those questions disappear. If logging is removed elsewhere in the system, they should not.

## Compose, don't subsume

Where mature tooling already exists for a problem ATAMO touches, ATAMO uses it rather than reimplementing it. Polly for retries. OpenTelemetry for telemetry. MailKit for email. Marten or EF Core for persistence. RabbitMQ, NATS, SQS, or Service Bus for industrial-strength inboxes. Wolverine or MassTransit for transport when scaling beyond in-process.

The standard ATAMO does hold itself to is _better composition than the alternative_, not _better implementation than the alternative_. Where ATAMO genuinely improves on existing tools is in the combination — delegated identity, multi-agent fan-out, and streaming-response-as-message integrated as a coherent substrate. Where it does not, it stands aside.

This principle directly shapes the inbox contract: it is the rough intersection of what mature brokers reliably support, so that the swap from default to industrial-strength is a configuration change rather than a re-architecture.

## API ergonomics are a feature, not a finishing touch

For a developer-facing library, the README and the first sample are the product. A consumer who finds the API tedious in the first ten minutes will not stay long enough to find the depth.

Concretely this means: typed messages with `record` types; `IAsyncEnumerable<T>` for streams; minimal-API-style fluent registration; source generators where they reduce boilerplate without hiding intent; clear async ownership; cancellation tokens propagated rigorously; no surprise allocations on hot paths. The library should feel like it was written in 2026, because it was.

The 2015 design's interface-prefix conventions (`IHubControl`, `IHubReceiver`, `ITelemetry`) are not preserved as-is. They will be redesigned around current .NET idioms.

## Multi-tenancy is a design concern, not a future feature

The principal abstraction is in the core from day one. Single-tenant use is a degenerate case where everyone shares one principal. This is the right way around: retrofitting multi-tenancy into a single-tenant codebase is one of the most expensive refactors in software, and ATAMO's audience includes platform builders who will need it.

The corollary is that "the user" is never a global; it is always a parameter, an ambient context, or an explicit handle. Anywhere it is implicit, that is a bug in the architecture even if no test currently fails because of it.

## Safety scales with trust

A trusted in-process agent registered by the host application can do almost anything. A user-submitted rule provider running on a multi-tenant deployment can do almost nothing without explicit grant. ATAMO's safety model must scale smoothly between these extremes rather than having two disconnected modes.

This principle has not yet been cashed out into a concrete model — it is one of the open questions. But it is recorded here as a commitment so that the model, when it lands, satisfies it.

## When in doubt, defer

Design docs and ADRs are cheap. Code that locks in a choice is expensive. When a decision is not yet forced by a real constraint, the right action is usually to record the question, note the candidate answers, and move on. ATAMO's open-questions document exists for exactly this purpose.

The opposite failure — deciding too early to feel productive — is one of the most common ways library projects drift away from their original purpose.
