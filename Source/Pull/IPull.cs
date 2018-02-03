using System;
using System.Collections.Concurrent;
using trial.Source;

namespace trial.Source.Pull
{
    public interface IPull : ISource
    {
        IPosition Pull();
        IPosition Pull(int MaxToPull);
        IPosition Pull(IPosition StartPosition, int MaxToPull, bool OverrideLastPosition = false);
    }
    
}