# 0003. Per-agent inboxes with industry-standard semantics

- **Status:** Accepted
- **Date:** 2026-04-28

## Contents

- [Context](#context)
- [Decision](#decision)
- [Alternatives considered](#alternatives-considered)
- [Consequences](#consequences)
- [References](#references)

## Context

Earlier design work treated agent dispatch as a function-call shape: the hub identifies the target agent, hands off the message, the agent processes it, optionally produces responses. This works for the common case of in-process, always-available agents, but it falls apart under three pressures:

- **Disconnected agents.** A human-review agent may not look at its work for hours or days. A batch-job agent may run only nightly. Holding messages in private agent storage during these gaps means each agent reimplements queue, persistence, timeout, and dead-letter logic — usually badly, and in a way that breaks ATAMO's audit guarantees if the agent process dies.
- **Restart and crash recovery.** Even an in-process agent may crash or be redeployed mid-message. Without explicit message-state durability, in-flight work is silently lost.
- **Backpressure.** A burst of messages targeting a slow agent has nowhere to go in the function-call model except an unbounded in-memory queue inside the agent itself.

The right place to solve all three is the substrate, not each agent. ATAMO holds the queue; agents pull from it at their own pace.

A second consideration: queues with these semantics are well-understood territory. RabbitMQ, NATS JetStream, Azure Service Bus, AWS SQS, and others have spent decades getting them right. ATAMO inventing its own bespoke queue model would conflict with the principle "compose, don't subsume" and would produce a model awkward to swap with industrial tooling later. The contract should align with what those brokers reliably provide.

This ADR resolves the implicit assumption in earlier design work that dispatch was a function-call shape, and replaces it with an explicit inbox model.

## Decision

Every agent has an **inbox**: a durable, per-agent queue managed by ATAMO. Dispatching a message to an agent means **enqueueing into that agent's inbox**. Agents **pull** from their inbox at their own pace.

The inbox is a swap point. The default implementation is a SQLite-backed durable queue suitable for embedded use. Consumers who outgrow it can swap in industrial brokers (RabbitMQ, NATS JetStream, Azure Service Bus, AWS SQS, Redis Streams) without API changes. The contract is deliberately scoped to what these brokers reliably support in common.

The inbox contract guarantees:

- **Durable enqueue and dequeue.** Messages survive process and broker restart.
- **Pull with lease.** Pulled messages are hidden from other consumers for a configurable visibility timeout. If not acknowledged within the timeout, the message returns to the queue.
- **Acknowledge and negative-acknowledge.** Explicit consumer commit. Ack removes; nack returns for redelivery, subject to retry limits.
- **Dead-letter on retry exhaustion or max age.** Configurable redelivery cap and message TTL; messages that exceed either move to a dead-letter queue rather than redelivering forever.
- **Max in-flight per agent.** Backpressure mechanism preventing overwhelm or hoarding.
- **At-least-once delivery.** Agents must be idempotent or tolerate occasional redelivery.
- **FIFO ordering within an inbox.** Per-inbox ordering, not global.

The contract explicitly does **not** include:

- **Exactly-once delivery.** Not universally supported by target brokers; promising it would be dishonest.
- **Cross-inbox transactions.** Most brokers don't support them.
- **Broker-specific routing topologies.** RabbitMQ exchanges and NATS subjects belong inside their respective adapters.

The vocabulary used in the contract — inbox, enqueue, lease, visibility timeout, ack, nack, dead-letter, max in-flight, at-least-once, FIFO — matches the established terminology of the major brokers. This is deliberate: developers familiar with broker semantics get the inbox model for free, and the swap-point story is self-explanatory.

The Governor records inbox lifecycle events (enqueue, lease, ack, nack, dead-letter) as part of the audit log. This means a query against the Governor can show not just "this message was dispatched" but "this message was enqueued at T, leased after 4 hours by agent instance X, nacked, redelivered, and finally acked by instance Y."

## Alternatives considered

**Function-call dispatch with agent-side queueing for disconnected cases.** Rejected because it forces every disconnected agent to reimplement persistence, leases, retries, and dead-lettering, and because agent-private storage breaks the audit guarantees ATAMO commits to. It also fragments error handling across agents instead of centralising it.

**Function-call dispatch with a shared queue library that agents opt into.** A softer version of the above — ATAMO ships a library that agents import for queueing. Rejected for the same reason: opt-in queueing means some agents will skip it, the audit and durability properties become per-agent rather than systemic, and the swap-point story is much weaker because consumers can't replace the queue uniformly.

**A bespoke ATAMO queue model not aligned to existing broker semantics.** Rejected because it would conflict with the "compose, don't subsume" principle, would produce a model awkward to swap with industrial tooling, and would require contributors to learn ATAMO-specific concepts where standard ones exist.

**Multiple inboxes per agent (e.g. priority, dead-letter, retry as separate addressable queues).** Considered and rejected for v0. A single conceptual inbox per agent with a separate dead-letter queue is simpler. Multiple addressable inboxes per agent can be reintroduced later if a real consumer requirement forces it; the cost of keeping it simple now is low.

## Consequences

**Easier:**

- Disconnected agents stop being a special case. Human-review agents, batch jobs, agents on flaky networks, and agents under maintenance all work uniformly under the same contract as in-process always-available agents — only the consumption rate differs.
- Crash recovery is automatic at the substrate level. A crashed or restarted agent picks up where it left off; in-flight messages whose leases expire are redelivered without explicit application-level handling.
- Backpressure is a substrate concern with a single mechanism (max in-flight per agent), not a per-agent reinvention.
- Swap-up to industrial brokers is a configuration change rather than an architectural migration. Consumers who outgrow the default SQLite inbox swap in RabbitMQ or SQS without touching agent code or routing rules.
- The standalone host falls out naturally: "agent on the other side of an HTTP connection" is just an agent pulling from its inbox over the network, not a structurally different case.
- Audit becomes richer. The Governor naturally records inbox lifecycle events, giving consumers visibility into queue depth, lease durations, redelivery counts, and dead-letter rates without bolting on broker-specific telemetry.

**Harder:**

- Even the simple in-process case now goes through an enqueue/pull cycle rather than a direct function call. The default implementation must keep this cheap (sub-millisecond for in-process agents) so the cost is invisible in the common case. This is achievable — SQLite with WAL mode handles tens of thousands of enqueue/pull cycles per second on a developer machine — but it is a real performance discipline to honour.
- Agents must be idempotent or tolerate occasional redelivery. This is the standard at-least-once contract, but it is a constraint agent authors must understand.
- The default inbox implementation is non-trivial. SQLite-backed durable queues with leases, redelivery, dead-letters, and policy enforcement are several hundred lines of careful code, not a weekend project. The investment is worthwhile because the abstraction is central, but it is real work.
- Inbox state must be coordinated with Governor audit such that they don't drift or duplicate. The architecture commits to them sharing storage where possible; the exact shape is an open question.

**New questions opened:**

- **Inbox policy scope for v0.** The full capability list is the long-term target; the v0 minimum is smaller. Probably visibility timeout, max retries, max age, and max in-flight are essential; priority, delayed delivery, and deduplication are post-v0. Recorded in [open-questions.md](../design/open-questions.md).
- **Inbox/Governor storage relationship.** Both are durable, both are queryable, they overlap. Single store with views, separate stores with unified query, or fully separate? Recorded in [open-questions.md](../design/open-questions.md).
- **Inbox addressing for hybrid components.** A component playing both Source and Agent roles has an inbox in its Agent capacity; this is uncomplicated. But components that play the Agent role multiple times (e.g. a single class registered as two different agents) have multiple inboxes. The registration API needs to make this clean.
- **Cancellation and shutdown semantics.** When the host shuts down, what happens to in-flight leases? When an agent is unregistered, what happens to queued messages? Probably leases expire normally and messages return to the queue; unregistered agents' inboxes are retained for a configurable grace period. Worth a dedicated ADR when the decision is forced.

## References

- [Architecture overview](../design/architecture.md) — the high-level treatment of inboxes.
- [Inbox component](../design/components/inbox.md) — the inbox contract and default implementation in detail.
- [Agent component](../design/components/agent.md) — the consumer side of the inbox contract.
- [Governor component](../design/components/governor.md) — the audit side of inbox lifecycle events.
- [Principles](../design/principles.md) — "compose, don't subsume" and "every agent contract assumes remote, even when in-process" both pull toward this decision.
- [Open questions](../design/open-questions.md) — records the new questions this ADR opens.
- [ADR 0002](0002-source-and-agent-as-roles.md) — defines the Agent role that this ADR equips with an inbox.
- AWS SQS, RabbitMQ, NATS JetStream, Azure Service Bus public documentation — the contract is the rough intersection of what these brokers provide.
