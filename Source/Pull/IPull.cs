using System;
using System.Collections.Concurrent;
using trial.Source;

namespace trial.Source.Pull
{
    public interface IPull : ISource
    {
        int Pull();
        int Pull(int MaxToPull);
        int Pull(int StartPosition, int MaxToPull);
    }
    
}