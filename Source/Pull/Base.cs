using System;
using trial.Source.Pull;
using trial.Source;

namespace trial.Source.Pull
{

    /*
    This class is going to be the base class for the Pull Source types
    Will include a basic implementation of a holding queue
     */
    abstract public class Base : IPull
    {
        private string _Name;
        private string _Key;

        public string Key
        { get { return _Key; } }
                
        public string Name
        {
            get { return _Name;}
            set { _Name = value;}
        }
        
        public string Pop()
        {
            throw new Exception("Not implemented");
        }

        public IPosition Pull()
        {
            // dummy call to pull the latest entries from the text file
            throw new Exception("Not implemented");
        }

        public IPosition Pull(int MaxToPull)
        {
            // dummy call to pull the latest entries from the text file
            throw new Exception("Not implemented");
        }

        public IPosition Pull(IPosition startPosition, int MaxToPull, bool OverrideLastPosition = false)
        {
            // dummy call to pull the latest entries from the text file
            throw new Exception("Not implemented");
        }

        public Base (string Name, string Key)
        {
            _Key = Key;
            _Name = Name;
        }
    }
}
