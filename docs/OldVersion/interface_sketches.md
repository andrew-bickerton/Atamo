## ATAMO Interface & Model Sketches

This document contains initial C# API sketches for the core Hub components and message models. These sketches are intended to capture the public surface and flow; they will be refined during implementation.

### Core message models
```csharp
public record MessageMetadata(string TenantId, string OriginatingUser, DateTimeOffset CreatedAt);

public record EventMessage
{
    public string EventType { get; init; }
    public string CorrelationId { get; init; }
    public MessageMetadata Metadata { get; init; }
    public IDictionary<string, object> Payload { get; init; }
    public string? DedupeKey { get; init; }
}

public record ActionMessage
{
    public string ActionId { get; init; }
    public string AgentId { get; init; }
    public string ParentEventKey { get; init; }
    public IDictionary<string, object> Payload { get; init; }
    public DeliveryOptions Delivery { get; init; }
}

public record DeliveryOptions
{
    public int MaxRetries { get; init; } = 3;
    public TimeSpan RetryBackoff { get; init; } = TimeSpan.FromSeconds(2);
    public bool RequireIdempotency { get; init; } = true;
}

public record EventKey(string Key);
public record ActionKey(string Key);

public record ActionProgress(string ActionKey, string Status, string? Details, DateTimeOffset Timestamp);
```

### IHubReceiver (ingress surface)
```csharp
public interface IHubReceiver
{
    /// Submit an event (fire-and-forget)
    Task<EventKey> SubmitEventAsync(EventMessage message, CancellationToken cancellationToken = default);

    /// Submit a request that expects streaming results; returns an EventKey and optionally a subscription id
    Task<EventKey> SubmitRequestAsync(EventMessage message, bool enableStreaming = false, CancellationToken cancellationToken = default);

    /// Optionally let external clients register a callback/subscriber for updates on an EventKey
    Task RegisterSubscriberAsync(EventKey key, Func<ActionProgress, Task> callback, CancellationToken cancellationToken = default);
}
```

### IHubControl (control-plane: registration, health, admin)
```csharp
public interface IHubControl
{
    Task RegisterAgentAsync(AgentDescriptor descriptor, CancellationToken cancellationToken = default);
    Task UnregisterAgentAsync(string agentId, CancellationToken cancellationToken = default);

    Task RegisterConfigProviderAsync(ConfigProviderDescriptor descriptor, CancellationToken cancellationToken = default);
    Task<IEnumerable<AgentDescriptor>> ListAgentsAsync(CancellationToken cancellationToken = default);

    Task<HubHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}

public record AgentDescriptor(string AgentId, string Description, IDictionary<string, string> Metadata);
public record ConfigProviderDescriptor(string ProviderId, string AssemblyName, string TypeName, IDictionary<string, string> Metadata);
public record HubHealth(bool IsHealthy, IDictionary<string, string> Details);
```

### IAgent (agent plug-in contract)
```csharp
public interface IAgent
{
    /// Execute a single action (complete when action finished)
    Task<ExecutionResult> ExecuteAsync(ActionMessage action, CancellationToken cancellationToken = default);

    /// Execute an action that streams progress/results back to the Hub
    IAsyncEnumerable<ActionProgress> ExecuteStreamingAsync(ActionMessage action, CancellationToken cancellationToken = default);
}

public record ExecutionResult(bool Success, string? Message, IDictionary<string, object>? Output = null);
```

### IConfigProvider (rule provider contract)
```csharp
public interface IConfigProvider
{
    /// Given an incoming event, return zero-or-more actions to be executed.
    /// Implementations must be resilient and idempotent where possible.
    Task<IEnumerable<ActionMessage>> EvaluateAsync(EventMessage evt, CancellationToken cancellationToken = default);
}
```

### Telemetry & Governor hooks
- Telemetry should expose structured events with: EventKey, ActionKey, timestamps for each stage, duration and result.
- Governor subscribes to telemetry and uses configured thresholds to raise alerts or take automated remediation.

### Notes & design considerations
- Recursive protection: `Router` and `AgentManager` should include origin filtering to avoid sending an Event back to its originator.
- Idempotency: `ActionMessage` should include a dedupe or idempotency key where agents must implement idempotent behavior when requested.
- Security: registration APIs (`IHubControl`) must require admin credentials and validation; uploaded config provider assemblies must be audited and permissioned.

---
End of API sketches. If you'd like, I can now scaffold the solution and create the initial C# projects with these interfaces and a minimal in-memory implementation for the Hub.
