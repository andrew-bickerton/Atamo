# Source

A source is a component playing the Source role: injecting messages into the hub from outside on its own initiative. Sources own their own lifecycle and decide when to act.

## Contents

- [Overview](#overview)
- [The Source contract](#the-source-contract)
- [Relationship with Agent](#relationship-with-agent)
- [Lifecycle](#lifecycle)
- [Examples of sources](#examples-of-sources)
- [Open questions](#open-questions)
- [References](#references)

## Overview

Sources are where messages enter ATAMO. Anything that produces a message and submits it to the hub on its own initiative is a source — a web request handler responding to an HTTP call, an IMAP poller checking a mailbox, a CLI command invoked by a user, a timer firing on a schedule, an external webhook receiver.

Sources differ from Agents in one important way: a source decides _when_ to inject a message. An agent waits for messages to arrive in its inbox. The two contracts are different shapes for that reason; see [Agent](agent.md) for the inbox-driven side of the story.

## The Source contract

A source's contract is small and is mostly about lifecycle:

- **Start.** When the host starts, the source starts. This is when the source establishes whatever external connections, timers, or listeners it needs.
- **Run.** The source operates on its own, however it sees fit. This might be a polling loop, an event-driven listener, a long-poll subscription, or a passive component that waits to be called from elsewhere in the application.
- **Inject.** When the source has a message to submit, it constructs the message (with a principal and message type) and submits it to the hub. This is the only interaction with ATAMO.
- **Stop.** When the host shuts down, the source is given a chance to clean up — close connections, persist state, drain in-flight injections.

Sources do not have inboxes. They are not dispatched to. They produce messages; they do not consume them.

A source that _also_ needs to consume messages (an "IMAP poller that handles `ForcePoll` messages") plays both roles, registered separately as a Source and as an Agent. The two registrations may share implementation and state, but their contracts are independent.

## Relationship with Agent

ATAMO has two roles: Source and Agent. They are deliberately separate, not unified.

- **Source = the inbound edge of the hub.** Sources push messages in.
- **Agent = the outbound edge of the hub.** Agents pull messages out of inboxes.

A component plays one role, both, or is registered multiple times under different roles. Most components naturally play one role. Hybrids exist but are uncommon, and they are honest as two registrations.

The 2015 design's "long-running agent" subtype was an artefact of conflating these roles. The Source/Agent split removes that artefact: long-lived behaviour is what Sources do, and the Agent contract is uniformly inbox-driven.

The full rationale lives on the [Agent page](agent.md#roles-not-types-the-relationship-with-source). The decision is recorded in [ADR 0002](../../adr/0002-source-and-agent-as-roles.md).

## Lifecycle

A source is started when the host starts and stopped when the host stops. Cancellation is propagated via `CancellationToken` from the host's shutdown sequence.

A few subtle properties:

- **Sources are responsible for their own concurrency.** A source that polls in a loop owns that loop. The substrate does not impose a threading model.
- **In-flight injections during shutdown.** When the host begins shutting down, sources are signalled to stop. Messages already submitted to the hub are processed normally; messages the source had not yet submitted are simply not submitted. The source is responsible for draining cleanly if it cares about not losing in-flight work.
- **Restart.** A source that needs to resume from where it left off (e.g. an IMAP poller tracking the last seen UID) is responsible for persisting its own progress. The substrate does not provide source-side state persistence.

The asymmetry with agents is intentional: agents are equipped with durable inboxes because their inputs come from ATAMO; sources receive their inputs from elsewhere and must manage their own state for that.

## Examples of sources

- **A web request handler.** An ASP.NET Core endpoint receives a request, constructs a message, submits it to the hub, and (optionally) waits for responses by subscribing to the correlation ID.
- **An IMAP poller.** Connects to a mail server, polls for new messages, and injects each new email as an `InboundEmail` message.
- **A CLI command.** A user runs `atamo send ...`, the command constructs a message and submits it.
- **A timer.** Fires every N seconds; injects a `Tick` message that other agents react to.
- **A webhook receiver.** Listens on an HTTP endpoint; on a webhook call, injects the payload as a message.
- **An agent's response.** Strictly speaking, when an agent produces a response message, the agent is acting in the role of a source for that injection. In practice the substrate handles this internally and developers do not write "agent acting as source" code; the agent's response API does the right thing.

## Open questions

- **Registration shape for shared-implementation hybrids.** A class playing both Source and Agent roles needs two registrations. The fluent builder API needs to make this clean.
- **Source-side observability.** Should sources have their own telemetry surface (injection rate, error count) recorded by the Governor? Probably yes, but the shape is undecided.
- **Source rate limiting.** Some sources (high-volume webhook receivers, busy pollers) should respect global rate limits set by the Governor. Whether this is a substrate concern or per-source is undecided.

## References

- [ADR 0002 — Source and Agent as roles, not types](../../adr/0002-source-and-agent-as-roles.md)
- [Agent](agent.md) — the other role, where the relationship is most fully discussed.
- [Hub](hub.md) — what sources submit messages to.
- [Principal](principal.md) — every message a source submits is on behalf of a principal.
