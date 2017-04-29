using System;
using System.Collections.Concurrent;

namespace Atamo
{
    public interface IAtamoSource<T> 
    {
        ConcurrentQueue<T> GetBatch(long lastID, int batchSize);
        void BatchCompleted(ConcurrentQueue<T> batch);
        
        void Failure(T sourceMessage, Exception exception);
    }
}