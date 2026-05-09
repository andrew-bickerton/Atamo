# Guide 2: Add LLM triage and outbound replies

Building on the email-ingest application from [guide 1](01-async-email-ingest.md), add a local-LLM agent that triages each inbound email and an SMTP agent that sends replies. The new agents plug into the fan-out from guide 1 — no existing components are modified beyond generalising the store agent to record the new message types.

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

Inbound emails are now reaching your application's UI and database (per guide 1). You want to add automated handling: a local LLM categorises each email, drafts a reply where appropriate, and the application sends the reply via SMTP. You also want everything that flows through — the original email, the LLM's decision, the draft, the sent confirmation — to be recorded as part of the same audit trail and queryable later.

## What you'll add

Two new agents, three new message types, one generalised existing agent:

- A **`TriageLlmAgent`** that consumes `InboundEmail` messages, calls a local LLM, and produces a `TriageDecision` message. When the decision is to reply, it also produces a `ReplyDraft` message.
- An **`OutboxAgent`** that consumes `ReplyDraft` messages, sends via SMTP, and produces a `ReplySent` confirmation.
- The existing **`EmailStoreAgent`** from guide 1 is generalised into a **`MessageStoreAgent`** that records `InboundEmail`, `TriageDecision`, `ReplyDraft`, and `ReplySent`. The store now covers the full chain, not just the inbound side.

The `ImapSource` and `GuiNotifierAgent` from guide 1 are unchanged. The substrate's fan-out lets you add the new agents alongside them; routing rules describe what receives what, and the hub aggregates.

## The walkthrough

### Step 1: define the new message types

```csharp
public sealed record TriageDecision(
    string CorrelationId,
    TriageAction Action,
    string Reasoning);

public enum TriageAction { AutoReply, NeedsHumanReview, NoAction }

public sealed record ReplyDraft(
    string CorrelationId,
    string To,
    string Subject,
    string Body);

public sealed record ReplySent(
    string CorrelationId,
    string MessageId,
    DateTimeOffset SentAt);
```

The `CorrelationId` threads the chain together — every message produced by the triage of a given email carries the same correlation ID. ATAMO assigns it automatically when the source emits the original `InboundEmail`; agents producing responses inherit it.

### Step 2: write the triage agent

```csharp
public sealed class TriageLlmAgent : IAgent
{
    private readonly ILocalLlm _llm;
    private readonly IResponder _responder;

    public TriageLlmAgent(ILocalLlm llm, IResponder responder)
    {
        _llm = llm;
        _responder = responder;
    }

    public async ValueTask HandleAsync(Message msg, CancellationToken ct)
    {
        if (msg is Message<InboundEmail> email)
        {
            var decision = await _llm.TriageAsync(email.Payload, ct);
            await _responder.RespondAsync(msg, decision, ct);

            if (decision.Action == TriageAction.AutoReply)
            {
                var draft = await _llm.DraftReplyAsync(email.Payload, ct);
                await _responder.RespondAsync(msg, draft, ct);
            }
        }
    }
}
```

The agent emits responses through `IResponder`, which the substrate provides. Each response inherits the originating message's correlation ID, so subscribers tracking the chain see the full causal path.

### Step 3: write the outbox agent

```csharp
public sealed class OutboxAgent : IAgent
{
    private readonly ISmtpClient _smtp;
    private readonly IResponder _responder;

    public OutboxAgent(ISmtpClient smtp, IResponder responder)
    {
        _smtp = smtp;
        _responder = responder;
    }

    public async ValueTask HandleAsync(Message msg, CancellationToken ct)
    {
        if (msg is Message<ReplyDraft> draft)
        {
            var messageId = await _smtp.SendAsync(draft.Payload, ct);
            await _responder.RespondAsync(msg, new ReplySent(
                draft.Payload.CorrelationId, messageId, DateTimeOffset.UtcNow), ct);
        }
    }
}
```

The outbox emits a `ReplySent` confirmation when the SMTP send succeeds. ATAMO's recursion protection ensures `ReplySent` will not be enqueued back into the outbox even if a routing rule for `ReplySent` happens to match.

### Step 4: generalise the store agent

The `EmailStoreAgent` from guide 1 only handled `InboundEmail`. The chain now produces three more types worth recording. Generalise to a `MessageStoreAgent`:

```csharp
public sealed class MessageStoreAgent : IAgent
{
    private readonly IDocumentStore _store;
    public MessageStoreAgent(IDocumentStore store) => _store = store;

    public async ValueTask HandleAsync(Message msg, CancellationToken ct)
    {
        await _store.SaveAsync(msg.CorrelationId, msg, ct);
    }
}
```

One method, four message types, no `switch`: the store does not need to know what kind of message it is recording. See the [persist-messages recipe](../recipes/persist-messages.md) for variations (subset filtering, multiple stores, idempotency).

### Step 5: register the new agents and rules

```csharp
hubBuilder
    .AddAgent<TriageLlmAgent>("triage", o => o.UseLocalModel("config:LLM:Model"))
    .AddAgent<OutboxAgent>("outbound", o => o.From("config:Mail:Smtp"))
    .AddAgent<MessageStoreAgent>("store", o => o.UseSqlite("history.db"))
    .AddRoutingRule(r => r.When<InboundEmail>().RouteTo("triage"))
    .AddRoutingRule(r => r.When<InboundEmail>().RouteTo("store"))
    .AddRoutingRule(r => r.When<ReplyDraft>().RouteTo("outbound"))
    .AddRoutingRule(r => r.When<ReplyDraft>().RouteTo("store"))
    .AddRoutingRule(r => r.When<TriageDecision>().RouteTo("store"))
    .AddRoutingRule(r => r.When<ReplySent>().RouteTo("store"));
```

`InboundEmail` already had rules from guide 1 (UI + store). The triage rule is additive: now it goes to the UI, the store, and triage. The store agent gets every type that should be recorded; the outbox agent gets `ReplyDraft` and produces `ReplySent`, which the store also records.

Routing is additive throughout: every rule that matches contributes a target, and the hub aggregates them. There is no notion of a primary route with side-effects; the rules just describe where messages should go.

## Why this approach

This is the guide where ATAMO's response-as-message pattern earns its keep. A few properties are exercised concretely:

- **Source vs Agent as separate roles.** `ImapSource` plays only the Source role; `OutboxAgent` plays only the Agent role. Two registrations, two contracts. They share configuration (mailbox credentials, server addresses) and may live in the same project, but they are structurally independent. The role separation is what keeps the outbound path from accidentally consuming inbound messages.
- **Response messages as first-class messages.** `ReplyDraft` is consumed by the outbox agent — not as a method return value, but as a routed message. The triage agent never needs to know the outbox exists; it just emits a draft and the substrate routes it. Adding a different downstream consumer (an audit log, a draft-review human-review queue) is one routing rule.
- **Recursion protection in practice.** The outbox emits `ReplySent`. Without protection, a misconfigured rule routing `ReplySent` back to the outbox would loop forever. ATAMO's recursion protection means a message is never enqueued into the inbox of the agent that produced it, even if a rule would otherwise match.
- **Correlation across a round-trip.** Every message carries the original email's correlation ID. Querying the store agent (or the Governor) for that ID returns the entire chain — inbound, triage decision, draft, sent confirmation — without joining records or reconstructing causality.
- **Content storage as a consumer concern.** `MessageStoreAgent` is one agent with one store, separate from the Governor's audit storage. The substrate records that things happened; the store agent records what they contained.

## What's not in this guide

To keep the scope honest:

- **No multi-tenancy.** One mailbox, one user, one set of credentials. The principal abstraction is exercised lightly (each message is on behalf of someone), but tenant boundaries are not.
- **No remote agents.** Everything runs in-process. The agent contract permits remote, but this guide does not exercise it.
- **No deferred retrieval.** Live correlations stay in-memory. A source that submits a request and disconnects loses its subscription; reconnecting later to retrieve results is [guide 3](03-add-history-and-live-feed.md)'s territory.
- **No genuinely disconnected agents.** Every agent here drains its inbox at machine speed. Human-time inboxes are [guide 4](04-add-disconnected-human-review.md).
- **No web UI or dashboard.** The store agent is exercised from a CLI or test harness in this guide. The browsable review form is [guide 3](03-add-history-and-live-feed.md).
- **No HTML email handling beyond what MailKit gives for free.** Plain text is sufficient.
- **No redaction.** The store persists message content as-is. A real deployment with sensitive content would need a redaction strategy; see [the redaction open question](../design/open-questions.md#redaction-in-content-storing-agents).

If any of these threaten to creep into the work, cut them. Each one is a meaningful concern but belongs to a different guide or to a separate design discussion.

## Definition of done

This guide's pattern is working when:

- An email arrives at the watched mailbox; within a few seconds the LLM has produced a `TriageDecision`; if the decision is to reply, an SMTP send happens and a `ReplySent` confirmation is recorded.
- The store agent contains the full chain for every email — inbound, triage, draft (if any), sent (if any) — keyed by correlation ID.
- The Governor records the lifecycle of every message — receive, route, enqueue, lease, ack, response — independently of the store agent's content recording. Disabling the store has no effect on the Governor's audit; disabling a Governor sink has no effect on the store.
- Every API friction point encountered while building the application has been either fixed in ATAMO or recorded as an issue with a deliberate decision to defer.

## Next up

[Guide 3 — Add a history-and-live review form](03-add-history-and-live-feed.md) adds a small web UI that opens onto a window of recent traffic and stays open as a live feed of new messages. It exercises the Governor as a queryable layer (not just a write target) and introduces the standalone host's HTTP surface.

## See also

- [Persist messages of a given type](../recipes/persist-messages.md) — the technique behind `MessageStoreAgent`.
- [Query previously persisted messages](../recipes/query-persisted-messages.md) — preview of guide 3's content side.
- [Agent: response messages](../design/components/agent.md#response-messages) — the contract the triage and outbox agents lean on.
- [Hub: recursion protection](../design/components/hub.md#recursion-protection) — why `ReplySent` does not loop back to the outbox.
