
# And Then A Miracle Occurs (Atamo)

## Project Overview

See [docs/Overview.md](docs/Overview.md) for a detailed introduction, architecture, and primary use cases.

---

# Quick Start
...existing content...

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
