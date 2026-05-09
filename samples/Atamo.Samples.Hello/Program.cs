// Smoke-test runner. Confirms that the scaffold compiles, references resolve,
// and the .NET 9 runtime is wired up end-to-end. Replaced or removed once
// real samples land (see docs/guides/01-async-email-ingest.md).

Console.WriteLine("ATAMO scaffold is alive.");
Console.WriteLine($"  .NET runtime : {Environment.Version}");
Console.WriteLine($"  OS           : {Environment.OSVersion}");
Console.WriteLine($"  Atamo.dll    : referenced (no public surface yet)");
