# ATAMO Core Components

This document describes the main components of ATAMO, their responsibilities, and how they interact.

## Hub (Core Engine)
- Central message router and processor.
- Exposes interfaces:
  - `IHubReceiver`: Accepts events and requests from clients/event providers.
  - `IHubControl`: Admin/control interface for agent and provider registration, health, and monitoring.
  - `Itelemetry`: Emits structured telemetry and audit events.
  - `IResponse`: Handles responses to clients (request/response, streaming).
- Maintains event queues, routes messages to config providers and agents, tracks message state.

## Controller
- Hosts the Hub (embedded or as a service).
- Registers agents, event providers, and configuration providers.
- Receives telemetry for audit, performance, and state monitoring.
- Manages stateless/stateful operation modes.
- Provides monitoring and alerting (latency, performance, throughput).

## Event Providers
- Client-facing adapters for submitting events/requests.
- Register new event types on the hub.
- Package event messages for submission.
- Manage responses to clients, including disconnected scenarios.

## Event Types & Action Types
- **Event Types**: Templates for messages the hub can receive; used for routing and rule evaluation.
- **Action Types**: Templates for actions agents can perform (email, DB write, HTTP call, etc).

## Agents
- Components that perform work (external calls, impersonation, etc).
- Can be generic (HTTP, SQL, etc) or custom.
- Registration includes metadata, action templates, and initialization.

## Configuration Providers & Rules
- **Configuration Providers**: Map events to actions based on rules.
  - Default provider: simple mapping (event type + user → agent/action).
  - Custom providers: support complex rules, filtering, and action generation.
- **Configuration Rules**: Link requests/events to actions; can match multiple rules per event.

## Users & Security
- Each user has tokens for agent providers.
- Group/tenant management for rule assignment.
- Security: safe token storage, permission checks, auditing for rule uploads.

---
For API details, see [interface_sketches.md](interface_sketches.md).
For guides and examples, see [planned_docs.md](planned_docs.md).
