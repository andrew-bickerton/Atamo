using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Atamo.Agents.Samples;
using Atamo.Hub;
using Atamo.SDK;
using Xunit;

namespace Atamo.Tests
{
    public class AdditionalHubTests
    {
        [Fact]
        public async Task Register_List_Unregister_Agents_Work()
        {
            using var hub = new InMemoryHub();

            var a1 = new AgentDescriptor("a1", "first", new Dictionary<string, string>());
            var a2 = new AgentDescriptor("a2", "second", new Dictionary<string, string>());

            await hub.RegisterAgentAsync(a1);
            await hub.RegisterAgentAsync(a2);

            var agents = (await hub.ListAgentsAsync()).ToList();
            Assert.Contains(agents, ad => ad.AgentId == "a1");
            Assert.Contains(agents, ad => ad.AgentId == "a2");

            await hub.UnregisterAgentAsync("a1");
            var agents2 = (await hub.ListAgentsAsync()).ToList();
            Assert.DoesNotContain(agents2, ad => ad.AgentId == "a1");
            Assert.Contains(agents2, ad => ad.AgentId == "a2");
        }

        [Fact]
        public async Task GetHealth_Includes_AgentsCount()
        {
            using var hub = new InMemoryHub();
            var a1 = new AgentDescriptor("a1", "first", new Dictionary<string, string>());
            await hub.RegisterAgentAsync(a1);

            var h = await hub.GetHealthAsync();
            Assert.True(h.IsHealthy);
            Assert.Equal("1", h.Details["AgentsRegistered"]);
        }

        [Fact]
        public async Task MultiAgentDispatch_EmitsProgressForAllAgents()
        {
            using var hub = new InMemoryHub();
            await hub.RegisterAgentAsync(new AgentDescriptor("ag1", "one", new Dictionary<string, string>()));
            await hub.RegisterAgentAsync(new AgentDescriptor("ag2", "two", new Dictionary<string, string>()));

            var bag = new ConcurrentBag<ActionProgress>();
            await hub.RegisterSubscriberAsync(new EventKey("unused"), progress =>
            {
                bag.Add(progress);
                return Task.CompletedTask;
            });

            var evt = new EventMessage
            {
                EventType = "multi.event",
                CorrelationId = string.Empty,
                Metadata = new MessageMetadata("t", "u", DateTimeOffset.UtcNow),
                Payload = new Dictionary<string, object>()
            };

            await hub.SubmitEventAsync(evt);

            // wait for up to 2s for dispatch
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (bag.Count < 2 && sw.ElapsedMilliseconds < 2000) await Task.Delay(50);

            Assert.True(bag.Count >= 2, "Expected at least two progress messages for two agents");
        }

        [Fact]
        public async Task SampleAgent_Execute_ReturnsSuccess()
        {
            var agent = new SampleAgent();
            var action = new ActionMessage
            {
                ActionId = "act1",
                AgentId = "sample",
                ParentEventKey = "evt1",
                Payload = new Dictionary<string, object>()
            };

            var res = await agent.ExecuteAsync(action);
            Assert.True(res.Success);
            Assert.Contains("Executed", res.Message ?? string.Empty);
        }

        [Fact]
        public async Task SampleAgent_ExecuteStreaming_YieldsProgress()
        {
            var agent = new SampleAgent();
            var action = new ActionMessage
            {
                ActionId = "act2",
                AgentId = "sample",
                ParentEventKey = "evt2",
                Payload = new Dictionary<string, object>()
            };

            var list = new List<ActionProgress>();
            await foreach (var p in agent.ExecuteStreamingAsync(action))
            {
                list.Add(p);
            }

            Assert.Equal(3, list.Count);
            Assert.All(list, p => Assert.Equal("Progress", p.Status));
        }
    }
}
