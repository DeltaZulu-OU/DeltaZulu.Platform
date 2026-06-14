using DeltaZulu.Platform.Data.SeedData;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeltaZulu.Platform.Tests.Data;

[TestClass]
public sealed class SampleDetectionContentSeederTests
{
    [TestMethod]
    public void Seed_WritesSampleYamlAndKqlFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "deltazulu-sample-detections-" + Guid.NewGuid().ToString("N"));

        try
        {
            var written = SampleDetectionContentSeeder.Seed(root);

            Assert.IsTrue(written.Count >= 20);
            Assert.IsTrue(File.Exists(Path.Combine(root, "powershell-execution-policy-change", "detection.yaml")));
            Assert.IsTrue(File.Exists(Path.Combine(root, "powershell-execution-policy-change", "query.kql")));

            var metadata = File.ReadAllText(Path.Combine(root, "powershell-execution-policy-change", "detection.yaml"));
            var query = File.ReadAllText(Path.Combine(root, "powershell-execution-policy-change", "query.kql"));

            StringAssert.Contains(metadata, "query_language: kql");
            StringAssert.Contains(metadata, "entity_mappings:");
            StringAssert.Contains(query, "ProcessEvent");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Seed_DoesNotOverwriteExistingFilesByDefault()
    {
        var root = Path.Combine(Path.GetTempPath(), "deltazulu-sample-detections-" + Guid.NewGuid().ToString("N"));

        try
        {
            SampleDetectionContentSeeder.Seed(root);
            var path = Path.Combine(root, "powershell-execution-policy-change", "query.kql");
            File.WriteAllText(path, "ProcessEvent | take 1");

            SampleDetectionContentSeeder.Seed(root);

            Assert.AreEqual("ProcessEvent | take 1", File.ReadAllText(path));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
