# Open questions

These are decisions that have been deliberately deferred. Each is recorded here so that it is visible, and so that when it _is_ decided, an ADR can be written against the framing here. New questions should be added as they arise; resolved questions should be moved into ADRs and removed from this list.

## Contents

- [How user-submitted code is isolated](#how-user-submitted-code-is-isolated)
- [Sandbox and UAT for user-submitted rules](#sandbox-and-uat-for-user-submitted-rules)
- [Credential vault strategy](#credential-vault-strategy)
- [Two-level cache implementation](#two-level-cache-implementation)
- [Governor storage and general persistence — same store?](#governor-storage-and-general-persistence--same-store)
- [In-flight tracking query interface](#in-flight-tracking-query-interface)
- [Deferred-retrieval keep-alive and TTL](#deferred-retrieval-keep-alive-and-ttl)
- [Resumability semantics for the history-then-live pattern](#resumability-semantics-for-the-history-then-live-pattern)
- [Inbox policy scope for v0](#inbox-policy-scope-for-v0)
- [Inbox cancellation and shutdown semantics](#inbox-cancellation-and-shutdown-semantics)
- [Lease extension and explicit release](#lease-extension-and-explicit-release)
- [Redaction in content-storing agents](#redaction-in-content-storing-agents)
- [Wire protocol for the standalone host](#wire-protocol-for-the-standalone-host)
- [Persistence layer choice](#persistence-layer-choice)
- [How ATAMO interacts with existing buses when one is present](#how-atamo-interacts-with-existing-buses-when-one-is-present)

## How user-submitted code is isolated

The 2015 design assumed user-submitted DLLs would be loaded into the hub process. In 2026 this is operationally painful and a security hazard: a faulty or malicious tenant's code can crash, leak, or compromise the host.

Candidate answers, roughly in increasing order of isolation and cost:

- `AssemblyLoadContext` per tenant, with collectibility. Cheap, but does not provide real isolation — shared GC, no resource limits, full .NET API surface.
- Out-of-process workers communicating with the hub over the standalone interface. Strong isolation, requires deployment story, modest per-message overhead.
- WASM components via Extism or `wasmtime-dotnet`. Strong isolation, language-agnostic, immature in .NET tooling, restricted API surface.
- Container-per-tenant. Strongest isolation, heaviest deployment cost, most operational burden.

The likely answer is _multiple_ of these, exposed through the agent-runtime swap point, with the default being the simplest option that is honest about its limits.

The decision is not blocking v0 because v0 has no user-submitted code. It becomes blocking when the multi-tenant story is built out.

## Sandbox and UAT for user-submitted rules

Tenants submitting their own routing rules need a way to test them before they affect production traffic. The shape this should take is undecided.

Candidates:

- **Shadow execution.** New rules run alongside production rules in dry-run mode, logging what they _would_ have done without dispatching anywhere. Diff the shadow output against production. Closest to Temporal's replay-testing model.
- **Replay against historical traffic.** A captured slice of message traffic is replayed through a candidate rule set; outputs are inspected. Requires the Governor to retain enough detail to replay.
- **Dedicated sandbox hub.** A separate ATAMO instance with synthetic or copied configuration, used for tenant-side experimentation before promotion.

These are not exclusive. Shadow execution is probably the highest-leverage and the one most likely to be in v1; the others may follow.

## Credential vault strategy

Per-principal credentials are central to the design. Building a credential vault from scratch is unwise: it is a small thing to get partially right and a large thing to get fully right.

Candidates:

- Provide a minimal in-process default with a clear "not for production" warning, and rely on consumers to plug in Azure Key Vault, AWS Secrets Manager, or HashiCorp Vault.
- Integrate with a delegated-identity service like Nango or Composio, which already solve per-user OAuth flows for a long list of providers.
- Build something ATAMO-specific only if the above are demonstrably insufficient.

The principle "compose, don't subsume" pulls hard toward integration rather than original implementation. The decision will be revisited when a non-trivial agent that needs delegated user credentials is being built.

## Two-level cache implementation

The 2015 design called for two caches: one at the message-source layer for disconnected clients, one inside the hub for in-flight request coalescing.

The coalescing cache is straightforward _to implement_: if two requests with the same key arrive while one is in flight, the second waits on the first. Standard pattern, on top of `ConcurrentDictionary` and `TaskCompletionSource`. The harder question is _when_ it lands. Neither v0 (single-LLM-call email triage) nor v1 (history-and-live review form) exercises it. The current bias is to defer until a real consumer requirement forces the question, in line with the "build one delightful path before generalising" principle. If it ships earlier as a "free" feature it almost certainly ships in the wrong shape. To be revisited when a third sample or a real consumer naturally produces duplicate in-flight requests for the same data.

The disconnected-client cache is harder because it needs to outlive process restarts and have a configurable keep-alive. This overlaps significantly with the persistence swap point and may be subsumed by it — see [Deferred-retrieval keep-alive and TTL](#deferred-retrieval-keep-alive-and-ttl) for the related TTL question. The decision is whether it is a separate concern or an aspect of persistence.

## Governor storage and general persistence — same store?

The architecture commits to the principle that Governor storage should be queryable. It does not commit to whether the audit log, deferred-retrieval results, the inbox queue, and the disconnected-client cache all live in the same physical store.

Candidates:

- **Single store with views.** One database; logical separation by table or schema; one persistence implementation to swap. Simplest operationally, easiest to query across.
- **Separate stores behind a unified query interface.** Audit might be append-only on commodity storage; inbox queues might want different durability characteristics; deferred results might want different retention policies. Unified query layer in front.
- **Separate concerns entirely.** Each subsystem chooses its own store; query agents read from each separately. Most flexible, most complex.

The first sample's message-store agent owning its own store (separate from the Governor's audit storage) settles a small version of this question (content storage is a consumer concern, queryable by the same agent that wrote it). The full answer for the substrate's own storage — Governor audit, inbox state, deferred-retrieval results — waits until deferred retrieval (scenario 3) and the inbox swap-point are both built out.

## In-flight tracking query interface

The Governor records every lifecycle event, so "which messages are currently in flight" is derivable from the audit log. The question is the _shape_ of that capability — what the operator-facing query interface looks like.

Candidates:

- **Fold the event stream on demand.** Every query reads the relevant slice of the audit log and computes current state. Simplest; no extra storage. Slow at scale; pushes the work to every consumer of the question.
- **Maintain a denormalised "current state" view.** A separate table or projection updated as events arrive, indexed by principal, agent, message type, and stage. Fast to query; couples the Governor to a specific schema that has to evolve as the lifecycle grows. The standard read-model-over-event-log pattern.
- **Expose only a streaming subscription.** Operators subscribe to live events and maintain their own view if they need one. Pushes the choice of materialisation to the consumer.

Likely answer is "denormalised view, regenerable from the event log if it gets out of sync." But the contract — query parameters, indexed dimensions, freshness guarantees, what counts as "in flight" — needs to be settled deliberately. The 2015 design named this as a controller-facing capability ("maintain list of inflight messages") without specifying the interface; getting the interface right is what makes this a load-bearing feature rather than a nice-to-have.

To be settled when the Governor's first concrete query surface lands, alongside or shortly after the v0 sample.

## Deferred-retrieval keep-alive and TTL

Vision scenario 3 (request with deferred retrieval) commits to "configurable keep-alive before they are evicted" but does not specify the policy. Several sub-questions need explicit answers:

- **Default TTL.** What is the default keep-alive for a deferred-retrieval result? A few minutes? An hour? Until explicitly retrieved? The 2015 design called this out as configurable but did not propose a default.
- **Granularity.** Is TTL set per-request by the source, per-message-type by configuration, globally by the host, or some combination?
- **Behaviour on retrieval-after-eviction.** A client returns with a correlation ID whose results have aged out. Is this an error, a "results unavailable" sentinel, or "fall back to live wait if the request can be replayed"?
- **Relationship with audit retention.** The Governor retains audit events for some period; the deferred-retrieval result store retains payloads for some period. These are different things with different lifetimes — the audit event for "this request was issued" can outlive the payload by orders of magnitude. Need to make the distinction explicit so consumers don't conflate them.
- **Storage location.** Does the deferred-retrieval cache share storage with the inbox? With the Governor? With application-level persistence? Overlaps with the [Governor storage](#governor-storage-and-general-persistence--same-store) question.

To be settled when scenario 3 (deferred retrieval) is built out — likely the third sample.

## Resumability semantics for the history-then-live pattern

The architecture commits to clean resumability — a subscriber can disconnect and reconnect with their last marker. The exact semantics are undecided.

Open sub-questions:

- **Marker form.** Timestamps are intuitive but ambiguous under clock skew and high write rates. Sequence numbers are unambiguous but require a single source of truth for sequencing. Hybrid (sequence-within-shard) adds complexity. Likely answer: monotonic sequence number, exposed alongside the message metadata.
- **Retention guarantees.** A subscriber that has been disconnected for a week may reconnect with a marker that has aged out of storage. The contract for what happens then — error, fallback to oldest available, fallback to "now" — needs to be explicit and configurable.
- **Ordering across shards.** If messages are partitioned (per-principal, per-type, per-tenant), is global ordering guaranteed, or is the consumer's marker per-shard? Probably per-shard, but this needs to be explicit.

The second sample is the forcing function for getting these right.

## Inbox policy scope for v0

The full inbox capability list (visibility timeout, max retries, max age, max in-flight, priority, delayed delivery, deduplication) is the long-term target. The v0 minimum is smaller.

Likely v0 minimum: visibility timeout, max retries, max age, max in-flight. These are essential for the inbox model to be honest about its guarantees.

Likely post-v0: priority queues, delayed/scheduled delivery, deduplication hints. These should not be added until a real consumer requirement forces the question, because adding them prematurely complicates every adapter without proportional benefit.

To be confirmed when the inbox implementation lands and the full set of required behaviours is concrete.

## Inbox cancellation and shutdown semantics

When the host shuts down, what happens to in-flight leases? When an agent is unregistered, what happens to queued messages?

Candidate answer: leases expire normally (the visibility timeout takes care of it; nothing forcibly revokes them), and unregistered agents' inboxes are retained for a configurable grace period during which messages can be reassigned, drained, or dead-lettered.

Worth a dedicated ADR when the inbox implementation lands.

## Lease extension and explicit release

The v2 sample (human review) surfaces two operations that the v0 inbox contract does not yet name:

- **Lease extension.** A consumer that is partway through a long-running item should be able to extend its lease ("I'm still working on this") rather than letting the visibility timeout fire and the message redeliver. Every major broker supports this; ATAMO's contract should too.
- **Explicit release.** A consumer that has claimed an item but cannot complete it (mistaken claim, end-of-shift, deferring to a colleague) should be able to release it back to the queue immediately rather than waiting for the timeout.

Likely answer: both are first-class operations on the inbox contract, exposed as `ExtendLease` and `Release` alongside `Ack` and `Nack`. Worth confirming when the v2 sample forces the question concretely.

## Redaction in content-storing agents

The principle "the substrate stores metadata; consumers store content" pushes content storage out of the core and into agents that consumers write. This is the right shape, but it surfaces a question the substrate should have a story for: when content contains sensitive material (credentials, PII, regulated data), whose responsibility is redaction, and what does ATAMO offer to make doing it correctly easy?

Three plausible loci of responsibility, not mutually exclusive:

- **The source decides at injection time.** Messages carry hints about which fields are sensitive; downstream content-storing agents respect those hints when deciding what to persist. Pushes the decision close to where the data originates and the principal is known.
- **The routing rule provider decides per-route.** A rule says "messages of this type going to this agent should have these fields redacted." Pushes the decision into the configuration layer.
- **The content-storing agent decides on its own.** Each agent's redaction policy is part of its own configuration; the substrate stays out of it. Pushes the decision closest to where the data lands.

The substrate likely needs a small contract — perhaps a way for messages to carry redaction hints, perhaps nothing at all — but it should not implement redaction itself. Worth revisiting when a real consumer use case forces the question.

## Wire protocol for the standalone host

The standalone host needs a wire protocol. HTTP, gRPC, and both are all viable.

- HTTP is universal, well-understood, and gives ATAMO a low-friction integration story for non-.NET clients.
- gRPC has natural support for streaming, which suits the response-stream and history-then-live scenarios.
- Both, if the standalone host is genuinely a thin adapter over the embedded API, may be inexpensive enough to support together.

The decision is mostly about the streaming-response and history-then-live cases. If HTTP/2 + Server-Sent Events is good enough for the first standalone milestone, gRPC can wait.

## Persistence layer choice

When ATAMO needs to outlast a process — for deferred retrieval, audit retention, inbox durability — a persistence layer is required. The choice of default matters because it shapes the dependency footprint.

Candidates:

- SQLite as the default, with a clear path to Postgres or SQL Server.
- Marten on Postgres as the default, since the audience is .NET-shaped and Marten is excellent.
- No default; require consumers to choose explicitly.

SQLite is currently leading on grounds of "works without infrastructure," but Marten's event-sourcing fit with the message-history model is appealing and worth a serious look.

## How ATAMO interacts with existing buses when one is present

If a consumer brings Wolverine or MassTransit, ATAMO should compose with it cleanly rather than fighting it. The shape of that integration is undecided. Candidates: ATAMO uses the bus as its transport swap-out; ATAMO sits in front of the bus and delegates dispatch; ATAMO sits behind the bus as one consumer among many.

Different consumers will want different shapes. The integration should probably support more than one mode, with the default being the simplest (transport swap-out).
