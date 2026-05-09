# Third sample: human review with a disconnected agent

The v2 sample adds a human reviewer to the loop. When the triage LLM flags an email as needing human attention, the message lands in a human-review agent's inbox and waits there — possibly for hours or days — until a reviewer opens the review UI and processes it. The reviewer's decision flows back through the system as a normal message, picked up by the same downstream agents that handled the LLM's decisions in v0.

This is the sample that pays off the inbox model. The first two samples exercise the inbox at machine speed; this one exercises it at human speed and proves disconnection is a difference of degree, not kind.

## Contents

- [What the sample does](#what-the-sample-does)
- [Components added on top of the second sample](#components-added-on-top-of-the-second-sample)
- [Why this scenario](#why-this-scenario)
- [What the consumer-facing setup should feel like](#what-the-consumer-facing-setup-should-feel-like)
- [What is intentionally out of this sample](#what-is-intentionally-out-of-this-sample)
- [Definition of done](#definition-of-done)

## What the sample does

The triage LLM from v0 categorises each inbound email. When it produces a `TriageDecision` with `Action == NeedsHumanReview`, that message is routed to the human-review agent's inbox rather than to the outbound mail agent. The inbox accumulates flagged items.

A reviewer opens a web UI — extending the review form built in v1 — and sees the items waiting in their inbox. They pick one, read the original email and the LLM's analysis, and choose an action: approve the LLM's draft reply, edit it, write their own reply, or dismiss the email entirely. Their decision is submitted back through the substrate as a `ReviewDecision` message, which is routed to the outbound mail agent the same way an LLM-only reply would have been.

Run end-to-end: an email arrives, the LLM flags it for review, the reviewer eventually picks it up, the reviewer's decision flows back through the system, the reply is sent, and the full audit chain — LLM analysis, time spent in the human inbox, which reviewer acted, what they decided — sits queryable in the Governor.

## Components added on top of the second sample

This sample is additive to v0 and v1. Existing components keep their roles. The additions:

**Sources:**
- The existing `WebQuerySource` from v1 is extended (or paired with a new source) to accept `ClaimReviewItem` and `SubmitReviewDecision` messages from the review UI.

**Agents:**
- `HumanReviewAgent` — receives `TriageDecision` messages flagged for review. The agent does not process them autonomously; instead, it accumulates them in its inbox and exposes them to the review UI through a controlled pull/claim/ack lifecycle. When a reviewer claims an item, the agent leases it; when the reviewer submits a decision, the agent acks the original message and emits a `ReviewDecision` response.

**Routing rules added:**
- `TriageDecision` where `Action == NeedsHumanReview` → `HumanReviewAgent`'s inbox.
- `ReviewDecision` where `Action == SendReply` → `OutboxAgent` (reusing the existing v0 mail-out path).

A new routing rule replaces or refines the v0 rule that sent every `TriageDecision` straight to the outbound agent — only LLM decisions with `Action == AutoReply` go directly out now.

The reviewer is, conceptually, the agent. The `HumanReviewAgent` component is the bridge between the inbox and the human: it owns the inbox, mediates the claim/lease/ack protocol with the UI, and translates the reviewer's UI actions into substrate operations. From ATAMO's perspective, this is a single agent with a slow inbox; from the reviewer's perspective, it's a queue of work to get through.

## Why this scenario

The inbox model in [ADR 0003](../adr/0003-per-agent-inboxes.md) commits to disconnection being a difference of degree, not kind. v0 and v1 do not test that commitment — every agent in those samples drains its inbox in microseconds. A claim that "human reviewers are just agents with slow inboxes" needs at least one realistic disconnected agent to be more than rhetorical. v2 is that test.

Specifically, this scenario forces decisions about:

- **Lease durations measured in human time.** A reviewer might claim an item, then go to lunch, then come back. The visibility timeout on a human-review inbox is hours, not seconds. The substrate has to handle this without quietly redelivering items the reviewer is still working on.
- **Lease extension and explicit release.** A reviewer who is partway through a difficult item should be able to extend their lease ("I'm still working on this"). A reviewer who claimed an item by mistake should be able to release it back to the queue without forcing a timeout-driven redelivery. These operations are part of the inbox contract or they are not — this sample decides.
- **Multiple reviewers sharing one inbox.** A pool of reviewers all draining the same `HumanReviewAgent` inbox is the natural deployment. This is the standard competing-consumers pattern that every broker supports, but ATAMO's inbox contract has to express it cleanly.
- **Reviewer identity flowing through as principal.** The reviewer is not the same principal as the original email's sender. The `ReviewDecision` message is emitted on behalf of the reviewer, but its causal chain reaches back to the inbound email's principal. Two principals, one chain. The Governor needs to record both.
- **Escalation and dead-letter for ignored work.** Items that sit in the human inbox for too long without being claimed should escalate — to a manager, to a different reviewer pool, to dead-letter, or to an automatic fallback. The dead-letter machinery from v0 is exercised here in a way it never was when every message was processed in microseconds.
- **The review UI as both Source and Agent surface.** The UI from v1 was a pure consumer of substrate data (read-only review form). v2's UI submits decisions back, making it a source. The same web project plays both roles and the substrate accommodates that without ceremony.
- **Audit of human decisions as queryable history.** "Why did this email get sent?" needs to return "because reviewer X approved the LLM's draft on date Y after Z minutes of consideration" — and that has to fall out of the same Governor query infrastructure used in v1, not require new instrumentation.

## What the consumer-facing setup should feel like

A developer extending the v1 sample to v2 should add roughly:

```csharp
hubBuilder
    .AddAgent<HumanReviewAgent>("review", o => o
        .VisibilityTimeout(TimeSpan.FromHours(2))
        .MaxClaimAge(TimeSpan.FromHours(24))
        .DeadLetterOnExpiry())
    .AddRoutingRule(r => r
        .When<TriageDecision>(d => d.Action == TriageAction.NeedsHumanReview)
        .RouteTo("review"))
    .AddRoutingRule(r => r
        .When<ReviewDecision>(d => d.Action == ReviewAction.SendReply)
        .RouteTo("outbound"));
```

…and the existing v0 rule that routed every `TriageDecision` to `outbound` becomes:

```csharp
.AddRoutingRule(r => r
    .When<TriageDecision>(d => d.Action == TriageAction.AutoReply)
    .RouteTo("outbound"))
```

The review UI extends the v1 form with three operations: list items in my inbox, claim an item (acquire its lease), submit a decision (ack the original and emit a `ReviewDecision`).

The reviewer-facing UI is small but the substrate-facing operations on it are the interesting part. If implementing them feels natural — claim is an inbox lease, submit is an ack-with-response, list is the same history-then-live query from v1 filtered to one inbox — then the substrate has the right shape. If the UI has to fight the substrate or reach around it, the inbox contract has gaps.

## What is intentionally out of this sample

To keep the scope honest:

- **No production-grade reviewer authentication.** A reviewer logs in with whatever development convenience the v1 sample used; mapping that to a principal is enough. Real auth (SSO, MFA, fine-grained permissions) is post-v2.
- **No reviewer pools with sophisticated assignment.** Multiple reviewers can drain the same inbox (competing consumers), but no skills-based routing, no load balancing across reviewer pools, no "this item should go to a senior reviewer." Those are real features but a different sample.
- **No SLA dashboards or reviewer performance tracking.** The data is in the Governor and a future sample could surface it; this one does not.
- **No collaborative review.** One reviewer per item. No "second opinion" workflow, no escalation chains beyond the basic dead-letter case.
- **No mobile UI.** Reviewers use the same web UI as v1, on a desktop. A mobile-friendly review interface is a fine future project but out of scope here.
- **No editing the LLM's analysis itself.** Reviewers approve, modify, or replace the draft reply. They do not retrain or correct the LLM — that's a different problem space.

## Definition of done

The v2 sample is done when:

- A developer running v0 and v1 can add v2 by registering the `HumanReviewAgent` and adding two routing rules, without modifying any existing component.
- An email flagged by the LLM for review reliably appears in the reviewer's UI within seconds of arriving, regardless of how long it takes the reviewer to actually look at it.
- A reviewer can claim an item, walk away for an hour, come back, and still have the item leased to them. Lease extension and explicit release both work.
- An item that no reviewer claims within the configured max-age moves to dead-letter, and the dead-letter is observable through the Governor query interface from v1.
- Two reviewers running the UI simultaneously can drain the same inbox without claiming the same item — competing consumers behaves correctly.
- A query against the Governor for any reviewed email returns the full chain: arrival, LLM analysis, enqueue to review inbox, claim by reviewer X, decision, outbound send. With timestamps. From the same query API as v1.
- The human-review agent compiles against the public ATAMO API only. Whatever lease/claim/release operations the UI uses are part of the agent contract, not special framework hooks for "human" agents.

## What this sample tells us about the substrate

If the sample comes together cleanly, several things have been demonstrated:

- The inbox contract from [ADR 0003](../adr/0003-per-agent-inboxes.md) is honest. Disconnection at human time-scales works under the same contract that handles in-process agents.
- The Source/Agent role split from [ADR 0002](../adr/0002-source-and-agent-as-roles.md) accommodates a UI playing both roles without strain.
- The Governor's queryable storage covers a much wider range of "what happened?" questions than v0 needed it to.
- The principle that "the core knows nothing about its use cases" holds even for human-in-the-loop scenarios. ATAMO does not have a `HumanReviewAgent` type built in; the sample's agent is application code.

If the sample fights the substrate at any point — if the human-review agent needs to reach into hub internals, if the lease semantics don't quite work for hours-long claims, if the audit story has gaps that need bespoke instrumentation — those are signals that v0 or v1 settled on contracts that look right at machine speed but break down at human speed. Better to discover that here, in a contained sample, than later when consumers are building real products on the substrate.
