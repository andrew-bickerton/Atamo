using System;

namespace trial
{
    class Program
    {
        static void Main(string[] args)
        {
            trial.Source.Pull.TextFile sourceFile = new trial.Source.Pull.TextFile("Test", "Test123");

            sourceFile.Pull();
            Console.WriteLine(sourceFile.Pop());

            sourceFile.Pull(1);
            Console.WriteLine(sourceFile.Pop());

            sourceFile.Pull(1,1);
            Console.WriteLine(sourceFile.Pop());

            Console.WriteLine("Hello World!");
        }
    }
}
