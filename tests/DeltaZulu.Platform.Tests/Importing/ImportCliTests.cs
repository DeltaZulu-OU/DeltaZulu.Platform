using DeltaZulu.Importing.Cli;

namespace DeltaZulu.Platform.Tests.Importing;

[TestClass]
public sealed class ImportCliTests
{
    [TestMethod]
    public void Run_WithDemoSeed_PrintsInteractiveImportSummary()
    {
        var directory = Path.Combine(Path.GetTempPath(), "deltazulu-import-cli-tests", Guid.NewGuid().ToString("N"));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = ImportCli.Run(["--demo-seed", directory, "--job-id", "demo", "--parser-target", "demo", "--preprocessor", "pre-v1"], TextReader.Null, output, error);

        Assert.AreEqual(0, exitCode, error.ToString());
        Assert.Contains("Demo seed files materialized", output.ToString());
        Assert.Contains("bronze rows: 6", output.ToString());
        Assert.Contains("windows-sysmon.jsonl", output.ToString());
    }
}
