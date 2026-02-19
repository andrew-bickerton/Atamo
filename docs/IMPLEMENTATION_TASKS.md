# IMPLEMENTATION TASKS
## Purpose
This document captures the architecture, design decisions and an actionable task list to restart development of ATAMO (And Then A Miracle Occurs) — a highly configurable, auditable, smart messaging hub that can be embedded into a .NET Core app or run as a standalone service with a REST API.

## High-level goals
- Provide an embeddable core engine (library) to add dynamic routing and async processing to host apps.
- Provide a standalone service mode exposing a REST API for integrations and UI.
- Support fire-and-forget events, request/response and streaming responses.
- Allow pluggable Agents and Configuration Providers for routing and actions.
- Provide robust auditing, governance and observability.

## Non-functional requirements
- Async-first, scalable design (thread pools/task-based model).
- Pluggable persistence (in-memory, SQL, blob) and transport (in-memory, RabbitMQ, Azure Service Bus).
- Configurable delivery semantics (at-most-once, at-least-once best-effort) with retries and dead-lettering.
- Multi-tenant and secure by design (authn/authz for agent registration and rule uploads).

## Core components (overview)
- Hub (core engine/library)
   - IHubReceiver: public interface for submitting Event/Request messages.
   - IHubControl: control-plane (register agents, providers, health, metrics).
   - Telemetry/IEventLogger: pluggable telemetry & audit sinks.
   - EventStack/Queue: internal queue(s) for incoming events.

- Agents
   - Implementers of an Agent interface/SDK that perform actions (email, DB write, HTTP call, long-running stream)
   - Each agent runs in its own execution pool; supports async work and streaming responses.

- Configuration Providers & Rules
   - Providers implement an Evaluate(event) → IEnumerable<ActionMessage>

   - Default provider: simple mapping from EventType and metadata to Agent + ActionTemplate
   - Support uploading custom rule DLLs with strict auditing and permission checks

- Controller / Host
   - Hosts the Hub when embedded or runs the standalone service.
   - Responsible for agent registration, tenant config, and receiving telemetry for auditing.

- Governor
   - System component that monitors health, audits events/actions, enforces retention and alerting levels.

## Message model
- EventMessage: contains EventType, OriginatingUser, Payload, CorrelationId, EventKey, metadata
- ActionMessage: produced by config providers; includes AgentId, ActionKey, payload, parent EventKey
- Response/Status messages: emitted by Agents and the Hub describing progress, deltas and completion

## Operational flow (derived from operational_flow.md)
1. Client/EventProvider submits Event to Hub (gets EventKey)
2. Hub logs event and enqueues it on EventStack
3. Router evaluates EventType + OriginatingUser and pushes evaluation tasks to registered config providers
4. Each config provider returns zero-or-more ActionMessages
5. Hub records requested Actions and dispatches them to assigned Agents
6. Agents perform work and post progress/status/results back to Hub (referencing EventKey + ActionKey)
7. Hub persists audit records and optionally streams events back to interested subscribers

Notes:
- Every stage appends timestamps for performance/latency analysis.
- Recursive protection: Hub tracks original originator and suppresses sending the original Event back to the origin agent.

## Persistence and caching
- Short-term cache for disconnected clients (EventProvider-side cache) and hub-side cache for multi-request dedupe.
- Message store for audit/retention (pluggable backing store - SQL, Cosmos, blob for payloads).
- Dead-letter store for failed actions beyond retry policy.

## Delivery guarantees & retries
- Configurable retry/backoff per Agent and per Action.
- Idempotency support via action keys / dedupe keys.

## Security & multi-tenancy
- AuthN/AuthZ for API access, agent registration and rule uploads.
- Tenant-aware routing and config separation.
- Audit logging for all rule uploads, agent changes and action executions.

## Observability
- Expose metrics (Prometheus), structured logs, and distributed traces (OpenTelemetry).
- Controller UI / API endpoints for inflight messages, latency and agent health.

## Extensibility
- Agent SDK and samples (HTTP, SQL, Email, Streaming agent).
- Config rule provider SDK and sample provider (default pass-through + example DLL upload pattern).

## Testing strategy
- Unit tests for core Router, Hub, AgentManager and Config evaluation.
- Integration tests with an in-memory transport and a local SQL store.
- E2E tests for standalone REST API with simulated Agents.

## Implementation milestones & tasks
1. Project scaffolding (repo structure, solution, CI)
   - Create solution with projects: Atamo.Hub (core lib), Atamo.Service (standalone REST host), Atamo.Agents.Samples, Atamo.SDK (agent + provider interfaces), Atamo.Persistence (abstractions)
2. Core message model & interfaces
   - Implement EventMessage, ActionMessage, IHubReceiver, IHubControl, IAgent, IConfigProvider
3. In-memory flow proof-of-concept
   - Implement in-memory EventStack, Router wiring to default config provider, AgentManager, simple Agent sample
   - Verify fire-and-forget and single-agent request/response
4. Persistence + audit
   - Add pluggable persistence abstraction and SQL implementation for message/audit store
5. Agent SDK & samples
   - Build HTTP and SQL agent samples; streaming agent sample
6. Standalone REST host
   - Implement minimal REST API to submit events/requests, query status, and receive telemetry
7. Config providers & rule upload
   - Implement default provider, plugin loader for custom rule DLLs with sandboxing/permissions
8. Governor & telemetry
   - Implement governor monitors, retention policies, alerting hooks and metrics export
9. Security, multi-tenancy, and production hardening
   - Add auth, tenant isolation, config encryption, and deployment guidance
10. Tests, docs, and examples
   - Populate user guides (see docs/planned_docs.md), sample apps and CI pipelines

## Immediate next steps (what I'll do now)
- Finalise this implementation tasks doc and review with you.
- Prepare the project scaffolding template (if you want I can scaffold the solution and initial projects).

## Files updated
- Updated: [docs/IMPLEMENTATION_TASKS.md](docs/IMPLEMENTATION_TASKS.md)

# IMPLEMENTATION_TASKS.md

## Documentation Tasks

1. **Gather Detailed Requirements**
   - Clarify the specific features and functionalities required for each scenario.
   - Determine any additional constraints or requirements that were not initially mentioned.

2. **Design Core Components**
   - Define the architecture of the core engine, including its components and their interactions.
   - Design the Governor system to monitor/audit the system with configurable levels of audit.

3. **Create Documentation Structure**
   - Outline the structure of the documentation, including sections for installation, configuration, usage, and troubleshooting.
   - Create a high-level design document that captures the overall architecture and key components.

4. **Review and Refine Documentation**
   - Review the documentation and implementation tasks with the user for feedback.
   - Make any necessary adjustments based on the feedback received.