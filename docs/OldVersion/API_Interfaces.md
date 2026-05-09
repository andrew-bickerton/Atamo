# ATAMO API & Interface Details

This document describes the main interfaces and API contracts for ATAMO, with usage notes and links to example code.

## IHubReceiver
- **Purpose:** Entry point for submitting events and requests to the hub.
- **Methods:**
  - `Task<EventKey> SubmitEventAsync(EventMessage message, CancellationToken cancellationToken = default)`
    - Submits a fire-and-forget event.
    - Example: See `MainForm.cs` in WinForms demo, `SubmitEventAsync` usage.
  - `Task<EventKey> SubmitRequestAsync(EventMessage message, bool enableStreaming = false, CancellationToken cancellationToken = default)`
    - Submits a request expecting streaming results.
  - `Task RegisterSubscriberAsync(EventKey key, Func<ActionProgress, Task> callback, CancellationToken cancellationToken = default)`
    - Registers a callback for progress updates.
    - Example: See `MainForm.cs`, subscriber registration for progress display.

## IHubControl
- **Purpose:** Admin/control interface for agent and provider registration, health, and monitoring.
- **Methods:**
  - `Task RegisterAgentAsync(AgentDescriptor descriptor, CancellationToken cancellationToken = default)`
  - `Task UnregisterAgentAsync(string agentId, CancellationToken cancellationToken = default)`
  - `Task RegisterConfigProviderAsync(ConfigProviderDescriptor descriptor, CancellationToken cancellationToken = default)`
  - `Task<IEnumerable<AgentDescriptor>> ListAgentsAsync(CancellationToken cancellationToken = default)`
  - `Task<HubHealth> GetHealthAsync(CancellationToken cancellationToken = default)`

## IAgent
- **Purpose:** Agent plug-in contract for executing actions.
- **Methods:**
  - `Task<ExecutionResult> ExecuteAsync(ActionMessage action, CancellationToken cancellationToken = default)`
  - `IAsyncEnumerable<ActionProgress> ExecuteStreamingAsync(ActionMessage action, CancellationToken cancellationToken = default)`
- **Example:** See `SampleAgent.cs` in `Atamo.Agents.Samples`.

## IConfigProvider
- **Purpose:** Rule provider contract for mapping events to actions.
- **Methods:**
  - `Task<IEnumerable<ActionMessage>> EvaluateAsync(EventMessage evt, CancellationToken cancellationToken = default)`

## Message Models
- `EventMessage`, `ActionMessage`, `ActionProgress`, `AgentDescriptor`, `ConfigProviderDescriptor`, `HubHealth`, `ExecutionResult`
- See `ModelsAndInterfaces.cs` in `Atamo.SDK` for definitions.

---
For practical walkthroughs and code examples, see [WinFormsDemo/MainForm.cs](../src/Atamo.WinFormsDemo/MainForm.cs).
