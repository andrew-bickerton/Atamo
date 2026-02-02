# And Then A Miracle Occurs (Atamo)

## Overview

Atamo is an auditable, pluggable, self-hostable service designed to support asynchronous processing of messages. It provides flexibility for both unidirectional and bidirectional message flows:

- **Unidirectional**: Clients send messages into Atamo, and agents process these events.
- **Bidirectional**: Clients send a request message and receive a `requestId`. Agents process the request, and their responses are made available for the client to retrieve.

### Key Features

- **Message Routing**: Messages are routed to agents based on configurable rules.
- **Auditing**: Tracks the entire lifecycle of a message, including its origin, routing, and processing.
- **Extensibility**: Every component of Atamo is extendable with default implementations provided.
- **Versatility**: Can be used as:
  - A library for asynchronous, multi-threaded processing.
  - A RESTful API for rule-driven event processing.
  - A user-driven agentic interface.

---

## Use Cases

### Primary Use Cases

1. **Log Events**:
   - Rules determine which actions fire based on the event.
   - Sub-cases:
     - **Fire and Forget**: Controller monitors state/failures; the client does not maintain a connection.
     - **Fire and Monitor**: Both the controller and client receive telemetry about the event and actions.

2. **Request and Response**:
   - Retrieve details from multiple sources and return them to the requestor.
   - Sub-cases:
     - **Semi-Static Data**: Client disconnects after receiving the initial response.
     - **Live Updates**: Client remains connected to receive updates.
     - **Deferred Retrieval**: Client sends a request and checks back later for results.

---

## Components

### Core Components

1. **Hub**:
   - Central engine where all settings are applied.
   - Interfaces:
     - `ITelemetry`: Notifies the controller and client about hub activity.
     - `IResponse`: Handles client-specific responses.
     - `IHubControl`: Interface for the controller to interact with the hub.
     - `IHubReceiver`: Interface for clients to submit requests.

2. **Controller**:
   - Hosts the hub and manages agents, event providers, and configuration providers.
   - Monitors hub state and performance, providing telemetry for auditing and alerting.

3. **Event Providers**:
   - Interface for clients to interact with the hub.
   - Responsibilities:
     - Register new event types.
     - Package and submit event messages.
     - Manage client responses, including support for disconnected clients.

4. **Agents**:
   - Perform actions such as sending emails or calling APIs.
   - Can be generic, with metadata defining their behavior (e.g., REST API calls, database operations).

5. **Configuration Providers**:
   - Define rules for routing events to agents.
   - Default provider links event types and users to agents.
   - Custom providers can implement complex rules.

6. **Configuration Rules**:
   - Link requests/events to actions.
   - Define metadata for filtering, agent selection, and action message creation.

7. **Users**:
   - Each user has tokens for agent providers.
   - Supports group management for shared configuration rules.

---

## Guides

### Getting Started

1. **Add Atamo to a WinForms App**:
   - Implement the controller to manage the hub.

2. **Make Data Loading Asynchronous**:
   - Create an agent to handle requests.
   - Display telemetry received by the controller.

3. **Load Data from Multiple Sources**:
   - Configure multiple agents to handle the same request.

4. **Set Up a Data Feed**:
   - Modify an agent to provide continuous updates.
   - Allow clients to disconnect when no longer interested in results.

5. **Log an Event**:
   - Register an event type and observe its behavior.

6. **Configure Actions**:
   - Add actions to agents and configure them to fire when events occur.

7. **Handle Failures**:
   - Simulate agent failures and demonstrate retry attempts.
   - Configure alerts for retries exceeding thresholds.

8. **Create Custom Configuration Providers**:
   - Implement a provider for complex rules and test it at runtime.

---

## Advanced Features

### Extensibility

- Users can submit their own mapping DLLs or configuration rules.
- Strict auditing and permission handling ensure security.
- Mocking and testing tools are built-in to verify new rules and components.

### Caching

- Two levels of caching:
  1. Event provider cache for disconnected clients.
  2. Hub agent manager cache to optimize repeated requests.

---

## Future Enhancements

- Support for user-submitted configuration providers.
- Enhanced auditing and runtime verification for custom rules.
- Improved scalability for large-scale deployments.
