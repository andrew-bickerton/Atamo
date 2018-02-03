using System;
using System.Collections;

namespace trial.Source
{
    public interface ISource
    {
        string Name {get; set;}
        string Key {get;}
        string Pop();
    }
    
}