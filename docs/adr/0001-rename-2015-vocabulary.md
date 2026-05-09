# 0001. Rename 2015 component vocabulary

- **Status:** Accepted
- **Date:** 2026-04-28

## Contents

- [Context](#context)
- [Decision](#decision)
- [Alternatives considered](#alternatives-considered)
- [Consequences](#consequences)
- [References](#references)

## Context

ATAMO was originally designed in 2015. The original component vocabulary (Controller, EventProvider, EventType, ActionType, ConfigurationProvider, ConfigurationRule) was reasonable for its time but no longer reads well in 2026:

- _Controller_ collides with the dominant meaning in ASP.NET MVC and Web API, where "controller" is a specific pattern with established expectations. Reusing the term in a non-MVC context creates friction every time a .NET developer encounters it.
- _EventProvider_ suggests a system focused on event distribution, when the role is broader: it covers anything that injects messages into the hub from outside, including HTTP requests, CLI commands, and timer-driven sources. The narrower name distorts the concept.
- _EventType_ and _ActionType_ as separate hierarchies imply a structural distinction between events and actions. In practice, both are messages with different semantics, and modeling them as separate types complicates the routing layer for no benefit.
- _ConfigurationProvider_ and _ConfigurationRule_ suggest application configuration (`appsettings.json`, `IConfiguration`), not message routing. The 2015 names predate the convention, but it is now strongly established.

Renaming public types after release is painful. The right time to settle the vocabulary is before any code is written that exposes these names through a public API.

This ADR resolves the "Naming" entry in the design's open questions.

## Decision

The following renames are adopted as the canonical vocabulary for ATAMO:

| 2015 name | 2026 name |
|---|---|
| Controller | Host |
| EventProvider | Source |
| EventType | (removed; collapsed into Message type) |
| ActionType | (removed; collapsed into Message type) |
| ConfigurationProvider | Routing rule provider |
| ConfigurationRule | Routing rule |

The following 2015 names are retained:

- **Hub** — central enough and unambiguous enough that a rename would lose more than it gained.
- **Governor** — distinctive, well-fitting for the audit/policy/telemetry role, no equivalent term in established .NET vocabulary.

The following names are new in the 2026 design:

- **Agent** — a role within the system. Distinct from the 2015 use, which conflated the role with specific implementations.
- **Principal** — the identity on whose behalf a message is processed.
- **Message** — the unified term for what flows through the hub. Subsumes events, actions, requests, and responses, distinguished by their semantics rather than by class hierarchy.

The Source/Agent role distinction itself is significant enough to warrant its own ADR; it is recorded as ADR 0002.

## Alternatives considered

**Keep the 2015 vocabulary as-is.** Rejected because the friction it creates for new contributors compounds over time and is cheap to avoid before any public API exists.

**Rename more aggressively.** Some candidate alternatives — _Pipeline_ instead of Hub, _Channel_ instead of Source, _Handler_ instead of Agent — were considered and rejected because each collides with stronger established meanings in the .NET ecosystem (`System.IO.Pipelines`, `System.Threading.Channels`, `IRequestHandler<T>`). The retained and new names were chosen partly because they do _not_ collide.

**Defer the decision until first code lands.** Rejected because the design docs already use these terms throughout. Either the docs commit to a vocabulary or they keep refactoring it; committing now is cheaper.

## Consequences

**Easier:**

- Design docs and code can use a consistent vocabulary from the start, with no "see also: legacy term X" footnotes.
- New contributors arriving at the codebase encounter names that match what they describe, without needing to translate from 2015 conventions.
- The collapse of EventType and ActionType into a single Message concept simplifies routing — rules match on message shape and semantics, not on which hierarchy a type belongs to.

**Harder:**

- Anyone returning to the project from the 2015 codebase needs to learn the new vocabulary. This cost is one-time and small relative to the ongoing benefit.
- External references to ATAMO from before this ADR (issue tracker history, prior conversations, the original 2015 documentation) will use the old names. A glossary entry mapping old → new is worth adding to the design docs at some point.

**New questions opened:**

- None significant. The Source/Agent role distinction is the next decision, recorded in ADR 0002.

## References

- [Architecture overview](../design/architecture.md) — uses the new vocabulary throughout.
- [Component pages](../design/components/) — each rename in detail on the relevant page.
- [Open questions](../design/open-questions.md) — the naming entry that this ADR resolved.
- [ADR 0002](0002-source-and-agent-as-roles.md) — the Source/Agent role distinction that this renaming makes possible.
