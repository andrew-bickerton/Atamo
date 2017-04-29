using System;
using System.Collections.Concurrent;

namespace Atamo
{
    public interface IAtamoDestination<T> 
    {
        void RequestStart();
        void RequestStop();

        void QueueBatch(ConcurrentQueue<T> assignedBatch);
        
        // need a way of receiving a response back (when is the batch complete,
        // what was the result?  any failures?  any retries?)

    }
}