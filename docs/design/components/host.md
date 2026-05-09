# Host

The host is the application that contains the hub. It owns configuration, registration, and lifetime. It is also the boundary between the embedded and standalone deployment shapes.

## Contents

- [Overview](#overview)
- [Responsibilities](#responsibilities)
- [Embedded vs standalone](#embedded-vs-standalone)
- [The standalone host](#the-standalone-host)
- [Lifecycle and shutdown](#lifecycle-and-shutdown)
- [Open questions](#open-questions)
- [References](#references)

## Overview

Every ATAMO deployment has a host. In the embedded case, the host is the consuming application — your ASP.NET Core service, your console app, your background worker. The hub is registered into the application's DI container and shares its lifetime. In the standalone case, the host is ATAMO's own service host: a thin wrapper around the embedded library that exposes the hub over HTTP and/or gRPC.

The host's role is small but important. It does the things that have to happen somewhere but don't belong inside the hub itself: configuration, registration, authentication, lifetime management.

## Responsibilities

The host:

- **Configures the hub.** Reads configuration (from `appsettings.json`, environment variables, or whatever .NET configuration providers the application uses) and applies it.
- **Registers components.** Sources, agents, routing rule providers, Governor sinks — all are registered through the host.
- **Owns lifetime.** Starts the hub when the application starts; signals shutdown when the application stops; gives sources and agents a chance to drain.
- **Authenticates sources.** When a source submits a message under a given principal, the host is responsible for verifying that the principal is who they say they are. ATAMO does not specify how — that depends on the deployment.
- **Provides DI scope.** Agents and sources are resolved through the application's DI container. Their dependencies are the application's responsibility.

The host does _not_:

- Make routing decisions (that's the hub).
- Implement agents or sources (those are application code).
- Enforce policy (that's the Governor).
- Persist messages (that's the inbox and the persistence layer).

The host is glue. It is where ATAMO meets the application's existing infrastructure.

## Embedded vs standalone

ATAMO is designed to run in two deployment shapes from the same codebase. The shapes are:

**Embedded** — referenced as a NuGet package, registered into the consuming application's DI container, sharing its process and lifetime. This is the default and the one the API ergonomics are tuned for.

**Standalone** — ATAMO's own service host, exposing the hub over HTTP and/or gRPC. Sources submit messages over the network; agents pull from their inboxes over the network.

The duality is real, not aspirational. The standalone host is implementable as a thin wrapper around the embedded library. If the standalone case needs primitives the embedded case does not have, that is a signal the embedded API is incomplete, not that the standalone case needs special treatment.

The inbox model is what makes the duality cheap. Because agents pull from inboxes rather than being dispatched into via function call, "agent on the other side of an HTTP connection" is a natural shape — the agent's pull operation just becomes a network request. See the [Inbox page](inbox.md) for the queue side of this story.

## The standalone host

The standalone host is a separate executable that consumers can deploy when:

- They want agents to live on different machines from the hub.
- They want non-.NET clients to interact with ATAMO over HTTP.
- They want a single shared hub serving multiple applications.
- They want clearer process boundaries for security or operational reasons.

It exposes:

- **Source endpoints.** HTTP and/or gRPC endpoints for submitting messages and (optionally) subscribing to responses.
- **Agent endpoints.** HTTP and/or gRPC endpoints for agents to register, pull from their inboxes, and submit responses.
- **Governor query endpoints.** HTTP and/or gRPC endpoints for querying the audit log (for operators, monitoring tools, and any consumer that needs to ask "what happened?" against the substrate's own metadata).
- **Health and operational endpoints.** Standard liveness, readiness, and metrics endpoints.

The standalone host is not a separate codebase. It is a project in this repository that depends on the embedded library and adds the network layer.

## Lifecycle and shutdown

The host's lifecycle:

1. **Start.** Configuration is read. Components are registered. The hub is initialized. Sources are started. Agents are made available to receive from their inboxes.
2. **Run.** Normal operation. Sources inject messages, agents process them, the Governor records everything.
3. **Shutdown signal.** Cancellation token is signalled (e.g. SIGTERM in a container, Ctrl-C locally).
4. **Drain.** Sources are signalled to stop. In-flight messages already submitted to the hub continue processing. Agents are given their lease window to complete in-flight work.
5. **Stop.** Components are disposed in reverse order of registration. The hub is shut down.

What survives a restart depends on the swap-point implementations:

- **Inbox state.** Survives if the inbox implementation is durable (the SQLite default is). In-flight leases that exceed their visibility timeout return to the queue.
- **Audit log.** Survives if the Governor sink is durable. The default SQLite sink is.
- **Correlation tracking.** Does _not_ survive a restart by default. A source that submitted a request and was waiting for a response will need to reconnect and re-subscribe, possibly using deferred-retrieval semantics if the response had already been emitted.

The host's drain behaviour is configurable but defaults to "wait reasonably, then force-stop." The default reasonable wait is the maximum visibility timeout in use, on the assumption that any agent currently processing a message will complete within its lease.

## Open questions

- **Configuration schema.** The exact shape of `appsettings.json` for ATAMO components is not yet decided. Likely follows .NET conventions (`Atamo:Hub`, `Atamo:Inbox`, etc.) but the keys and structure are TBD.
- **Wire protocol for the standalone host.** HTTP, gRPC, or both. Recorded in [open-questions.md](../open-questions.md).
- **Authentication for the standalone host.** How sources and agents authenticate themselves to the standalone host is undecided. Probably bearer tokens or mTLS, configurable per-deployment.
- **Multi-host clustering.** A single host is the v0 model. How multiple hosts coordinate (shared inbox, shared audit, leader election) is out of scope but worth flagging.

## References

- [Architecture overview](../architecture.md) — embedded vs standalone in the larger picture.
- [Hub](hub.md) — what the host hosts.
- [Inbox](inbox.md) — the queue model that makes embedded/standalone duality cheap.
- [Open questions](../open-questions.md) — wire protocol decision.
