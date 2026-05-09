# Second sample: review form with history and live feed

The v1 sample is a small web UI that opens onto a window of recent ATAMO traffic and stays open as a live feed of new messages. It builds on the first sample's substrate and exercises a different set of properties — properties that, taken together, prove the Governor is genuinely a first-class queryable layer rather than just a write target.

## Contents

- [What the sample does](#what-the-sample-does)
- [Components added on top of the first sample](#components-added-on-top-of-the-first-sample)
- [Why this scenario](#why-this-scenario)
- [What the consumer-facing setup should feel like](#what-the-consumer-facing-setup-should-feel-like)
- [What is intentionally out of this sample](#what-is-intentionally-out-of-this-sample)
- [Definition of done](#definition-of-done)

## What the sample does

The user opens a web page and sees the last N messages that have flowed through the hub — colour-coded by type, sortable by time, filterable by principal or message type. The page does not need to refresh: as new messages arrive, they appear at the top. If the user closes the laptop and reopens an hour later, the feed resumes from where it left off without gaps or duplicates.

Behind the scenes, the page is a thin client over two substrate capabilities: a live metadata feed from the Governor (what's happening, in aggregate, right now) and a content query against the message-store agent (what did the messages actually contain). The first sample's components keep doing what they were already doing; this sample adds a Source for the web UI's queries and exercises the Governor's history-then-live capability for the metadata side.

## Components added on top of the first sample

This sample is purely additive to the first sample. The components that were already there — `InboxSource`, `TriageLlmAgent`, `OutboxAgent`, `MessageStoreAgent` — keep their existing roles and contracts. The additions are:

**Sources:**
- `WebQuerySource` — receives HTTP requests from the review form, injects them as messages: `MessageHistoryQuery` for content lookup (routed to the existing message-store agent), and `GovernorMetadataQuery` for the live metadata feed (routed to the substrate's Governor query surface).

The web UI itself is not part of ATAMO. It is a small separate project (Blazor, minimal API + HTML, or whatever fits) that talks to the standalone host. This is deliberate: keeping the UI out of the substrate proves the substrate is sufficient, and keeps the sample itself small.

## Why this scenario

The first sample proves messages can flow through ATAMO and be acted on. This sample proves messages can be _retrieved_ from ATAMO — both retrospectively and prospectively — through the same substrate that produced them. That is a structurally different demonstration, and it is the one that validates the Governor's value beyond audit-as-write-target.

Specifically, this scenario forces decisions about:

- **The Governor as queryable metadata, accessible to consumers.** The web UI subscribes to a live metadata feed from the Governor and gets history-then-live semantics for free. If the Governor's audit storage is awkward to query as a consumer, that's the abstraction failing. Content lookups go through the existing message-store agent — distinct concern, distinct path.
- **The history-then-live pattern as a first-class capability.** The architecture commits to this pattern being in the substrate; this sample is where the commitment is paid. The cutover between historical and live messages must be seamless, gap-free, and duplicate-free.
- **Resumability semantics.** A reconnect after disconnect with the last-seen marker is the canonical test. Three sub-questions get answered concretely: what shape the marker takes, what happens when the marker has aged out of storage, and how ordering across shards (if any) is exposed to the consumer.
- **Subscribe-to-many at the API level.** The review form wants "all messages, optionally filtered." Whether this is a special API or a generalisation of the existing routing-rule mechanism is a real choice. The cleanest answer is probably that subscription is just a long-lived rule with a streaming sink, but this needs proving.
- **Standalone host plumbing.** The web UI is the first consumer of the standalone host's API. If the standalone host needs special primitives that the embedded API does not have, the embedded API is incomplete. This sample exposes any such gap.
- **Resource shaping for long-lived subscribers.** A web UI is naturally chatty and easy to leave open. Backpressure, slow-consumer handling, and disconnect detection all surface here in ways the first sample never reached.

## What the consumer-facing setup should feel like

The exact API will settle in code, but a developer adding the review-form capability should produce something close to:

```csharp
hubBuilder
    .AddSource<WebQuerySource>("web-query", o => o.ListenOn("http://+:5050"))
    .AddRoutingRule(r => r.When<MessageHistoryQuery>().RouteTo("store"))
    .AddRoutingRule(r => r.When<GovernorMetadataQuery>().RouteToGovernor());
```

The two queries are deliberately separate. `MessageHistoryQuery` is content — "what did this email say?", "show me the body of triage decision #42" — and is handled by the existing message-store agent from v0. `GovernorMetadataQuery` is metadata — "how many messages of type X in the last hour?", "which agent handled correlation Y?", "stream me everything happening right now" — and is handled by the substrate itself, because the Governor is part of the substrate.

The Governor's metadata-query handling should let the consumer ask for a historical batch and then keep receiving live events as they happen, without distinguishing the cutover — that is the substrate's job. If the consumer has to manually reconcile "this is history" with "this is live," the abstraction has not earned its place. The right shape is probably an `IAsyncEnumerable<GovernorEvent>` that the substrate populates from the audit log and then transparently extends with live results, with a sequencing guarantee.

## What is intentionally out of this sample

- **No production-grade UI.** The page is functional, not polished. If it grows into a project of its own, the polished version is not part of this sample.
- **No authentication beyond development conveniences.** Multi-user review with proper auth is post-v1.
- **No write-back.** The review form is read-only. Acting on a reviewed message ("retry this," "cancel that," "rerun with different rules") is interesting and out of scope. The disconnected human-review case is the natural next sample.
- **No cross-shard global ordering guarantee.** If the persistence layer is partitioned, the resumability marker is per-shard, and the UI presents per-shard streams interleaved. Global ordering at scale is a v2 question.

## Definition of done

The v1 sample is done when:

- A developer running the v0 sample can add this one without modifying any v0 component.
- Opening the review form shows the last N messages within one second on a developer machine.
- New messages appear in the form within one second of being processed by the hub.
- Closing the form for an arbitrary period and reopening it resumes the feed cleanly: every message that flowed through during the gap is shown, in order, before live updates resume.
- The Governor's storage is the only persistence the sample touches. No parallel store, no special audit channel.
- The sample compiles against the public ATAMO API only, and is implementable equally well against the embedded library or the standalone host.
