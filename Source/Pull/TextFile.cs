using System;
using trial.Source.Pull;
using trial.Source;

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

        public IPosition Pull()
        {
            // dummy call to pull the latest entries from the text file
            _TestPull = "Full Pull";
            return new RowNumber(1);
        }

        public IPosition Pull(int MaxToPull)
        {
            // dummy call to pull the latest entries from the text file
            _TestPull = "Partial Pull at end";
            return new RowNumber(1);
        }

        public IPosition Pull(IPosition startPosition, int MaxToPull, bool OverrideLastPosition = false)
        {
            // dummy call to pull the latest entries from the text file
            _TestPull = "Partial Pull at position";
            return new RowNumber(1);
        }

        public TextFile (string Name, string Key)
        {
            _Key = Key;
            _Name = Name;
        }
    }
}
