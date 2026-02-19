using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Atamo.SDK;

namespace Atamo.Hub
{
    public class InMemoryHub : IHubReceiver, IHubControl, IDisposable
    {
        private readonly ConcurrentQueue<EventMessage> _queue = new();
        private readonly ConcurrentDictionary<string, AgentDescriptor> _agents = new();
        private readonly List<Func<ActionProgress, Task>> _globalSubscribers = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _worker;

        public InMemoryHub()
        {
            _worker = Task.Run(ProcessQueueAsync);
        }

        public Task<EventKey> SubmitEventAsync(EventMessage message, CancellationToken cancellationToken = default)
        {
            var key = new EventKey(Guid.NewGuid().ToString("D"));
            // attach correlation id if missing
            if (string.IsNullOrEmpty(message.CorrelationId))
            {
                message = message with { CorrelationId = key.Key };
            }
            _queue.Enqueue(message);
            return Task.FromResult(key);
        }

        public Task<EventKey> SubmitRequestAsync(EventMessage message, bool enableStreaming = false, CancellationToken cancellationToken = default)
        {
            // for POC treat same as SubmitEvent
            return SubmitEventAsync(message, cancellationToken);
        }

        public Task RegisterSubscriberAsync(EventKey key, Func<ActionProgress, Task> callback, CancellationToken cancellationToken = default)
        {
            // simple global subscriber support in POC
            _globalSubscribers.Add(callback);
            return Task.CompletedTask;
        }

        public Task RegisterAgentAsync(AgentDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            _agents[descriptor.AgentId] = descriptor;
            return Task.CompletedTask;
        }

        public Task UnregisterAgentAsync(string agentId, CancellationToken cancellationToken = default)
        {
            _agents.TryRemove(agentId, out _);
            return Task.CompletedTask;
        }

        public Task RegisterConfigProviderAsync(ConfigProviderDescriptor descriptor, CancellationToken cancellationToken = default)
        {
            // POC: not implemented
            return Task.CompletedTask;
        }

        public Task<IEnumerable<AgentDescriptor>> ListAgentsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<AgentDescriptor>>(_agents.Values.ToArray());
        }

        public Task<HubHealth> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            var health = new HubHealth(true, new Dictionary<string, string>
            {
                ["QueueLength"] = _queue.Count.ToString(),
                ["AgentsRegistered"] = _agents.Count.ToString()
            });
            return Task.FromResult(health);
        }

        private async Task ProcessQueueAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (_queue.TryDequeue(out var evt))
                    {
                        // Simple default: create one ActionMessage per registered agent
                        foreach (var agentId in _agents.Keys)
                        {
                            var action = new ActionMessage
                            {
                                ActionId = Guid.NewGuid().ToString("D"),
                                AgentId = agentId,
                                ParentEventKey = evt.CorrelationId ?? string.Empty,
                                Payload = new Dictionary<string, object> { ["eventType"] = evt.EventType }
                            };

                            // Emit a simple progress message to subscribers
                            var progress = new ActionProgress(action.ActionId, "Dispatched", $"Dispatched to {agentId}", DateTimeOffset.UtcNow);
                            var subscribers = _globalSubscribers.ToArray();
                            foreach (var sub in subscribers)
                            {
                                try { await sub(progress); } catch { /* swallow in POC */ }
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(200, _cts.Token);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(500); }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _worker.Wait(1000); } catch { }
            _cts.Dispose();
        }
    }
}
