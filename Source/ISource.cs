using System;
using System.Collections;

namespace trial.Source
{
    // need to define a Queue that the Source populates/can pop from that can be an interface type
    // also need to abstract out the 'message/body' that can enqueued/popped off that queue
     
    public interface ISource
    {
        string Name {get; set;}
        string Key {get;}
        string Pop();
    }
    
}