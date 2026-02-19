# ATAMO Overview

ATAMO (And Then A Miracle Occurs) is a highly configurable, auditable, smart messaging hub designed to provide dynamic routing and asynchronous processing for modern applications. It can be embedded as a library in .NET Core apps or run as a standalone service with a REST API.

## Key Features
- Fire-and-forget event logging, with rules that trigger actions across multiple agents
- Request/response flows, including streaming updates and disconnected clients
- Pluggable agents and configuration providers for flexible routing and action execution
- Robust auditing, telemetry, and monitoring for operational visibility

## Primary Use Cases
1. **Log Event & Rule-driven Actions**
   - Fire-and-forget: Client posts an event, rules determine which agents act. Controller receives telemetry; client does not need to stay connected.
   - Fire-and-monitor: Client can receive/retrieve telemetry and action progress as events are processed.
2. **Request/Response & Search**
   - Request data from multiple sources; receive initial results and streaming updates.
   - Support for disconnected clients (results cached, can check back later).
   - Configurable keep-alive and retention for results.

## Usage Modes
- **Stateless mode:** Embedded in WinForms/web apps to enable async, multi-threaded, responsive workflows.
- **Stateful service mode:** Run as a web/service host to guarantee action delivery, audit, and monitoring.

---
For more details, see the [planned_docs.md](planned_docs.md) and architecture documentation.