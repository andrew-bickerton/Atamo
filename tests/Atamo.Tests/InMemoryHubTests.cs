using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atamo.Hub;
using Atamo.SDK;
using Xunit;

namespace Atamo.Tests
{
    public class InMemoryHubTests
    {
        [Fact]
        public async Task RegisterAgentAndSubmitEvent_EmitsProgressToSubscriber()
        {
            using var hub = new InMemoryHub();

            var agent = new AgentDescriptor("agent-1", "sample agent", new Dictionary<string, string>());
            await hub.RegisterAgentAsync(agent);

            var tcs = new TaskCompletionSource<ActionProgress?>(TaskCreationOptions.RunContinuationsAsynchronously);

            await hub.RegisterSubscriberAsync(new EventKey("unused"), progress =>
            {
                tcs.TrySetResult(progress);
                return Task.CompletedTask;
            });

            var evt = new EventMessage
            {
                EventType = "test.event",
                CorrelationId = string.Empty,
                Metadata = new MessageMetadata("t1", "u1", DateTimeOffset.UtcNow),
                Payload = new Dictionary<string, object>()
            };

            await hub.SubmitEventAsync(evt);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            Assert.True(tcs.Task.IsCompleted, "Expected a progress message from hub within timeout");
            var progress = await tcs.Task;
            Assert.NotNull(progress);
            Assert.Equal("Dispatched", progress!.Status);
        }
    }
}
