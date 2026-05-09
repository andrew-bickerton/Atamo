# Inbox

The inbox is the per-agent durable queue that holds messages dispatched to an agent until the agent consumes them. It is the abstraction that makes disconnected agents possible without making them a special case.

## Contents

- [Overview](#overview)
- [The shift from dispatch-as-call to dispatch-as-enqueue](#the-shift-from-dispatch-as-call-to-dispatch-as-enqueue)
- [Inbox contract](#inbox-contract)
- [Standard vocabulary](#standard-vocabulary)
- [The default implementation](#the-default-implementation)
- [Swapping in industrial brokers](#swapping-in-industrial-brokers)
- [Relationship with the Governor](#relationship-with-the-governor)
- [Open questions](#open-questions)
- [References](#references)

## Overview

Every agent has an inbox. When the hub dispatches a message to an agent, it enqueues the message into that agent's inbox. Agents pull from their inboxes at their own pace — microseconds for an in-process LLM agent, hours or days for a human-review agent.

The inbox is a [swap point](../architecture.md#swap-points). The default implementation is a SQLite-backed durable queue suitable for embedded use. Consumers who outgrow it can swap in industrial brokers without API changes, because the inbox contract is deliberately scoped to what those brokers reliably support in common.

## The shift from dispatch-as-call to dispatch-as-enqueue

Earlier design work treated agent dispatch as a function-call shape: the hub identifies the target agent, hands off the message, the agent processes it, optionally produces responses. This works for the common case of in-process always-available agents but falls apart under three pressures:

- **Disconnected agents.** A human reviewer may not look at work for hours. A batch-job agent runs nightly. Holding messages in private agent storage during these gaps means each agent reimplements queue, persistence, timeout, and dead-letter logic — usually badly, and in a way that breaks ATAMO's audit guarantees if the agent process dies.
- **Restart and crash recovery.** Even an in-process agent may crash or be redeployed mid-message. Without explicit message-state durability, in-flight work is silently lost.
- **Backpressure.** A burst of messages targeting a slow agent has nowhere to go in the function-call model except an unbounded in-memory queue inside the agent itself.

The right place to solve all three is the substrate. ATAMO holds the queue; agents pull from it at their own pace. This is recorded in [ADR 0003](../../adr/0003-per-agent-inboxes.md).

A useful framing: even an in-process agent has an inbox — it is just one that drains at machine speed. The contract is uniform; only the consumption rate differs.

## Inbox contract

The contract every inbox implementation must provide:

- **Durable enqueue and dequeue.** Messages survive process and broker restart.
- **Pull with lease.** When an agent pulls a message, it is hidden from other consumers for a configurable visibility timeout. If not acknowledged within the timeout, the message returns to the queue for redelivery.
- **Acknowledge and negative-acknowledge.** Explicit consumer commit. Ack removes the message from the inbox; nack returns it for redelivery, subject to retry limits.
- **Dead-letter on retry exhaustion or max age.** After a configurable number of redelivery attempts or after a configurable max age, messages move to a dead-letter queue rather than redelivering forever.
- **Max in-flight per agent.** Backpressure mechanism preventing a single agent from being overwhelmed or from hoarding messages it cannot process promptly.
- **At-least-once delivery.** The standard guarantee. Agents must be idempotent or tolerate occasional redelivery.
- **FIFO ordering within an inbox.** Messages are delivered in the order they were enqueued, with the standard caveat that ordering is per-inbox, not global across the system.

Capabilities deliberately _not_ in the contract:

- **Exactly-once delivery.** Famously hard and not universally supported by the brokers ATAMO targets. Promising it would be dishonest.
- **Cross-inbox transactions.** Most brokers don't have them; the ones that do have them with caveats.
- **Broker-specific routing topologies.** RabbitMQ exchanges and NATS subjects belong inside their respective adapters, not in the ATAMO contract.

Capabilities that may be added later as opt-in features, with the understanding that some adapters will fake them imperfectly:

- **Priority queues.**
- **Delayed / scheduled delivery.**
- **Message deduplication hints.**

These are not in v0 and should not be added until a real consumer requirement forces the question.

## Standard vocabulary

The terms used in the inbox contract — inbox, enqueue, lease, visibility timeout, ack, nack, dead-letter, max in-flight, at-least-once, FIFO — match the vocabulary established by the major message brokers. This is deliberate. .NET developers with broker experience already know these concepts; inventing ATAMO-specific names would obscure the swap-point story and force contributors to learn new words for old ideas.

When extending the contract, the same discipline should hold: prefer the term the brokers use, even if a more "ATAMO-ish" name is tempting.

## The default implementation

The default inbox is a SQLite-backed durable queue. The implementation is straightforward enough to be a few hundred lines, but the discipline is to keep it honest: if the contract above promises a behaviour, the SQLite implementation should provide it correctly, not approximate it.

Specifically, the default must keep the in-process case cheap. SQLite with WAL mode handles tens of thousands of enqueue/pull cycles per second on a developer machine, which means the inbox should be invisible in the simple case — an in-process LLM agent's inbox should drain in microseconds, not milliseconds, and the developer building such an agent should never need to think about the inbox unless they are doing something specifically inbox-shaped.

The default is not a contribution to the world of message brokers. It is a sensible starting point for consumers who haven't outgrown it, and one that makes the swap up to RabbitMQ or SQS feel like an upgrade rather than a migration.

## Swapping in industrial brokers

The expected swap targets are RabbitMQ, NATS JetStream, Azure Service Bus, AWS SQS, and Redis Streams. The inbox contract was deliberately scoped to the rough intersection of what these brokers reliably support, so the adapter for each should be a relatively thin translation layer.

Each adapter is responsible for:

- Mapping inbox enqueue to the broker's publish/send operation.
- Mapping pull-with-lease to the broker's receive-with-visibility-timeout.
- Mapping ack/nack to the broker's commit/abandon operations.
- Honouring max-in-flight via the broker's prefetch or equivalent setting.
- Routing dead-lettered messages to a broker-managed DLQ.

What an adapter must _not_ do:

- Expose broker-specific features (RabbitMQ exchanges, NATS subjects) through the ATAMO API. These belong inside the adapter, configured separately.
- Change the contract's guarantees. If the broker doesn't reliably support a contract feature (e.g. FIFO under all conditions), the adapter should document the gap rather than pretend.

A consumer's choice between brokers is then a normal infrastructure decision — operational familiarity, deployment shape, cost — rather than a coupling decision against ATAMO.

## Relationship with the Governor

Both the inbox and the Governor are durable, both are queryable, and they overlap. The cleanest design is for the Governor to record inbox lifecycle events (enqueue, lease, ack, nack, dead-letter) alongside dispatch and response events.

This means a query against the Governor's audit log naturally captures the full lifecycle of every message: not just "this message was dispatched" but "this message was enqueued at T, leased after 4 hours by agent instance X, nacked, redelivered, and finally acked by instance Y." That is a meaningful improvement over what most brokers expose, and it falls out of the architecture for free if the relationship is set up correctly.

Whether the Governor's audit storage and the inbox's queue storage share a physical store is an open question. The principle is that audit is queryable across both; the implementation can vary.

See the [Governor page](governor.md) for the audit side of this relationship.

## Open questions

- **Inbox policy scope for v0.** The full capability list is the long-term target; the v0 minimum is smaller. Probably visibility timeout, max retries, max age, and max in-flight are essential; priority, delayed delivery, and deduplication are post-v0.
- **Inbox/Governor storage relationship.** Single store with views, separate stores with unified query, or fully separate? Recorded in [open-questions.md](../open-questions.md).
- **Inbox addressing for hybrid components.** A component playing the Agent role multiple times (e.g. a single class registered as two different agents) has multiple inboxes. The registration API needs to make this clean.
- **Cancellation and shutdown semantics.** When the host shuts down, what happens to in-flight leases? When an agent is unregistered, what happens to queued messages? Probably leases expire normally and messages return to the queue; unregistered agents' inboxes are retained for a configurable grace period.

## References

- [ADR 0003 — Per-agent inboxes with industry-standard semantics](../../adr/0003-per-agent-inboxes.md)
- [Agent](agent.md) — the consumer side of the inbox contract.
- [Governor](governor.md) — the audit side of inbox lifecycle events.
- [Host](host.md) — how inboxes work across the embedded/standalone divide.
- AWS SQS, RabbitMQ, NATS JetStream, Azure Service Bus public documentation — the contract is the rough intersection of what these brokers provide.
