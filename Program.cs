using System;

namespace trial
{
    class Program
    {
        static void Main(string[] args)
        {
            trial.Source.Pull.TextFile sourceFile = new trial.Source.Pull.TextFile("Test", "Test123");
            trial.Source.IPosition currPosition;

            // from the last 'accessed' position - pull in the next 10 rows
            // if first used sets to start of source
            currPosition = sourceFile.Pull(10);
            Console.WriteLine(sourceFile.Pop());

            // pull from specific position, by default does not override the last accessed position
            sourceFile.Pull(new trial.Source.RowNumber(10),1);
            Console.WriteLine(sourceFile.Pop());

            // pull all data from current position till end of file
            sourceFile.Pull();
            Console.WriteLine(sourceFile.Pop());

            Console.WriteLine("Hello World!");
        }
    }
}
