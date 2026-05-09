# Critique

This document is a register of concerns about the ATAMO design and the project around it. It exists to keep failure modes visible while the design is still cheap to change, and to prevent the polish of the existing design corpus from masking the strategic risks underneath.

Each concern below names what is at risk, why it matters, what would resolve or mitigate it, and a current status. Items are not ranked by severity within a section, but the sections themselves are ordered roughly by how likely the concern is to kill the project.

This document is mutable. Items get added when concerns surface, edited when our understanding of them sharpens, and resolved (with a link to the ADR or change that closed them) when they are addressed. Resolved items stay in the document with a struck-through status so the history of how we got here is preserved.

## Contents

- [How to use this document](#how-to-use-this-document)
- [Existential concerns](#existential-concerns)
  - [No 2015 post-mortem](#no-2015-post-mortem)
  - [No real first consumer](#no-real-first-consumer)
  - [Solo implementation effort vs. scope](#solo-implementation-effort-vs-scope)
  - [No sustainability plan](#no-sustainability-plan)
- [Strategic concerns](#strategic-concerns)
  - [Differentiator is thinner than the docs imply](#differentiator-is-thinner-than-the-docs-imply)
  - [Unacknowledged overlap with the actor model](#unacknowledged-overlap-with-the-actor-model)
  - ["Compose, don't subsume" eats the surface area](#compose-dont-subsume-eats-the-surface-area)
  - [Core-knows-nothing principle vs. the value pitch](#core-knows-nothing-principle-vs-the-value-pitch)
- [Design-stage concerns](#design-stage-concerns)
  - [In-process performance claim is unverified](#in-process-performance-claim-is-unverified)
  - [Premature contract specification across v0/v1/v2](#premature-contract-specification-across-v0v1v2)
  - [v0 sample scope is too large](#v0-sample-scope-is-too-large)
  - [Audit schema lock-in risk](#audit-schema-lock-in-risk)
  - [Multi-tenancy scope creep](#multi-tenancy-scope-creep)
- [Cosmetic concerns](#cosmetic-concerns)
  - [The name](#the-name)
- [Summary](#summary)

## How to use this document

When a design or implementation decision is being made, check whether it touches one of the concerns below. If it does, the concern's "What would resolve it" section is a checklist of what the decision needs to do to count as progress. If a decision actively makes a concern worse, that is a signal to push back on the decision.

When a concern is resolved, mark its status as resolved with a link to the ADR or change that closed it, and leave the body intact for historical reference.

When a concern turns out to have been wrong — the risk did not materialise, or further information showed it was overstated — mark its status as withdrawn, with a note explaining what changed. Do not delete it.

## Existential concerns

These are the concerns most likely to end the project. They should be the highest-priority items to address.

### No 2015 post-mortem

**Status:** Open.

**Concern.** This is a 2026 reboot of an effort that started in 2015 and did not ship. The current design corpus does not anywhere explain why the original effort stopped. Without that explanation, the most likely outcome is that the same failure mode recurs — whether that was scope, lack of a forcing user, life events, an abstraction that did not survive contact with code, or something else.

**Why it matters.** Every other concern in this document compounds with this one. If the 2015 effort died because of scope, the current design is *larger* than the 2015 design. If it died from lack of a first consumer, the situation has not changed. If it died from a specific abstraction failure, that abstraction may still be in the 2026 design under a different name.

**What would resolve it.** A short post-mortem document — `docs/design/2015-post-mortem.md` or similar — written from memory, notes, or surviving artefacts, naming the proximate cause(s) of the 2015 effort stopping, and saying which 2026 design choices are responses to which 2015 failures. A post-mortem of two paragraphs is infinitely more useful than no post-mortem at all.

### No real first consumer

**Status:** Open.

**Concern.** All three samples in `docs/design/` are hypothetical. Nothing in the repository is a real product that needs ATAMO and would be built (or rebuilt) on top of it. Substrate libraries without a first-party consumer reliably ship the wrong abstractions, because nothing forces the design to confront real constraints that the author did not imagine.

The principle "build one delightful path before generalising" addresses this for *use cases* but not for *consumers*. The samples are intended to be that one delightful path, but a sample written to validate a substrate is not the same as an application that *needs* the substrate and would be measurably worse without it.

**Why it matters.** This is the failure mode the 2015 effort most likely hit, and the design corpus does not contain a defence against it recurring. Without a forcing user, every open question gets resolved with an answer that is reasonable in the abstract but may not survive a real workload.

**What would resolve it.** Identify a real product — the author's, an employer's, or a collaborator's — that has the shape ATAMO is meant to serve, and that would credibly be built on ATAMO if ATAMO existed. Use that product to drive every contract decision from v0 onward. The samples can remain as they are; they are useful pedagogy. But the *forcing function* needs to be a real product.

If no such product exists or can be found, the honest answer is to scope this as a learning project rather than a substrate intended for adoption.

### Solo implementation effort vs. scope

**Status:** Open.

**Concern.** A correct implementation of the design as written includes:

- A SQLite-backed durable queue with leases, dead-lettering, retry caps, max-in-flight, max-age policies (ADR 0003).
- A Governor schema for lifecycle events, causal chains, history-then-live with resumable markers, and queryable retention.
- Per-principal credential threading with at least a development-grade vault and the swap point for production vaults.
- Routing rule evaluation with template substitution and a pluggable provider interface.
- A fluent registration API spanning sources, agents, routing rules, governor sinks, principals, and policy.
- Two host shapes — embedded and standalone — over HTTP and/or gRPC.
- Three end-to-end samples (email triage, history-and-live UI, human review with claim/release/extend).
- Tests at unit, integration, and end-to-end scale.

For a strong solo .NET developer, this is a 9-18 month effort sustained at substantial weekly hours. Library projects of this scope routinely stop at 50-70% complete, often after the design phase has consumed more energy than expected.

**Why it matters.** Even if every other concern in this document is addressed, the project ships nothing if the implementation does not finish.

**What would resolve it.** One or more of:

- A drastically reduced v0 (see [v0 sample scope](#v0-sample-scope-is-too-large) below).
- Co-implementers — at least one other contributor with sustained capacity.
- An employer or sponsor whose paid time can go into ATAMO.
- A re-scoping of ATAMO to a deliberately smaller library (for example: routing + audit only, with inbox and persistence delegated entirely to existing tools).

The decision does not have to be made now, but it does have to be made before the first significant code lands. Re-scoping after 5,000 lines of code is much harder than re-scoping at 0.

### No sustainability plan

**Status:** Open.

**Concern.** The library exists in an ecosystem where the comparable projects all have institutional support: Wolverine has Jeremy Miller's company; Dapr has Microsoft; Orleans has Microsoft; MassTransit has a commercial entity behind it. ATAMO has none, and the design docs do not address how the project survives life events for the maintainer (job changes, illness, burnout, loss of interest).

**Why it matters.** Open-source libraries without commercial backing tend to drift when their maintainer's life gets busy, and the drift is invisible until users notice that issues are not being responded to. For a library that markets itself on durability and audit guarantees, a stalled maintenance posture is a credibility problem in addition to a practical one.

**What would resolve it.** A deliberate decision about what ATAMO is *for*, with consequences for how it is supported:

- If it is a hobby / portfolio project, the README should say so, and the audience should be other developers learning from the design — not production users.
- If it is intended for adoption, a sustainability path needs to exist: an employer's blessing, a sponsor, a co-maintainer with redundant authority, or a clear handoff plan.

This concern can be parked until the project gets closer to first release, but it should not be ignored.

## Strategic concerns

These concerns are about how ATAMO is positioned relative to the surrounding ecosystem. They are not existential, but they shape whether the work compounds into something used or remains isolated.

### Differentiator is thinner than the docs imply

**Status:** Open.

**Concern.** [vision.md](vision.md) names Wolverine and Dapr as closest in spirit and claims ATAMO differs by treating "delegated identity, multi-agent fan-out, streaming-response-as-message, and disconnected agents as core concerns rather than features." Examined item by item:

- **Multi-agent fan-out** is publish/subscribe with multiple subscribers. Every .NET bus has this.
- **Streaming-response-as-message** is `IAsyncEnumerable<T>` keyed on a correlation ID. Wolverine plus a few hundred lines of glue achieves this.
- **Disconnected agents** is the competing-consumer pattern with a long visibility timeout. Every broker and bus already supports it.
- **Per-principal credentials** is genuinely distinctive — but the [credential vault open question](open-questions.md#credential-vault-strategy) leans on Nango or Composio for the production answer, which means ATAMO is mostly a routing abstraction over their work.

What is actually distinctive is the *combination* plus the audit-first stance. That may be a real product, but the docs frame these as a category, which they are not.

**Why it matters.** A design corpus that overclaims its differentiator does not survive contact with skeptical readers, including potential users evaluating ATAMO against Wolverine for a real project. If the answer to "why not just use Wolverine?" is not sharp and honest, adoption stalls regardless of how good the implementation is.

**What would resolve it.** A section in [vision.md](vision.md) — or a dedicated `docs/design/comparisons.md` — that treats Wolverine, MassTransit, Orleans, Dapr, and MediatR head-on. For each, a paragraph naming what they do well, what ATAMO does that they do not, what they do that ATAMO deliberately does not, and what the migration cost looks like in either direction. Honest comparison is more credible than implicit superiority.

### Unacknowledged overlap with the actor model

**Status:** Open.

**Concern.** Per-agent inboxes (ADR 0003) plus role-as-pull-target (ADR 0002) plus recursion protection plus supervised lifecycle is, structurally, the actor model. Akka.NET, Proto.Actor, and Orleans grains all give an experienced .NET developer this shape with battle-tested implementations. None of these are mentioned anywhere in the design corpus.

**Why it matters.** "Why not Orleans?" is a question a sophisticated reader will ask in the first ten minutes. Without an answer in the docs, the project looks like it is reinventing actors without engaging with the prior art. The likely answer ("Orleans is heavy, opinionated about clustering, and has no per-principal audit story") is defensible — but it has to be written down.

**What would resolve it.** A subsection in `comparisons.md` (above) or in [architecture.md](architecture.md) treating the actor-model overlap explicitly: where ATAMO's inbox model and Akka/Proto/Orleans converge, where they diverge, why ATAMO is not built on top of one of them, and under what circumstances a consumer should choose Orleans grains over ATAMO agents.

### "Compose, don't subsume" eats the surface area

**Status:** Open.

**Concern.** The principle "compose, don't subsume" is correct as a design instinct, but pursued consistently it leaves a small library behind. Once you delegate transport to Wolverine, industrial inbox to RabbitMQ/SQS/Service Bus, persistence to Marten, credentials to Nango, telemetry to OpenTelemetry, retries to Polly, email to MailKit, and LLM to consumer code, what remains in ATAMO's core is:

- Routing rule evaluation
- Recursion protection
- Correlation tracking
- Governor schema and query surface
- Principal threading
- Registration ergonomics

This is a real library, but it is a much smaller one than the eight component pages and three samples imply. The risk is a corpus that designs a large surface and an implementation that, after delegation, defends only a small one — leaving the docs and the code visibly mismatched.

**Why it matters.** Either the docs over-promise (and consumers feel that the value is thinner than advertised), or the implementation over-delivers (and the project stays larger than the principle would have it be). Both outcomes are worse than a design that names its actual scope from the start.

**What would resolve it.** An explicit "what is in the core after delegation" section in [architecture.md](architecture.md), naming the irreducible ATAMO contribution and treating everything else as integration points. This will probably reveal that the core is smaller than expected, which is a feature, not a bug.

### Core-knows-nothing principle vs. the value pitch

**Status:** Open.

**Concern.** The README sells "route work to AI/human/code agents" but the [core-knows-nothing principle](principles.md#the-core-knows-nothing-about-its-use-cases) means the substrate ships zero AI/email/HTTP/SQL agents. Consumers must write all of those themselves. The first sample requires the consumer to provide IMAP polling, SMTP sending, a local-LLM bridge, persistence, and the orchestration glue. That is a lot of code for a substrate whose pitch is "five minutes to a working hub."

The "30 seconds to working hub" phrasing in [principles.md](principles.md) is honest only in the strict sense — a hub with no agents *runs*, but it *does nothing*. The cliff between "running" and "useful" is steep, and the design does not currently address how a new user crosses it.

**Why it matters.** Library adoption depends on the new-user experience in the first half hour. If the on-ramp from "hub running" to "hub doing something useful" requires writing several integrations from scratch, most users will not stay long enough to discover the substrate's deeper properties.

**What would resolve it.** A clear positioning of `Atamo.Agents.Common` (the companion package mentioned in [vision.md](vision.md)) as the on-ramp, with reusable agents for the most common shapes (HTTP client, SQL writer, file watcher, console source, simple LLM bridge). The companion package is acknowledged as out-of-core; the gap is that the design treats it as optional polish rather than as the thing that makes the substrate usable on day one.

A v0 with a working `Atamo.Agents.Common` containing two or three high-leverage agents is a genuinely usable library. A v0 without it is a substrate looking for a reason to exist.

## Design-stage concerns

These are concerns about specific design decisions or unresolved questions that, if mishandled, could degrade the project without killing it.

### In-process performance claim is unverified

**Status:** Open.

**Concern.** [ADR 0003](../adr/0003-per-agent-inboxes.md) commits to the in-process inbox cycle being "sub-millisecond" and cites SQLite-WAL handling tens of thousands of enqueue/dequeue operations per second. That number is achievable in isolation. But the real hot path adds:

- Routing rule evaluation
- Recursion protection check
- Principal/credential lookup
- Governor lifecycle event writes (enqueue, lease, ack, response)
- Lease bookkeeping

If each of these adds even a small fixed cost, the in-process latency could plausibly be 5-10ms instead of microseconds. That collapses scenario 6 (responsive client UI) and weakens the embedded-by-default story significantly.

**Why it matters.** "Sensible defaults, swappable layers" loses its central argument if the default is unusable for the responsive-UI use case the design specifically claims to serve.

**What would resolve it.** A benchmark, run before the v0 sample is finalised, of the full in-process happy path against a Wolverine baseline. Acceptable result: within 2-3x of Wolverine on a representative workload. If the result is 10x or worse, either the in-process implementation needs serious work or scenario 6 needs caveats in the documentation.

### Premature contract specification across v0/v1/v2

**Status:** Open.

**Concern.** The design corpus commits substantial contract surface across all three samples — lease extension, claim/release semantics, history-then-live with sequence markers, governor causal-chain queries, recursion protection semantics, principal-flowing-through-causal-chains — none of which has met a compiler. Each will reveal a wrinkle when it hits code. Re-doc-ing is cheap, but re-coding around docs that are now wrong is a morale tax, and the sheer amount of locked-in contract surface multiplies the chance that v0 ships late.

**Why it matters.** This violates the project's own [build one delightful path before generalising](principles.md#build-one-delightful-path-before-generalising) principle. The principle is about code, but the spirit applies to contracts too — committing to v1 and v2 contract details before v0 has compiled is a form of premature generalisation.

**What would resolve it.** Mark v1 and v2 contract details (history-then-live markers, claim/release/extend ergonomics, principal-chain semantics) as *provisional* in the docs, with a note that they will be revisited after v0 lands. Keep them as forcing functions for v0 design decisions, but do not treat them as committed.

### v0 sample scope is too large

**Status:** Open.

**Concern.** The v0 sample as written in [first-sample.md](first-sample.md) requires: IMAP polling, SMTP sending, a local-LLM bridge, a SQLite message store, a CLI/test harness for the message-store agent, a working SQLite Governor sink, end-to-end audit chain, and recursion protection. The fallback ("console-driven Source and file-writing Agent") is acknowledged but framed as a retreat.

**Why it matters.** A v0 sample that is large enough to be a small product in its own right is a v0 sample that ships late or not at all. The samples exist to validate the substrate; their value-as-demos is secondary. Optimising for "useful in its own right" inflates scope past what the substrate-validation goal actually requires.

**What would resolve it.** Promote the fallback to be the *primary* v0. Keep the email-triage scenario as a stretch goal or as the v0.5 milestone. The substrate validation properties (Source/Agent role separation, response-as-message, content-vs-metadata split, retrievable history, recursion protection) are *all* exercised by the fallback. The polished demo can come after the substrate is real.

If the email scenario remains the v0, write a hard time budget into [first-sample.md](first-sample.md) — "if v0 has not shipped by date X, fall back to the console scenario" — so the decision is forced rather than drifting.

### Audit schema lock-in risk

**Status:** Open.

**Concern.** The Governor is committed to having queryable storage with a stable enough shape that consumers can build on it ([architecture.md](architecture.md), the history-then-live commitment in v1). The actual schema is currently "driven by the persistence-layer choice" and lives in [open-questions.md](open-questions.md). Once consumers start querying against the schema, changing it is expensive — and the v1 sample is itself a consumer, so this lock-in arrives early.

**Why it matters.** Audit schemas that are designed by accretion tend to acquire awkward joins, ambiguous columns, and irregular cardinalities that consumers paper over and then depend on. The cost of getting it wrong on first ship is paid by every future consumer.

**What would resolve it.** A dedicated ADR — likely numbered around the time the persistence-layer decision lands — that names the audit schema explicitly: tables (or document shapes), the keys that join them, the indexes consumers can rely on, and the explicit non-guarantees. The ADR should be written *before* the v1 sample is implemented, because the v1 sample is the first non-trivial consumer of the schema.

### Multi-tenancy scope creep

**Status:** Open.

**Concern.** [Multi-tenancy is a design concern, not a future feature](principles.md#multi-tenancy-is-a-design-concern-not-a-future-feature) is the right principle, but pursued literally it implies: per-principal credentials, per-principal quotas, tenant-submitted rule sandboxing, agent runtime isolation, per-tenant audit retention, and the operational machinery to support all of these. That is a SaaS platform's worth of substrate. Without a tenant who is paying for it, this scope will inflate the v0 surface until v0 does not ship.

**Why it matters.** "Multi-tenancy first-class" can mean two very different things: *the principal abstraction is in the core from day one* (cheap and right) versus *every multi-tenant capability is in v0* (expensive and likely fatal to scope).

**What would resolve it.** Make the distinction explicit in [principles.md](principles.md): the principal abstraction is in v0, but the operational multi-tenancy machinery (sandboxing, quotas, runtime isolation, tenant-side rule submission) is deliberately post-v0 and gated on a real multi-tenant consumer. The principle stands; the v0 scope contracts.

## Cosmetic concerns

These do not affect the project's substance but are worth noting because they affect how it lands with readers.

### The name

**Status:** Open.

**Concern.** "ATAMO = And Then A Miracle Occurs" is a great in-joke. As a library name presented to potential users for the first time, it splits readers into two groups: those who recognise the cartoon reference and find it charming, and those who hear "the author thinks the hard parts are handwaving." The second group is larger and includes most people evaluating the library against Wolverine.

**Why it matters.** Library names do real work in the first ten seconds of a reader's encounter. A name that signals "this might be unserious" makes the polished design corpus harder to land.

**What would resolve it.** A deliberate decision about whether the name is part of the project's positioning or a placeholder. If it stays, the README should own the joke explicitly — "yes, the name is from the cartoon" — rather than leaving readers to figure it out. If it goes, the rename is cheap now and expensive after first release.

This is a low-priority concern but a one-way door once code ships.

## Summary

Of the concerns above, four are close to existential and should be addressed first:

1. Write the 2015 post-mortem.
2. Identify a real first consumer, or rescope the project as a learning exercise.
3. Decide whether the implementation effort matches the available capacity, and rescope v0 if not.
4. Decide what ATAMO is *for* (hobby, portfolio, adoption) and align the sustainability posture to that decision.

Three more are strategic and should be addressed before the public design is presented to potential users:

5. Sharpen the differentiator vs. Wolverine, MassTransit, Orleans, Dapr, MediatR.
6. Engage the actor-model overlap explicitly.
7. Name the irreducible core that survives "compose, don't subsume".

The remaining concerns are tractable as design or implementation work and can be resolved as the relevant decisions land.

None of the concerns above are reasons to stop. They are reasons to be deliberate about what stays in scope and what does not, and about which questions are being deferred versus which ones are being avoided. The design corpus is good; this document exists to make sure the project around it is good too.
