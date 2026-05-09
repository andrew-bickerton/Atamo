# Query previously persisted messages

## When you'd want this

You have a content-storing agent (see [Persist messages of a given type](persist-messages.md)) that has been recording messages, and now you want to read them back. A user wants to see their past triage decisions; a reviewer wants to find a specific email from last week; an analyst wants to count how many replies went out yesterday. The store is yours, but you want the query to flow through ATAMO so the audit trail is complete and the same agent owns both reads and writes.

## The technique

The agent that persists messages also handles query messages. A query is just a message; the agent receives it, reads from its own store, and emits the results as response messages.

```csharp
public sealed class MessageStoreAgent : IAgent
{
    private readonly IDocumentStore _store;
    private readonly IResponder _responder;

    public MessageStoreAgent(IDocumentStore store, IResponder responder)
    {
        _store = store;
        _responder = responder;
    }

    public async ValueTask HandleAsync(Message msg, CancellationToken ct)
    {
        switch (msg)
        {
            case Message<InboundEmail> email:
                await _store.SaveAsync(email.CorrelationId, email, ct);
                break;

            case Message<TriageDecision> decision:
                await _store.SaveAsync(decision.CorrelationId, decision, ct);
                break;

            case Message<MessageHistoryQuery> query:
                await foreach (var hit in _store.QueryAsync(query.Payload.Filter, ct))
                {
                    await _responder.RespondAsync(query, hit, ct);
                }
                break;
        }
    }
}

hubBuilder
    .AddAgent<MessageStoreAgent>("store")
    .AddRoutingRule(r => r.When<InboundEmail>().RouteTo("store"))
    .AddRoutingRule(r => r.When<TriageDecision>().RouteTo("store"))
    .AddRoutingRule(r => r.When<MessageHistoryQuery>().RouteTo("store"));
```

A source — a web UI, a CLI, an API endpoint — submits a `MessageHistoryQuery` message. The hub routes it to the store agent. The agent reads from its store and emits each match as a response. The source receives the responses through normal correlation tracking.

This is the same shape as any other request/response flow in ATAMO. The store agent is no different from an LLM agent or any other request-handler. The fact that it happens to read from a database rather than calling a model is invisible to the substrate.

## Variations

**Multiple result streams.** A query that produces a lot of results can stream them as the agent reads from the store, rather than buffering. The source receives each result as it arrives and can stop subscribing whenever it has enough. This is the same streaming-response pattern used elsewhere; see [the Agent page](../design/components/agent.md#response-messages).

**Different query types.** Define a typed message per query shape — `FindByCorrelationId`, `FindByPrincipal`, `FindByDateRange`, `FullTextSearch` — and let the agent's `switch` route each to the appropriate store operation. This keeps the wire protocol explicit and discoverable.

**Read-only agent.** If your write path and read path are sufficiently different, split them into two agents — `MessageStoreWriter` and `MessageStoreReader` — pointing at the same store. The substrate doesn't care; from its perspective these are two registrations. The split can make sense when read load and write load have different scaling characteristics, or when you want to deploy the reader separately for security reasons.

**Querying live as well as historical.** The substrate's history-then-live pattern (see [`second-sample.md`](../design/second-sample.md)) is the right tool when a consumer wants past results _and_ a live feed of new ones. The store agent handles the historical part; the substrate handles the live part. Combining them cleanly is what the second sample is for.

**Pagination.** Long result sets are easier to handle with explicit cursors. A `MessageHistoryQuery` can carry a `cursor` field; the agent reads a bounded page from the store and emits a final `QueryComplete` response carrying the next cursor. The source decides whether to issue a follow-up query.

## See also

- [Persist messages of a given type](persist-messages.md) — the write side of this pair.
- [Agent component](../design/components/agent.md) — particularly the response-messages section.
- [Hub component](../design/components/hub.md) — recursion protection ensures the store agent's responses don't loop back to itself.
- [Second sample](../design/second-sample.md) — combines querying with a live feed using the history-then-live pattern.
