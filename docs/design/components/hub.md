# Hub

The hub is ATAMO's routing engine. It receives messages from sources, evaluates routing rules to decide which agents should see each message, enqueues messages into the relevant agent inboxes, and routes responses back through the same machinery.

## Contents

- [Overview](#overview)
- [Responsibilities](#responsibilities)
- [What the hub does not do](#what-the-hub-does-not-do)
- [Statefulness](#statefulness)
- [Recursion protection](#recursion-protection)
- [Open questions](#open-questions)
- [References](#references)

## Overview

The hub is the centre of ATAMO. Sources submit messages to it; rule providers tell it where to route them; agents pull from inboxes that it manages; the Governor records what it does. Everything else in the architecture exists to serve or surround the hub.

Despite being central, the hub is intentionally thin. Most of the logic that consumers care about — what messages mean, where they should go, what to do with them — lives in routing rule providers and agents. The hub itself is mostly orchestration: take a message, ask the rule providers, enqueue into target inboxes, watch for responses, audit through the Governor.

## Responsibilities

The hub is responsible for:

- **Receiving messages.** Sources call into the hub to submit messages. The hub validates the message (correlation ID present, principal known, type recognised) and accepts or rejects it.
- **Evaluating routing rules.** For each accepted message, the hub asks every registered routing rule provider to evaluate the message. Each provider returns zero or more (target agent, shaped message) pairs. The hub aggregates them.
- **Enqueueing into inboxes.** For each (agent, shaped message) pair, the hub enqueues the shaped message into that agent's inbox.
- **Tracking correlations.** The hub knows which sources are subscribed to which correlation IDs, so that when responses arrive it can deliver them to the right place.
- **Routing responses.** When agents produce response messages, the hub treats them as new messages and routes them through the same rule pipeline — applying recursion protection so an agent never receives its own output.
- **Reporting to the Governor.** Every receive, route decision, enqueue, and response is recorded.

These responsibilities are intentionally narrow. The hub does not interpret message content, does not retry, does not persist messages itself (the inbox does that), does not manage agent lifecycle (the host does that), does not authenticate (the host does that).

## What the hub does not do

A list of things explicitly _not_ the hub's job, because it is easy to drift into making them the hub's job:

- **Persistence.** The hub does not store messages. Inboxes do that for queued work; the Governor does that for audit; the persistence layer does that for deferred retrieval.
- **Retry.** The hub does not retry failed dispatches. The inbox handles redelivery on nack; the agent handles its own internal retries if it wants them.
- **Authentication.** Principals arrive on messages from sources and are taken at face value by the hub. Authenticating the source is the host's responsibility.
- **Authorisation.** Whether a given principal is allowed to invoke a given agent is a Governor concern, not a hub concern. The hub asks the Governor; the Governor decides.
- **Transformation.** The hub does not transform messages. Routing rule providers may emit shaped messages, but that is the rule provider's logic, not the hub's.
- **Scheduling.** The hub does not delay messages or schedule them for future delivery. If those features arrive, they live in the inbox layer.

Keeping the hub thin is the discipline that lets the surrounding components stay clean.

## Statefulness

The hub is **stateless about message content** (it doesn't care what's inside a message) but **stateful about in-flight correlations and subscriptions**.

The state the hub holds:

- **Subscription map.** Which sources are subscribed to which correlation IDs, and how to deliver responses back to them.
- **Active rule providers.** The set of registered routing rule providers, in registration order.
- **Active agent registry.** Which agents exist, what their inbox addresses are, what message types they handle.

This state is in-memory and rebuilds on host restart. Anything that needs to outlive a process restart belongs in the inbox or the Governor, not in the hub.

## Recursion protection

When a response message re-enters the hub, the routing rule providers may legitimately produce a rule that would route the response back to the agent that produced it. This must not happen, or the system loops.

The hub enforces recursion protection at the enqueue step: a message produced by agent X is never enqueued into agent X's inbox, even if a rule matches. This is independent of message content or correlation ID; it is purely a structural rule based on the produced-by relationship.

The protection is intentionally conservative. It does not detect longer cycles (X produces a message routed to Y, which produces a message routed back to X). Detecting those is much harder and produces more false positives than the simple direct-recursion case. If longer-cycle detection becomes necessary, it will be a Governor concern (audit-driven) rather than a hub concern.

## Open questions

- **Ordering of rule providers.** When multiple rule providers match the same message, in what order are their outputs combined? Probably registration order, with later providers able to amend earlier providers' outputs, but the exact mechanics are undecided.
- **Backpressure from inboxes.** When an agent's inbox hits its max-in-flight limit, what happens to additional messages targeting that agent? Block the source? Reject? Queue elsewhere? Probably reject with a typed error and let the source decide, but worth confirming.
- **Hub clustering.** A single hub is the v0 model. Multi-hub topologies (federation, sharding, regional hubs) are out of scope for now but worth noting as a future design space.

## References

- [Architecture overview](../architecture.md) — the hub's role in the larger picture.
- [Inbox](inbox.md) — where the hub enqueues messages.
- [Routing rule provider](routing.md) — what tells the hub where messages go.
- [Governor](governor.md) — what records what the hub does.
- [Source](source.md) — what submits messages to the hub.
- [Agent](agent.md) — what consumes the hub's output.
