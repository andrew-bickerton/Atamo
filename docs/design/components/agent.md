# Agent

An agent is a component playing the Agent role: pulling messages from its inbox, processing them, and producing zero or more response messages. Agents are the only place ATAMO touches the outside world (databases, HTTP, LLMs, mail servers, humans) on the hub's behalf.

## Contents

- [Overview](#overview)
- [The Agent contract](#the-agent-contract)
- [Roles, not types: the relationship with Source](#roles-not-types-the-relationship-with-source)
- [Lifecycle](#lifecycle)
- [Idempotency and at-least-once delivery](#idempotency-and-at-least-once-delivery)
- [Response messages](#response-messages)
- [Examples of agents](#examples-of-agents)
- [Open questions](#open-questions)
- [References](#references)

## Overview

Agents are reactive: they do not act unless there is work in their inbox. An agent's job is to pull a message, do something with it, optionally produce responses, and acknowledge the message. That cycle is uniform across every agent — an in-process LLM agent and a human-review agent obey the same contract; only the rate at which they consume their inbox differs.

This uniformity is what makes disconnection a difference of degree rather than kind. A human reviewer is just an agent whose inbox drains at human speed.

## The Agent contract

An agent's contract is small:

- **Pull.** Take the next message (or a batch, if the agent opts in) from its inbox. The message is leased — hidden from other consumers for a visibility timeout. Pull is the agent's choice; the hub does not push.
- **Process.** Do whatever the agent's purpose is. This is the only place where domain-specific logic lives.
- **Produce responses.** Optionally emit zero or more messages back into the hub, each tagged with the originating correlation ID.
- **Acknowledge.** Commit the message — ack on success, nack on failure. Ack removes from the inbox; nack returns for redelivery (subject to the inbox's retry limits and dead-letter policy).

That is the entire surface area of being an agent. There is no lifecycle method, no init/teardown protocol beyond what the host provides, no special "long-running" mode. The agent's job is to honour its inbox's pull/ack contract; everything else is the agent's own concern.

The contract is intentionally identical for in-process and remote agents. An agent on the other side of an HTTP connection still pulls, processes, optionally responds, and acks. The transport changes; the contract does not.

## Roles, not types: the relationship with Source

ATAMO has two roles: Source and Agent. They are deliberately separate, not unified into a single component abstraction.

A **Source** owns its own lifecycle and decides when to inject messages into the hub. A **Agent** reacts to its inbox. The two contracts have genuinely different shapes — a Source's main thing is its loop; an Agent's main thing is its pull/process/ack cycle — and forcing them into one interface would either bloat the Agent case or weaken the Source case.

Most components naturally play one role. The rare hybrid (an IMAP poller that also handles `ForcePoll` messages) is registered twice: once as a Source for its polling loop, once as an Agent for its inbox-driven behaviour. They may share implementation and state, but they have separate registrations and separate contracts.

The 2015 design's "long-running agent" subtype was an artefact of conflating these roles. With the split, the Agent contract becomes uniformly inbox-driven, and long-lived behaviour lives where it belongs — in Sources. Detail and rationale live in [ADR 0002](../../adr/0002-source-and-agent-as-roles.md).

See the [Source page](source.md) for the other side of this relationship.

## Lifecycle

An agent comes into existence when it is registered with the host and goes out of existence when the host shuts down or the agent is explicitly unregistered. Between those points, it is available to receive messages in its inbox.

When the host shuts down:

- In-flight leases are not forcibly revoked. The agent has whatever time remains on its lease to finish processing.
- Unprocessed messages remain in the inbox.
- On restart, the agent's inbox is restored as it was; processing resumes.

When an agent is explicitly unregistered:

- Its inbox is retained for a configurable grace period (default behaviour TBD; see open questions).
- During the grace period, queued messages can be reassigned, drained by a replacement agent, or dead-lettered.
- After the grace period, the inbox and any remaining messages are removed.

These behaviours are inbox-level concerns; see the [Inbox page](inbox.md) for the durable-queue side of the story.

## Idempotency and at-least-once delivery

The inbox contract guarantees at-least-once delivery, not exactly-once. This means an agent must either be **idempotent** (processing the same message twice produces the same result) or **tolerant of occasional redelivery** (it can detect and skip duplicates, or it can perform the same side-effect twice without harm).

This is the standard discipline for at-least-once messaging and is well-understood. ATAMO does not try to hide it. Agents that violate it (charging a credit card on every redelivery, sending an email twice) will malfunction in ways that ATAMO cannot prevent.

Common patterns to honour the discipline:

- **Idempotency keys.** Include a stable unique key in the message; agents track which keys they have already processed.
- **Conditional writes.** Use database constraints (`INSERT ... ON CONFLICT DO NOTHING`) to make duplicate side-effects benign.
- **Outbox pattern.** Persist the side-effect intent atomically with the ack; perform the actual side-effect in a separate, idempotent step.

The substrate does not enforce any of these. It honestly exposes the at-least-once guarantee and trusts agent authors to handle it.

**Dedupe is a consumer concern, not a substrate concern.** ATAMO does not put a `DedupeKey` field on the message contract. Dedupe semantics are agent-specific — a payment agent's notion of "same payment" differs from a content-store agent's notion of "same record" — and adding a single substrate-level field would imply a uniformity that does not hold. Agents that need dedupe carry their own keys in the payload and check them themselves. If multiple agents end up reimplementing the same wrapper, that pattern is a candidate for the community-contributed `Atamo.Agents.Common` package rather than for the core message contract.

## Response messages

An agent processing a message may produce zero or more **response messages**. These are normal messages — they re-enter the hub and are routed by the same rule engine that routed the original.

Two properties matter:

- **Responses carry the originating correlation ID.** This is what allows sources that subscribed to their request to receive the agent's responses, and what lets the Governor reconstruct the full causal chain.
- **Recursion protection.** A response message will not be enqueued into the inbox of the agent that produced it, even if a routing rule would otherwise match. This is what allows responses to be observable by other agents (logging, audit, reactive workflows) without producing infinite loops.

Responses can be produced incrementally. An agent that wants to stream partial results emits multiple response messages, each tagged with the same correlation ID.

**Ordering is per-producer, not global.** A single agent's responses to one correlation ID are delivered to subscribers in the order the agent emitted them. When multiple agents respond to the same correlation ID (fan-out), there is no ordering guarantee across agents — subscribers must treat the merged stream as unordered with respect to which agent produced each message. This matches what brokers reliably support and what the standalone-host streaming wire protocols (HTTP/2 + SSE, gRPC) can deliver across multiple producers. Consumers that need a globally ordered merged view can sequence by Governor timestamp at read time, but the substrate does not pre-merge for them.

## Examples of agents

A few illustrative examples of what fits the agent shape:

- **An LLM call wrapper.** Pulls a prompt message, calls a local or remote model, emits the completion as a response message. May stream tokens as multiple responses.
- **An email sender.** Pulls a `ReplyDraft` message, sends via SMTP, emits a `ReplySent` confirmation.
- **A database writer.** Pulls a record-to-persist message, writes to the database, emits an acknowledgement or error response.
- **A message-store agent.** Pulls every message it subscribes to, writes the content to a store it owns (SQLite, blob storage, a search index, whatever fits). May also handle query messages by reading from the same store and emitting historical messages as responses. This is how content storage is just-another-agent rather than special framework support.
- **An audit-fanout agent.** Subscribes to messages and forwards them to an external observability pipeline (a Kafka topic, an analytics warehouse, an OpenTelemetry collector configured for messages rather than just spans). Distinct from the Governor, which records metadata; this agent might forward content for downstream processing.
- **A human-review agent.** Pulls a flagged message; the inbox accumulates work over hours or days; a human eventually consumes the message via a UI, makes a decision, and the agent emits the decision as a response message. Structurally identical to the LLM agent — only the rate differs.

## Open questions

- **Registration shape for shared-implementation hybrids.** A class playing both Source and Agent roles needs two registrations. The fluent builder API needs to make this clean. Likely answer: separate `AddSource<T>` and `AddAgent<T>` calls, with DI ensuring the same instance is reused if `T` is registered as a singleton. Will be settled when the registration API lands in code.
- **Cancellation in the agent contract.** Should every agent's process step take a `CancellationToken`? Probably yes, but worth confirming when the contract is written.
- **Batch pull vs single pull.** Some agents (database writers, embeddings agents) benefit substantially from batching. Whether batch pull is opt-in or part of the default contract is undecided.
- **Backoff and retry policy ownership.** The inbox handles redelivery on nack. But some agents want richer backoff (exponential, jittered, capped). Whether this is a per-agent concern or a configurable inbox feature is undecided.

## References

- [ADR 0002 — Source and Agent as roles, not types](../../adr/0002-source-and-agent-as-roles.md)
- [Inbox](inbox.md) — the queue every agent pulls from.
- [Source](source.md) — the other role; the relationship between the two is the heart of the architecture.
- [Hub](hub.md) — what dispatches messages into agent inboxes.
- [Governor](governor.md) — what records every agent's lifecycle events.
