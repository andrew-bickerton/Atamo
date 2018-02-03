using System;
using trial.Source.Pull;

namespace trial.Source.Pull
{
    public class TextFile : IPull
    {
        private string _Name;
        private string _Key;
        private string _TestPull;


        public string Key
        { get { return _Key;} }
                
        public string Name
        {
            get { return _Name;}
            set { _Name = value;}
        }
        
        public string Pop()
        {
            return _TestPull;
        }

        public int Pull()
        {
            // dummy call to pull the latest entries from the text file
            _TestPull = "Full Pull";
            return 1;
        }

        public int Pull(int MaxToPull)
        {
            // dummy call to pull the latest entries from the text file
            _TestPull = "Partial Pull at end";
            return 1;
        }

        public int Pull(int startPosition, int MaxToPull)
        {
            // dummy call to pull the latest entries from the text file
            _TestPull = "Partial Pull at position";
            return 1;
        }

        public TextFile (string Name, string Key)
        {
            _Key = Key;
            _Name = Name;
        }
    }
}
