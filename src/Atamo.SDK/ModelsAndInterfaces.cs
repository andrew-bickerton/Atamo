using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Atamo.SDK
{
    public record MessageMetadata(string TenantId, string OriginatingUser, System.DateTimeOffset CreatedAt);

    public record EventMessage
    {
        public string EventType { get; init; } = default!;
        public string CorrelationId { get; init; } = default!;
        public MessageMetadata Metadata { get; init; } = default!;
        public IDictionary<string, object> Payload { get; init; } = new Dictionary<string, object>();
        public string? DedupeKey { get; init; }
    }

    public record DeliveryOptions
    {
        public int MaxRetries { get; init; } = 3;
        public System.TimeSpan RetryBackoff { get; init; } = System.TimeSpan.FromSeconds(2);
        public bool RequireIdempotency { get; init; } = true;
    }

    public record ActionMessage
    {
        public string ActionId { get; init; } = default!;
        public string AgentId { get; init; } = default!;
        public string ParentEventKey { get; init; } = default!;
        public IDictionary<string, object> Payload { get; init; } = new Dictionary<string, object>();
        public DeliveryOptions Delivery { get; init; } = new DeliveryOptions();
    }

    public record EventKey(string Key);
    public record ActionKey(string Key);

    public record ActionProgress(string ActionKey, string Status, string? Details, System.DateTimeOffset Timestamp);

    public record AgentDescriptor(string AgentId, string Description, IDictionary<string, string> Metadata);
    public record ConfigProviderDescriptor(string ProviderId, string AssemblyName, string TypeName, IDictionary<string, string> Metadata);
    public record HubHealth(bool IsHealthy, IDictionary<string, string> Details);

    public record ExecutionResult(bool Success, string? Message, IDictionary<string, object>? Output = null);

    public interface IHubReceiver
    {
        Task<EventKey> SubmitEventAsync(EventMessage message, CancellationToken cancellationToken = default);
        Task<EventKey> SubmitRequestAsync(EventMessage message, bool enableStreaming = false, CancellationToken cancellationToken = default);
        Task RegisterSubscriberAsync(EventKey key, System.Func<ActionProgress, Task> callback, CancellationToken cancellationToken = default);
    }

    public interface IHubControl
    {
        Task RegisterAgentAsync(AgentDescriptor descriptor, CancellationToken cancellationToken = default);
        Task UnregisterAgentAsync(string agentId, CancellationToken cancellationToken = default);
        Task RegisterConfigProviderAsync(ConfigProviderDescriptor descriptor, CancellationToken cancellationToken = default);
        Task<System.Collections.Generic.IEnumerable<AgentDescriptor>> ListAgentsAsync(CancellationToken cancellationToken = default);
        Task<HubHealth> GetHealthAsync(CancellationToken cancellationToken = default);
    }

    public interface IAgent
    {
        Task<ExecutionResult> ExecuteAsync(ActionMessage action, CancellationToken cancellationToken = default);
        IAsyncEnumerable<ActionProgress> ExecuteStreamingAsync(ActionMessage action, CancellationToken cancellationToken = default);
    }

    public interface IConfigProvider
    {
        Task<System.Collections.Generic.IEnumerable<ActionMessage>> EvaluateAsync(EventMessage evt, CancellationToken cancellationToken = default);
    }
}
