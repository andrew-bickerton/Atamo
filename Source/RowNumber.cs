using System;
using trial.Source;

namespace trial.Source
{
    public class RowNumber : IPosition
    {
        private int _Position;

        public int Position
        {
            get { return _Position;}
            set { _Position = value;}
        }

        public RowNumber(int Position)
        {
            _Position = Position;
        }
    }
}
