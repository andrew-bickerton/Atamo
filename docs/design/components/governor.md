# Governor

The Governor is ATAMO's audit, policy, and telemetry layer. It observes every message, routing decision, inbox event, and response; enforces policy at the hub's request; and exposes its storage as a queryable resource that the rest of the system can read from.

## Contents

- [Overview](#overview)
- [Responsibilities](#responsibilities)
- [Audit storage as a queryable resource](#audit-storage-as-a-queryable-resource)
- [Policy enforcement](#policy-enforcement)
- [Sinks and the swap point](#sinks-and-the-swap-point)
- [Relationship with the Inbox](#relationship-with-the-inbox)
- [Open questions](#open-questions)
- [References](#references)

## Overview

The Governor exists because ATAMO promises full auditability. A consumer asking "what happened, why, and on whose behalf?" must get a complete answer without instrumenting their own code. The Governor is the part of the architecture that makes this possible.

It does three things:

- **Audit.** Records every event that happens inside the substrate.
- **Policy.** Enforces rate limits, allowed-agent rules, and similar constraints at the hub's request.
- **Telemetry.** Provides the observability surface that consumers and operators use to understand the system's behaviour.

The Governor is _not_ a logging library. Logging is one thing a Governor sink can do. The Governor itself is the part of the architecture that knows _what_ to record and _why_; sinks are interchangeable.

## Responsibilities

The Governor records:

- **Message lifecycle events.** Receive (a source submitted a message), route decision (rule providers produced these targets), enqueue (delivered to this agent's inbox), lease (this agent pulled it), ack/nack (this agent committed or rejected it), dead-letter (this message failed terminally), response (this agent emitted this).
- **Stage timestamps.** Every lifecycle event is timestamped with sufficient precision to support per-stage latency analysis. The combination of timestamps across a message's lifetime is the canonical record of where time was spent — receive-to-route, route-to-enqueue, enqueue-to-lease (queue depth), lease-to-ack (agent processing). Operators investigating slow paths query the Governor; they do not reconstruct timing from log scrapers or application instrumentation.
- **Principal context.** Every event is tagged with the principal on whose behalf it occurred.
- **Causal chains.** Response messages carry their originating correlation ID; the Governor records the produced-by relationship so the full cascade from any starting message can be reconstructed. Two questions must be answerable with authority from Governor queries — without joining application logs or inferring from timestamps: "given this message (source-injected or agent-produced), what messages and agent actions cascaded from it?" and the inverse "given this message, what caused it to exist?" Application code does not need to thread its own correlation context to make this work.
- **Policy decisions.** When the Governor blocks an action (rate limit exceeded, agent not allowed for principal), the decision and its reasoning are recorded.

The Governor enforces:

- **Rate limits.** Per-principal, per-agent, or globally configured.
- **Allowed agents per principal.** Multi-tenancy boundary enforcement.
- **Audit retention.** When old data should be archived or deleted.
- **Quota.** Per-principal usage limits where applicable.

The Governor exposes:

- **Queryable audit storage.** Operators and the substrate itself can query the Governor's storage to answer metadata questions: what happened, when, on whose behalf, with what outcomes. Consumers wanting to query _message content_ (email bodies, LLM prompts, etc.) build their own content-storing agents; that data does not live in the Governor.
- **In-flight message visibility.** Operators can ask "what is currently in flight?" — messages received but not yet routed, enqueued but not yet leased, leased but not yet acked, dead-lettered. The audit log's event sequence is sufficient to derive this; whether the Governor exposes a denormalised "current state" view or expects consumers to fold the event stream themselves is recorded as an open question (see [open-questions.md](../open-questions.md)).
- **Subscription to live events.** Components can subscribe to Governor events as they happen, which is what enables the history-then-live pattern.

## Audit storage as a queryable resource

The Governor's audit storage is queryable, not just write-only. Operators investigating "what happened?" can ask the Governor directly; the substrate itself uses the audit log to support the history-then-live pattern for live metadata feeds. Querying message content is a different concern, addressed by consumer-built content-storing agents (see the [persist-messages](../../recipes/persist-messages.md) recipe) — that data does not flow through the Governor.

In practice this means:

- **The audit log can be read.** Not just appended to. Queries by message type, principal, correlation ID, time range, and agent are supported.
- **The audit log is sequenced.** Events have a monotonic ordering that supports cursor-based reading and resumable streams.
- **The audit log is the source of truth for "what happened."** Other components read from it rather than maintaining their own parallel records.

Treating the Governor as queryable from day one closes off the easy mistake of treating audit as a side-effect that only matters when something goes wrong. In ATAMO, audit is something the system actively uses for its own primary functionality.

## Policy enforcement

The Governor is the enforcement point for policy. When the hub is about to take an action that policy might block (enqueue into a rate-limited agent's inbox, dispatch on behalf of a principal that has exceeded its quota), the hub asks the Governor; the Governor decides.

The decision and reasoning are recorded as part of the audit log. If a request is blocked, "why" is auditable just as readily as "what."

Policy is configured per-deployment. The default Governor enforces nothing — every action is allowed. Production deployments configure rate limits, allowed-agent rules, and quotas appropriate to their use case.

## Sinks and the swap point

A Governor _sink_ is where audit data ultimately ends up. The Governor's core knows how to record events; sinks are interchangeable destinations.

Built-in sinks:

- **Console sink.** Writes events to standard output. Useful in development.
- **SQLite sink.** Writes events to a local SQLite database. The default for embedded use; supports the queryable-audit promise out of the box.

Swap targets:

- **OpenTelemetry sink.** For production deployments using observability platforms (Honeycomb, Grafana, etc.).
- **File sink.** Append-only structured logs, suitable for shipping to log aggregators.
- **Database sink.** Postgres, SQL Server, or similar for production-grade audit retention.
- **Custom sinks.** Consumers can write their own.

Multiple sinks may be configured simultaneously. The architectural commitment is that _at least one_ sink supports queryable read access, because the substrate's own retrieval features depend on it. In the default configuration, the SQLite sink fulfils this; in production deployments using an OpenTelemetry-only configuration, the consumer is responsible for configuring a queryable sink as well if they want retrieval-agent functionality.

## Relationship with the Inbox

The Governor and the Inbox both store durable data, both are queryable, and they overlap. The Governor records every inbox event (enqueue, lease, ack, nack, dead-letter); the inbox itself stores the messages waiting to be processed.

The cleanest design is for the two to share storage where possible. A query against the Governor can then see both "this message is currently in agent X's inbox, leased since T" and "this message was acked by agent Y at T+5 minutes" through a single query interface.

Whether this means literally one database or separate stores with a unified query layer is an open question. The principle is that audit is queryable across both; the implementation can vary.

See the [Inbox page](inbox.md#relationship-with-the-governor) for the queue side of this relationship.

## Open questions

- **Inbox/Governor storage relationship.** Single store with views, separate stores with unified query, or fully separate? Recorded in [open-questions.md](../open-questions.md).
- **Default audit retention.** How long does the SQLite sink keep events by default? Probably configurable with a sensible default (30 days?), but worth deliberate decision.
- **Sensitive data in audit.** Principals carry credentials; messages may carry private payloads. The Governor needs a story for redaction or selective recording. Likely answer: opt-in fields are recorded by default, opt-out fields are redacted at the sink, exact policy is per-deployment.
- **Querying across multiple sinks.** If a deployment has both a SQLite sink and an OpenTelemetry sink, can a query span both? Probably not — one sink is designated queryable, others are write-through — but worth confirming.
- **Performance impact at high volume.** A naive Governor that records every event synchronously will throttle the hub. The implementation needs to be honest about backpressure: probably async write-through with bounded buffers and a clear policy on what happens when the buffer fills.

## References

- [Inbox](inbox.md) — the queue the Governor records lifecycle events for.
- [Hub](hub.md) — what asks the Governor for policy decisions.
- [Principal](principal.md) — what every audit event is tagged with.
- OpenTelemetry, Serilog, and OTel-compatible backends — likely sink targets.
