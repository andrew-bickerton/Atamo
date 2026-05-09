# Guide 3: Add a history-and-live review form

Building on the triage application from [guide 2](02-add-llm-triage.md), add a small web UI that opens onto a window of recent ATAMO traffic and stays open as a live feed of new messages. The UI does not refresh on a timer; the substrate hands it history and live updates as a single stream.

## Contents

- [When you'd want this](#when-youd-want-this)
- [What you'll add](#what-youll-add)
- [The walkthrough](#the-walkthrough)
- [Why this approach](#why-this-approach)
- [What's not in this guide](#whats-not-in-this-guide)
- [Definition of done](#definition-of-done)
- [Next up](#next-up)
- [See also](#see-also)

## When you'd want this

You have a triage system running (per guides 1 and 2) and you need to see what is happening inside it — both retrospectively (what did the LLM do with this email yesterday?) and live (what is flowing through right now?). Operators, support staff, and you-during-debugging all want the same thing: a window onto the substrate's traffic without bolting on parallel logging.

ATAMO's Governor records every message, route decision, inbox event, and response. This guide makes that record consumable as a UI rather than a static log file.

## What you'll add

The components from guides 1 and 2 keep their existing roles and contracts. The additions are small:

**Sources:**

- A `WebQuerySource` that receives HTTP requests from the review form and injects them as messages: `MessageHistoryQuery` for content lookup (routed to the existing `MessageStoreAgent`), and `GovernorMetadataQuery` for the live metadata feed (handled by the substrate's Governor query surface).

The web UI itself is not part of ATAMO. It is a small separate project (Blazor, minimal API + HTML, or whatever fits) that talks to the standalone host. Keeping the UI out of the substrate proves the substrate is sufficient and keeps the guide small.

## The walkthrough

The user opens a web page and sees the last N messages that have flowed through the hub — colour-coded by type, sortable by time, filterable by principal or message type. The page does not need to refresh: as new messages arrive, they appear at the top. If the user closes the laptop and reopens an hour later, the feed resumes from where it left off without gaps or duplicates.

Behind the scenes, the page is a thin client over two substrate capabilities: a live metadata feed from the Governor (what's happening, in aggregate, right now) and a content query against the message-store agent (what did the messages actually contain). The components from guides 1 and 2 keep doing what they were already doing; this guide adds a Source for the web UI's queries and exercises the Governor's history-then-live capability for the metadata side.

A developer adding the review-form capability should produce something close to:

```csharp
hubBuilder
    .AddSource<WebQuerySource>("web-query", o => o.ListenOn("http://+:5050"))
    .AddRoutingRule(r => r.When<MessageHistoryQuery>().RouteTo("store"))
    .AddRoutingRule(r => r.When<GovernorMetadataQuery>().RouteToGovernor());
```

The two queries are deliberately separate. `MessageHistoryQuery` is content — "what did this email say?", "show me the body of triage decision #42" — and is handled by the message-store agent from guide 2. `GovernorMetadataQuery` is metadata — "how many messages of type X in the last hour?", "which agent handled correlation Y?", "stream me everything happening right now" — and is handled by the substrate itself, because the Governor is part of the substrate.

The Governor's metadata-query handling should let the consumer ask for a historical batch and then keep receiving live events as they happen, without distinguishing the cutover — that is the substrate's job. If the consumer has to manually reconcile "this is history" with "this is live," the abstraction has not earned its place. The right shape is probably an `IAsyncEnumerable<GovernorEvent>` that the substrate populates from the audit log and then transparently extends with live results, with a sequencing guarantee.

## Why this approach

The previous guides showed that messages can flow through ATAMO and be acted on. This guide shows that messages can be _retrieved_ from ATAMO — both retrospectively and prospectively — through the same substrate that produced them. That is a structurally different demonstration, and it is the one that validates the Governor's value beyond audit-as-write-target.

A few specific decisions get exercised:

- **The Governor as queryable metadata, accessible to consumers.** The web UI subscribes to a live metadata feed and gets history-then-live semantics for free. If the Governor's audit storage is awkward to query as a consumer, the abstraction is failing.
- **The history-then-live pattern as a first-class capability.** The cutover between historical and live messages must be seamless, gap-free, and duplicate-free.
- **Resumability semantics.** A reconnect after disconnect with the last-seen marker is the canonical test. Three sub-questions get answered: what shape the marker takes, what happens when the marker has aged out of storage, and how ordering across shards (if any) is exposed to the consumer.
- **Subscribe-to-many at the API level.** The review form wants "all messages, optionally filtered." Whether this is a special API or a generalisation of the existing routing-rule mechanism is a real choice — probably the latter, but this guide is where it gets settled.
- **Standalone host plumbing.** The web UI is the first consumer of the standalone host's API. If the standalone host needs special primitives the embedded API does not have, the embedded API is incomplete.
- **Resource shaping for long-lived subscribers.** A web UI is naturally chatty and easy to leave open. Backpressure, slow-consumer handling, and disconnect detection all surface here.

## What's not in this guide

- **No production-grade UI.** The page is functional, not polished. If it grows into a project of its own, the polished version is not part of this guide.
- **No authentication beyond development conveniences.** Multi-user review with proper auth is post-curriculum.
- **No write-back.** The review form is read-only. Acting on a reviewed message ("retry this," "cancel that," "rerun with different rules") is interesting and out of scope here. The disconnected human-review case is the next guide.
- **No cross-shard global ordering guarantee.** If the persistence layer is partitioned, the resumability marker is per-shard, and the UI presents per-shard streams interleaved. Global ordering at scale is a [guide 4](04-add-disconnected-human-review.md)-and-beyond question.

## Definition of done

This guide's pattern is working when:

- A developer running guides 1 and 2 can add this guide's content without modifying any prior component.
- Opening the review form shows the last N messages within one second on a developer machine.
- New messages appear in the form within one second of being processed by the hub.
- Closing the form for an arbitrary period and reopening it resumes the feed cleanly: every message that flowed through during the gap is shown, in order, before live updates resume.
- The Governor's storage is the only persistence the substrate side touches. No parallel store, no special audit channel.
- The application compiles against the public ATAMO API only, and is implementable equally well against the embedded library or the standalone host.

## Next up

[Guide 4 — Add a human reviewer to the loop](04-add-disconnected-human-review.md) adds a human-in-the-loop review step for emails the LLM flags as needing attention. The review UI from this guide grows write operations; the inbox model gets exercised at human time-scales.

## See also

- [Query previously persisted messages](../recipes/query-persisted-messages.md) — the content-query side in isolation.
- [Governor component](../design/components/governor.md) — the layer the metadata query targets.
- [Resumability open question](../design/open-questions.md#resumability-semantics-for-the-history-then-live-pattern) — the design decisions this guide forces.
- [Host: standalone host](../design/components/host.md#the-standalone-host) — what the web UI talks to.
