# Guide 1: Receive emails into a GUI and a database

Receive incoming emails into your application, surface them in a user interface as they arrive, and persist them locally — without the UI and the database having to know about each other.

## Contents

- [When you'd want this](#when-youd-want-this)
- [What you'll build](#what-youll-build)
- [The walkthrough](#the-walkthrough)
- [Why this approach](#why-this-approach)
- [What's not in this guide](#whats-not-in-this-guide)
- [Definition of done](#definition-of-done)
- [Next up](#next-up)
- [See also](#see-also)

## When you'd want this

You have a desktop or service application that needs to react to inbound emails. Two things have to happen for each one: it should appear in the application's UI within a second or two of arriving, and it should be persisted locally so nothing is lost if the UI is closed. You also want adding a third consumer later — a Slack notification, an alert webhook, a nightly export — to be cheap.

The naive way is to read emails on a background thread, write to the database, then marshal back to the UI thread to update the view. This works, but the database and the UI become coupled in the same code path: a slow database write delays the UI update, and adding a third consumer means refactoring the path everyone shares. ATAMO's role here is to keep those consumers independent so each one can evolve, fail, or be replaced without touching the others.

## What you'll build

Three components plus your host application:

- An **`ImapSource`** that polls a mailbox and injects each new email as an `InboundEmail` message. (Source role: ATAMO does not pull from sources, the source pushes to ATAMO.)
- A **`GuiNotifierAgent`** that receives `InboundEmail` messages and pushes them into the UI through a small bridge service the host app provides. (Agent role.)
- An **`EmailStoreAgent`** that receives the same `InboundEmail` messages and writes them to a local SQLite database. (Agent role.)

One message type, two agents, no coupling between them. The hub fans the message out to both inboxes; each agent pulls from its own inbox at its own pace.

## The walkthrough

### Step 1: define the message

A message in ATAMO is a record. The substrate handles correlation, principal, and timestamps for you; you just declare the shape of the payload.

```csharp
public sealed record InboundEmail(
    string From,
    string Subject,
    string Body,
    DateTimeOffset ReceivedAt);
```

### Step 2: write the source

A source injects messages into the hub on its own initiative. For an IMAP poller, the loop checks for new mail and submits each one. ATAMO does not provide an IMAP client; use [MailKit](https://github.com/jstedfast/MailKit) or whatever you already have.

```csharp
public sealed class ImapSource : ISource
{
    private readonly IMailbox _mailbox;
    private readonly IHubReceiver _hub;

    public ImapSource(IMailbox mailbox, IHubReceiver hub)
    {
        _mailbox = mailbox;
        _hub = hub;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await foreach (var email in _mailbox.PollAsync(ct))
        {
            await _hub.SendAsync(new InboundEmail(
                email.From, email.Subject, email.Body, email.ReceivedAt), ct);
        }
    }
}
```

That is the complete source. Once it's registered, ATAMO calls `RunAsync` on startup and cancels the token on shutdown.

### Step 3: write the agents

Each agent is a class implementing `IAgent`. The substrate hands it messages from its inbox; the agent does whatever it needs and acks. The two agents below are the entire content side of this guide.

```csharp
public sealed class EmailStoreAgent : IAgent
{
    private readonly IEmailRepository _repo;
    public EmailStoreAgent(IEmailRepository repo) => _repo = repo;

    public async ValueTask HandleAsync(Message msg, CancellationToken ct)
    {
        if (msg is Message<InboundEmail> email)
        {
            await _repo.SaveAsync(email.Payload, ct);
        }
    }
}

public sealed class GuiNotifierAgent : IAgent
{
    private readonly IEmailUiBridge _ui;
    public GuiNotifierAgent(IEmailUiBridge ui) => _ui = ui;

    public ValueTask HandleAsync(Message msg, CancellationToken ct)
    {
        if (msg is Message<InboundEmail> email)
        {
            _ui.NotifyNewEmail(email.Payload);
        }
        return ValueTask.CompletedTask;
    }
}
```

`IEmailRepository` is your data-access layer; `IEmailUiBridge` is whatever your UI app provides to receive a notification on the right thread (a service that marshals to the UI thread, an event, an `IObservable<T>`, an `IAsyncEnumerable<T>` channel — your choice). Neither agent knows about the other.

### Step 4: register everything and run

```csharp
var hub = AtamoHub.CreateBuilder()
    .AddSource<ImapSource>("imap", o => o.From("config:Mail:Inbox"))
    .AddAgent<GuiNotifierAgent>("ui")
    .AddAgent<EmailStoreAgent>("store")
    .AddRoutingRule(r => r.When<InboundEmail>().RouteTo("ui"))
    .AddRoutingRule(r => r.When<InboundEmail>().RouteTo("store"))
    .UseSqliteGovernor("audit.db")
    .Build();

await hub.RunAsync(cancellationToken);
```

Two routing rules. Each `InboundEmail` is fanned out to both the GUI agent's inbox and the store agent's inbox. They process independently.

### Step 5: extend without rewriting

Want to add a Slack notification when an email mentions "urgent"? Three lines:

```csharp
.AddAgent<SlackNotifierAgent>("slack")
.AddRoutingRule(r => r
    .When<InboundEmail>(e => e.Subject.Contains("urgent", StringComparison.OrdinalIgnoreCase))
    .RouteTo("slack"))
```

The new agent is registered alongside the others; the existing components do not change. Nothing knows about Slack except the agent that talks to Slack and the rule that decides when to invoke it.

## Why this approach

A few ATAMO properties are already exercised by this two-consumer fan-out:

- **Dispatch is enqueue, not function-call.** A slow database write does not slow down GUI updates. Each agent has its own inbox; if the database is busy, emails accumulate in the store agent's inbox and the GUI agent keeps draining its own at full speed.
- **Adding a consumer is additive.** No refactoring of existing components. The new agent is a registration; the new rule is a one-liner. This is the property the guide is really demonstrating: extension is cheap because nothing is coupled.
- **Crash recovery is automatic.** If the application restarts mid-dispatch, in-flight messages stay in their durable inboxes (SQLite-backed by default) and resume on startup. No special code in the agents.
- **Audit is a Governor concern, content is your concern.** The Governor records that an email arrived, was routed, was enqueued to two agents, was processed. The email's actual content lives in the store agent's database. Two stores, two responsibilities, no overlap.

## What's not in this guide

To keep the scope honest:

- **No triage or transformation.** The agents handle every email the same way. [Guide 2](02-add-llm-triage.md) introduces an LLM agent that triages emails and produces decisions.
- **No outbound replies.** Nothing leaves the application. [Guide 2](02-add-llm-triage.md) adds an SMTP agent.
- **No querying past emails.** The store agent only writes. [Guide 3](03-add-history-and-live-feed.md) adds a query path and a live feed.
- **No multi-tenancy.** One mailbox, one principal. The principal abstraction is in the substrate from day one but this guide does not exercise it.
- **No remote agents.** Everything is in-process. The agent contract permits remote agents, but this guide does not exercise it.

## Definition of done

The guide's pattern is working when:

- Sending an email to the watched mailbox results in the UI updating within a second or two and the email appearing in the SQLite database.
- Closing the UI does not lose emails; they continue to be persisted by the store agent.
- Disabling the store agent does not affect the UI; disabling the UI agent does not affect the store. They are independent consumers.
- Adding a third agent (Slack, webhook, log file, anything) requires no change to the existing two.

## Next up

[Guide 2 — Add LLM triage and outbound replies](02-add-llm-triage.md) extends this application with a local-LLM agent that triages each email and an SMTP agent that sends replies. Response messages and correlation IDs become important; the same fan-out pattern handles a new wave of message types.

## See also

- [Persist messages of a given type](../recipes/persist-messages.md) — the technique behind `EmailStoreAgent` in isolation.
- [Source component](../design/components/source.md) — the role `ImapSource` plays.
- [Agent component](../design/components/agent.md) — the role both agents play.
- [Routing rule provider](../design/components/routing.md) — how the fan-out is expressed.
- [Vision: scenario 6 — responsive client application](../design/vision.md#6-responsive-client-application) — the broader scenario this guide instantiates.
