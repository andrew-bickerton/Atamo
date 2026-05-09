# 0002. Source and Agent as roles, not types

- **Status:** Accepted
- **Date:** 2026-04-28

## Contents

- [Context](#context)
- [Decision](#decision)
- [Alternatives considered](#alternatives-considered)
- [Consequences](#consequences)
- [References](#references)

## Context

ATAMO's components fall into two natural shapes:

- Components that **inject messages** into the hub on their own initiative — IMAP pollers, web request handlers, CLI commands, timers, other agents producing responses.
- Components that **react to dispatched messages** — LLM call wrappers, database writers, mail senders, audit loggers.

The 2015 design treated these as a single hierarchy with a "long-running agent" subtype for the first shape. This created friction:

- The Agent contract had to accommodate both reactive dispatch and self-driven loops, making the common case (reactive) ceremonious.
- "Long-running" agents were structurally different from request-driven agents but shared an interface, which obscured the actual contract each was meant to honour.
- Hybrid components — most realistically an IMAP poller that also handles `ForcePoll` messages — fit awkwardly into either pure model.

The first sample (email triage) made this concrete: trying to model the email integration as a single component with both inbound polling and outbound sending forced uncomfortable choices about lifecycle, registration, and contract.

This ADR resolves the "Lifecycle contract for long-running agents" entry that previously appeared in the design's open questions.

## Decision

Source and Agent are **roles**, not types. A component plays one or both roles, and is registered separately for each role it plays.

- A **Source** is a component playing the Source role. It owns its own lifecycle, decides when to inject messages into the hub, and may be long-lived for any reason (polling, watching, timing, etc.). The Source contract is about lifecycle and injection.
- An **Agent** is a component playing the Agent role. It pulls messages from its inbox, processes them, and produces zero or more response messages. The Agent contract is about pulling work and producing responses. Agents are uniformly reactive; they do not act unless there is work in their inbox.

A component that plays both roles is registered twice — once as a Source and once as an Agent. The two registrations may share implementation (a single class, shared configuration, shared state), but they have separate contracts and separate registration entries.

The Agent contract is therefore uniform and minimal: pull a message, process, optionally produce responses, ack. There is no "long-running agent" mode. Long-lived behaviour lives in Sources.

## Alternatives considered

**Single unified component interface with capability flags.** A component would declare which roles it plays and implement a combined interface. Rejected because it bloats the common case (most components play one role) to accommodate the rare hybrid, and because the two contracts have genuinely different shapes that resist unification without losing precision in one or both.

**Two separate hierarchies with no allowance for hybrids.** Rejected because hybrids are real and useful — the IMAP-with-force-poll case is a genuine pattern, not an exotic edge case. Forbidding it would push consumers to awkward workarounds (a separate "trigger" component that talks to the IMAP component out-of-band).

**Keep the 2015 "long-running agent" subtype.** Rejected because it conflates lifecycle with dispatch in the same interface, which is exactly the source of the friction this ADR exists to remove.

## Consequences

**Easier:**

- The Agent contract becomes uniformly inbox-driven. No optional loops, no lifecycle modes, no conditional behaviour based on whether the agent is "long-running."
- Hybrids are honest. A component playing both roles registers twice, making the two pieces of behaviour explicit and independently testable.
- The first sample's email integration is naturally two registrations (`InboxSource` + `OutboxAgent`) rather than one component with split personality.
- Mental model is cleaner for new contributors: "Sources push, Agents pull." No subtype to learn.

**Harder:**

- Registration ergonomics need to handle two registrations of the same class without ceremony. The fluent builder API needs to make `AddSource<T>(...)` and `AddAgent<T>(...)` feel natural even when `T` is the same type. This is a real API design constraint to honour.
- Components that play both roles need a discipline about shared state. State that the Source mutates and the Agent reads (or vice versa) must be thread-safe. This was always true; the new model makes it more explicit.

**New questions opened:**

- The exact shape of the registration API for shared-implementation hybrids. Likely answer: two separate `AddSource<T>` and `AddAgent<T>` calls, with DI ensuring the same instance is reused if `T` is registered as a singleton. This will be settled when the registration API lands in code.
- Whether the Source contract should support cancellation in a uniform way, or whether cancellation is a Source-specific concern. Probably uniform via `CancellationToken`, but worth confirming when the contract is written.

## References

- [Architecture overview](../design/architecture.md) — the high-level treatment of roles.
- [Source component](../design/components/source.md) — the Source role in detail.
- [Agent component](../design/components/agent.md) — the Agent role in detail, including the rationale for separation.
- [First sample](../design/first-sample.md) — the email triage scenario that forced this distinction concretely.
- [ADR 0001](0001-rename-2015-vocabulary.md) — the renaming that introduced _Source_ as a term in the first place.
