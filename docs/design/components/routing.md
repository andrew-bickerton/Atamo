# Routing rule provider

A routing rule provider is a component that, given a message, decides which agent inboxes the message should be enqueued into and how the message should be shaped for each. It is the extensibility point that lets consumers plug in domain-specific routing logic without modifying the hub.

## Contents

- [Overview](#overview)
- [The provider contract](#the-provider-contract)
- [The default provider](#the-default-provider)
- [Custom providers](#custom-providers)
- [Multiple providers](#multiple-providers)
- [Where rules live](#where-rules-live)
- [Open questions](#open-questions)
- [References](#references)

## Overview

The hub does not know how to route messages. It knows how to ask. When a message arrives, the hub asks every registered routing rule provider "what should happen with this?" and aggregates the answers. Each provider returns zero or more (target agent, shaped message) pairs.

This indirection is what allows ATAMO to be a substrate rather than a fixed product. Two consumers can use the same hub with completely different routing semantics by registering different providers — one might use simple type-based matching, another might use a tenant-defined rules DSL, a third might use an LLM-based router that classifies messages on the fly.

## The provider contract

A routing rule provider exposes a single operation:

- **Evaluate.** Given a message and the current set of registered agents, return zero or more (agent, shaped-message) pairs.

That is the entire contract. A provider that returns an empty result for every message is valid (and probably useless). A provider that returns hundreds of pairs for a single message is also valid (and probably wasteful). The hub does not constrain the answer, only the question.

The shaped-message in each pair may be the original message unchanged, or it may be a transformed version with a different type or payload. This is how a single inbound message can fan out into multiple differently-shaped messages targeting different agents — for example, an `OrderPlaced` event might shape into a `SendConfirmationEmail` for the email agent and a `RecordSale` for the analytics agent.

Providers are pure functions of their inputs in principle. In practice they may consult external state (a database of tenant rules, a cache of recently-seen messages) but should not have side-effects. The hub may call them concurrently for different messages.

## The default provider

ATAMO ships with one default routing rule provider: a type-and-principal matcher with template substitution.

It supports rules of the form:

- "When a message of type X arrives from a principal in group Y, enqueue it (possibly transformed) into agent Z's inbox."
- "Always enqueue messages of type X into agent Z's inbox."
- "Enqueue messages of type X into agents Z1 and Z2." (fan-out)

Rules are declared at registration time using a fluent API. They are evaluated in registration order. The default provider supports simple template-based shaping (extract fields from the input message, populate corresponding fields in the output message) but does not support arbitrary transformation logic — that is what custom providers are for.

The default provider is intentionally simple. It exists to make the common case easy and to demonstrate the contract. Consumers with non-trivial routing needs are expected to write their own providers.

## Custom providers

A consumer can register a custom routing rule provider by implementing the provider contract and adding it to the host. Reasons to do so:

- **Tenant-defined rules.** A multi-tenant deployment may store routing rules per tenant in a database; a custom provider reads them and applies the right rules to the right messages.
- **Content-based routing.** Routing decisions based on message content (this email mentions "urgent", route to priority agent) require logic the default provider doesn't have.
- **External rule engines.** A consumer may already have a rule engine (Drools-style, OPA, custom DSL) and want to plug it in.
- **AI-assisted routing.** Classifying messages with an LLM and routing based on the classification is a custom-provider use case — and one ATAMO is well-suited to.

Custom providers are first-class. The default provider has no special privileges; it is just one provider that ships in the box.

## Multiple providers

Multiple providers can be registered simultaneously. When a message arrives, every provider evaluates it; the hub aggregates all the (agent, shaped-message) pairs from all providers.

This means a consumer can compose routing logic from multiple sources. The default provider can handle "obvious" rules; a custom provider can handle tenant-specific rules; a fan-out provider can route every message to a content-storing agent regardless of other rules. They don't conflict — they accumulate.

What happens when two providers produce overlapping results (both want to enqueue the same message into the same agent's inbox)? The hub deduplicates: each (agent, shaped-message) pair is enqueued once, even if multiple providers nominated it. This is intentional. Providers should not have to coordinate to avoid stepping on each other.

What happens when two providers produce contradictory results (one says enqueue, another says don't)? Don't is not expressible in the current contract — providers can only nominate enqueues, not veto them. Vetoes belong to the Governor's policy layer, not to providers.

## Where rules live

Rules are configuration, not code (usually). The provider is code; the rules it applies are configuration.

For the default provider, rules are declared in C# at registration time, which means they are technically code but feel like configuration:

```csharp
hubBuilder.AddRoutingRule(r => r
    .When<InboundEmail>()
    .RouteTo("triage"));
```

For custom providers, rules can live anywhere the provider chooses to read them from — a database, a YAML file, an external service, an LLM's output. The provider's job is to interpret rules; the hub's job is to ask the provider.

This separation is what makes user-submitted rules (a future capability flagged in [open questions](../open-questions.md)) possible without rebuilding the substrate.

## Open questions

- **Provider ordering.** When multiple providers run, in what order? Probably registration order, but does that matter? Open whether later providers can see earlier providers' results.
- **Performance at scale.** Every provider runs for every message. A consumer with ten providers and high throughput pays the cost of all ten on every message. Whether the hub should support some kind of pre-filtering ("only call provider X for messages of type Y") is undecided.
- **Provider failures.** If a provider throws an exception, does the message route based on the surviving providers' results? Does the message go to a dead-letter? Does the hub retry the provider? Probably "skip the failing provider and route from the rest, record the failure with the Governor," but worth confirming.
- **User-submitted providers in multi-tenant deployments.** A tenant supplying their own provider implementation raises all the sandboxing questions. Recorded in [open-questions.md](../open-questions.md).

## References

- [Hub](hub.md) — what calls into routing rule providers.
- [Agent](agent.md) — what providers route messages to.
- [Governor](governor.md) — where policy vetoes live, separate from provider logic.
- [Open questions](../open-questions.md) — sandbox/UAT for user-submitted rules.
