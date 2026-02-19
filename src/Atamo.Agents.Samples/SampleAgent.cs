using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Atamo.SDK;

namespace Atamo.Agents.Samples
{
    public class SampleAgent : IAgent
    {
        public async Task<ExecutionResult> ExecuteAsync(ActionMessage action, CancellationToken cancellationToken = default)
        {
            // trivial sample: pretend to do work
            await Task.Delay(100, cancellationToken);
            return new ExecutionResult(true, $"Executed {action.ActionId}", new Dictionary<string, object> { ["ok"] = true });
        }

        public async IAsyncEnumerable<ActionProgress> ExecuteStreamingAsync(ActionMessage action, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < 3; i++)
            {
                await Task.Delay(200, cancellationToken);
                yield return new ActionProgress(action.ActionId, "Progress", $"step {i}", System.DateTimeOffset.UtcNow);
            }
        }
    }
}
