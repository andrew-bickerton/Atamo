# Persist messages of a given type

## When you'd want this

You have messages flowing through ATAMO and you want to keep a copy of some of them — for later querying, for reporting, for replay, for compliance, or simply because you want a record. Maybe it's every email that arrives. Maybe it's every triage decision the LLM makes. Maybe it's every reply you send. The substrate itself doesn't store message content (only metadata via the Governor), so persisting content is your job.

## The technique

Write an agent that subscribes to the message types you care about and writes them to whatever store fits your use case. Register a routing rule that sends those message types to the agent.

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

hubBuilder
    .AddAgent<MessageStoreAgent>("store")
    .AddRoutingRule(r => r.When<InboundEmail>().RouteTo("store"))
    .AddRoutingRule(r => r.When<TriageDecision>().RouteTo("store"))
    .AddRoutingRule(r => r.When<ReplySent>().RouteTo("store"));
```

The agent is unremarkable: it implements the standard agent contract, takes whatever store it wants as a dependency, and writes each message it receives. The routing rules are also unremarkable — each one says "messages of type X should go to the store." Other rules elsewhere in the registration may also route those same messages to other agents (the outbound mail agent, the triage agent, the audit logger). Routing is additive: every rule that matches a message contributes a target, and the hub aggregates them. The store agent gets its copy without preventing any other agent from getting its own.

The choice of store is yours. SQLite for a single-process deployment, Postgres or SQL Server for shared persistence, Marten if you want event-sourcing semantics, S3 or blob storage for large or long-retention payloads, a search index if you'll be querying by content. ATAMO does not care.

## Variations

**Persist only a subset.** Add a predicate to the rule:

```csharp
.AddRoutingRule(r => r
    .When<TriageDecision>(d => d.Action != TriageAction.AutoReply)
    .RouteTo("store"))
```

Now only triage decisions that needed human attention are persisted; routine auto-replies are not.

**Persist everything.** Subscribe to the base message type:

```csharp
.AddRoutingRule(r => r.When<IMessage>().RouteTo("store"))
```

This is what audit-style content recording looks like. Every message that flows through the hub gets persisted. Be aware that this can be a lot of data, and that some of it may be sensitive — see [the redaction open question](../design/open-questions.md#redaction-in-content-storing-agents).

**Multiple stores for different purposes.** Register multiple agents, each with its own store and its own subscription. A `HotStorageAgent` writing to SQLite for fast recent-history queries; a `ColdStorageAgent` writing to S3 for long-term retention; an `AnalyticsAgent` writing to a data warehouse. The substrate handles the fan-out.

**Idempotency.** Because ATAMO promises at-least-once delivery, your store will occasionally see the same message twice. Use the message's correlation ID as a unique key (or include `INSERT ... ON CONFLICT DO NOTHING`-style semantics) so duplicate writes are benign. See [the Agent page](../design/components/agent.md#idempotency-and-at-least-once-delivery) for the broader discipline.

## See also

- [Query previously persisted messages](query-persisted-messages.md) — the natural counterpart to this recipe.
- [Agent component](../design/components/agent.md) — the contract this technique builds on.
- [Routing rule provider](../design/components/routing.md) — how rules are evaluated and how multiple matching rules combine.
- [Principles: the substrate stores metadata; consumers store content](../design/principles.md#the-substrate-stores-metadata-consumers-store-content) — the design principle this recipe embodies.
