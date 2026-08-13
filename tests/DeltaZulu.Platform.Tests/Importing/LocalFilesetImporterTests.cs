using DeltaZulu.Importing.Core;

namespace DeltaZulu.Platform.Tests.Importing;

[TestClass]
public sealed class LocalFilesetImporterTests
{
    [TestMethod]
    public void Import_EnumeratesFilesChunksDeterministicallyAndAddsBronzeProvenance()
    {
        var directory = CreateDirectory();
        File.WriteAllText(Path.Combine(directory, "a.jsonl"), "{\"id\":1}\n{bad}\n{\"id\":3}\n");
        File.WriteAllText(Path.Combine(directory, "b.syslog"), "alpha\nbeta\n");
        var importer = new LocalFilesetImporter();
        var request = new ImportJobRequest("job-1", ImportMode.Migration, directory, "windows_sysmon", "pre-v1", MaxChunkBytes: 12);

        var first = importer.Import(request);
        var second = importer.Import(request);

        Assert.AreEqual(2, first.Manifest.SourceObjects.Count);
        Assert.IsTrue(first.Manifest.Chunks.Count > 2);
        Assert.AreEqual(5, first.BronzeRows.Count);
        Assert.AreEqual(1, first.Manifest.Summary.MalformedRecordCount);
        CollectionAssert.AreEqual(first.Manifest.Chunks.Select(chunk => chunk.ChunkId).ToArray(), second.Manifest.Chunks.Select(chunk => chunk.ChunkId).ToArray());
        Assert.IsTrue(first.BronzeRows.All(row => row.ImportJobId == "job-1"));
        Assert.IsTrue(first.BronzeRows.All(row => row.ParserTarget == "windows_sysmon"));
        Assert.IsTrue(first.BronzeRows.All(row => row.PreprocessorVersion == "pre-v1"));
        Assert.IsTrue(first.BronzeRows.All(row => row.SourceSha256.Length == 64 && row.ChunkSha256.Length == 64));
    }

    [TestMethod]
    public void DemoSeedMode_LoadsKnownDatasetAndValidatesCounts()
    {
        var directory = DemoSeedImportCatalog.MaterializeBaseline(CreateDirectory());
        var importer = new LocalFilesetImporter();

        var result = importer.Import(new ImportJobRequest("demo", ImportMode.DemoSeed, directory, "demo", "pre-v1"));

        Assert.IsTrue(DemoSeedImportCatalog.ValidateBaselineCounts(result));
        Assert.AreEqual(6, result.Manifest.Summary.BronzeRowCount);
        Assert.IsTrue(result.Manifest.Summary.SilverNormalizationScheduled);
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "deltazulu-importer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
