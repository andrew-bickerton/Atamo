# First sample: email triage with local LLM

The v0 sample is a self-hosted email triage assistant. It is small enough to build, useful enough to run, and structurally rich enough to pressure-test ATAMO's core abstractions before they ossify.

## Contents

- [What the sample does](#what-the-sample-does)
- [Components](#components)
- [Why this scenario](#why-this-scenario)
- [What the consumer-facing setup should feel like](#what-the-consumer-facing-setup-should-feel-like)
- [What is intentionally out of this sample](#what-is-intentionally-out-of-this-sample)
- [Fallback if email is too much](#fallback-if-email-is-too-much)
- [Definition of done](#definition-of-done)

## What the sample does

An IMAP-watched mailbox feeds inbound emails into the hub as messages. A local-LLM agent triages each email — categorising it, drafting a reply, or marking it for human attention. The LLM's response becomes a message in its own right, routed onward. An outbound-email agent picks up reply messages and sends them via SMTP. A message-store agent subscribes to the message types the consumer wants persisted, writes their content to its own store, and answers history queries by reading from that same store.

Run end-to-end: an email arrives, the LLM drafts a response, the response is sent, the message-store agent has recorded the content of every message it cares about, and a query against the store agent returns the history of any past triage. The Governor independently records what happened — message lifecycle, routing decisions, inbox events — but does not store the content of messages. Content storage is the message-store agent's job, not the substrate's.

## Components

The sample registers five components:

**Sources** (inject messages):
- `InboxSource` — polls IMAP, injects each new email as an `InboundEmail` message.

**Agents** (consume from inboxes):
- `TriageLlmAgent` — receives `InboundEmail` messages, calls a local LLM, produces a `TriageDecision` message. May produce a `ReplyDraft` message if a reply is appropriate.
- `OutboxAgent` — receives `ReplyDraft` messages, sends via SMTP, produces a `ReplySent` confirmation.
- `MessageStoreAgent` — subscribes to `InboundEmail`, `TriageDecision`, `ReplyDraft`, and `ReplySent` messages and persists their content to a SQLite store. Also handles `MessageHistoryQuery` messages by reading from that store and emitting matching historical messages as responses.

The email integration is two registrations, not one. `InboxSource` plays only the Source role; `OutboxAgent` plays only the Agent role. They share configuration (mailbox credentials, server addresses) and may live in the same project, but they are structurally independent. This is deliberate — it confirms that the role separation in the architecture holds up under a realistic scenario rather than being theoretical.

The `MessageStoreAgent` is — also deliberately — just another agent. It writes content to a store it owns; it reads content from that same store; it has no special framework support. Audit metadata flows through the Governor; message content flows through this agent. The two are independent and the sample demonstrates both. See the [persist-messages](../recipes/persist-messages.md) and [query-persisted-messages](../recipes/query-persisted-messages.md) recipes for the technique in isolation.

## Why this scenario

It was tempting to start with a console-output "hello, agent" sample. The reason this larger one is the v0 instead: a single-agent fire-and-forget demo exercises maybe a third of ATAMO's primitives. This scenario exercises almost all of them, in a context that maps onto something a developer might actually want.

Specifically, this scenario forces decisions about:

- **Source vs Agent as separate roles.** Two registrations sharing config but not contract makes the role distinction concrete. If the architecture's role separation generates friction here, that's a signal to rethink before generalising.
- **Inboxes in practice.** Even though every agent in this sample drains its inbox at machine speed, the inbox model is exercised end-to-end. The default SQLite inbox carries every message through enqueue, lease, ack, and audit — so the v0 substrate honestly demonstrates the contract that disconnected agents will rely on later.
- **Response messages as first-class messages.** The LLM's `ReplyDraft` is consumed by the SMTP agent — not as a method return value, but as a routed message. Get this right in the sample and the abstraction generalises; get it wrong and every consumer will end up working around it.
- **Correlation across a round-trip.** Inbound email → triage → outbound reply must thread a correlation ID through cleanly so the reply lands in the right thread.
- **Recursion protection in practice.** The `OutboxAgent` must not pick up messages it itself produced (`ReplySent` is structurally similar to `ReplyDraft`), or the system loops forever sending mail to itself. This sample makes that hazard concrete and the protection testable.
- **Content storage as a consumer concern.** The message-store agent is the canonical example of the principle that the substrate stores metadata and consumers store content. If this feels natural to write — just an agent with routing rules and a store — the principle is honest. If it feels like fighting the framework, the abstractions are wrong.
- **Per-principal credentials in a non-toy setting.** The mailbox belongs to a user. The LLM may have a per-user model selection or quota. The SMTP send happens on a user's behalf. This pushes the principal abstraction past the lip-service stage.

## What the consumer-facing setup should feel like

The exact API will settle in code, but the sample is the forcing function for its shape. A developer writing this sample should produce something close to:

```csharp
var hub = AtamoHub.CreateBuilder()
    .AddSource<InboxSource>("inbox", o => o.From("config:Mail:Inbox"))
    .AddAgent<TriageLlmAgent>("triage", o => o.UseLocalModel("config:LLM:Model"))
    .AddAgent<OutboxAgent>("outbound", o => o.From("config:Mail:Smtp"))
    .AddAgent<MessageStoreAgent>("store", o => o.UseSqlite("history.db"))
    .UseSqliteGovernor("audit.db")
    .AddRoutingRule(r => r.When<InboundEmail>().RouteTo("triage"))
    .AddRoutingRule(r => r.When<InboundEmail>().RouteTo("store"))
    .AddRoutingRule(r => r.When<ReplyDraft>().RouteTo("outbound"))
    .AddRoutingRule(r => r.When<ReplyDraft>().RouteTo("store"))
    .AddRoutingRule(r => r.When<TriageDecision>().RouteTo("store"))
    .AddRoutingRule(r => r.When<ReplySent>().RouteTo("store"))
    .AddRoutingRule(r => r.When<MessageHistoryQuery>().RouteTo("store"))
    .Build();

await hub.RunAsync(cancellationToken);
```

This sketch is illustrative, not committed. The point is to reveal the API choices the sample needs to settle: how Sources and Agents are distinguished at registration time, how rules are expressed, how typed messages relate to runtime routing, where async ownership sits, how cancellation and shutdown propagate. If writing the sample produces ten such questions and good answers to all of them, the v0 API is ready.

Note the routing pattern: each message type that should be persisted has a rule routing it to `"store"` — separately from any other routing it has. `InboundEmail` goes to both `"triage"` and `"store"`; `ReplyDraft` goes to both `"outbound"` and `"store"`. Routing is additive: every rule that matches contributes a target, and the hub aggregates them. There is no notion of a primary route with additions; the rules just describe where messages should go, and the hub honours all of them.

## What is intentionally out of this sample

To keep the sample small enough to actually finish:

- No multi-tenant principal management. One mailbox, one user, one set of credentials.
- No remote agents. Everything runs in-process. The agent contract should _allow_ remote, but the sample does not exercise it.
- No deferred retrieval (scenario 3 from the vision). Live correlations stay in-memory.
- No genuinely disconnected agents. Every agent in this sample drains at machine speed; the disconnected scenario gets its own sample later.
- No user-submitted rules or agents, no sandboxing. Those are post-v0.
- No web UI, no dashboard. The message-store agent is exercised from a CLI or test harness; the review form is the v1 sample.
- No HTML email handling beyond what MailKit gives for free. Plain text is sufficient.
- No conversation threading beyond the basic In-Reply-To header.
- No redaction. The store agent persists message content as-is. A real deployment with sensitive content would need a redaction strategy; the sample does not address it. See [the redaction open question](open-questions.md#redaction-in-content-storing-agents).

If any of these threaten to creep into v0, they should be cut. The sample is not a product; it is a calibration tool for the substrate.

## Fallback if email is too much

The smallest viable version that still tests the same things is to replace the IMAP/SMTP components with a console-driven Source and a file-writing Agent — keeping the LLM and message-store agents unchanged. This loses the polished demo but preserves the structural properties (Source/Agent role separation, response-as-message, content-storage-as-consumer-concern, retrievable history). If the email integration starts becoming its own project, fall back to this.

## Definition of done

The v0 sample is done when:

- A developer can clone the sample repo, supply a mailbox config and a local model path, and have a working triage assistant within fifteen minutes.
- The sample runs continuously without leaks or stuck correlations.
- The Governor records a complete and queryable audit trail of every message lifecycle event (receive, route, enqueue, lease, ack, response).
- The message-store agent persists message content for every type subscribed, and `MessageHistoryQuery` returns correct results from that store.
- The Governor and the message-store agent's storage are independent. Disabling the message-store agent has no effect on the Governor's audit; disabling the Governor sink has no effect on the message-store agent's persistence.
- The sample compiles against the public ATAMO API only, with no `internal`-visible escapes.
- Every API friction point encountered while building the sample has been either fixed in ATAMO or recorded as an issue with a deliberate decision to defer.
