using Dapper;
using DeltaZulu.Platform.Data.Seeding;
using DeltaZulu.Platform.Data.Sqlite.Governance;
using LibGit2Sharp;
using Microsoft.Data.Sqlite;

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

            Assert.IsGreaterThanOrEqualTo(20, written.Count);
            Assert.IsTrue(File.Exists(Path.Combine(root, "powershell-execution-policy-change", "detection.yaml")));
            Assert.IsTrue(File.Exists(Path.Combine(root, "powershell-execution-policy-change", "query.kql")));

            var metadata = File.ReadAllText(Path.Combine(root, "powershell-execution-policy-change", "detection.yaml"));
            var query = File.ReadAllText(Path.Combine(root, "powershell-execution-policy-change", "query.kql"));

            Assert.Contains("query_language: kql", metadata);
            Assert.Contains("entity_mappings:", metadata);
            Assert.Contains("ProcessEvent", query);
        }
        finally
        {
            DeleteDirectoryIfExists(root);
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
            DeleteDirectoryIfExists(root);
        }
    }

    [TestMethod]
    public void SeedAcceptedContentRepository_WritesCanonicalFilesAndCreatesGitCommit()
    {
        var root = Path.Combine(Path.GetTempPath(), "deltazulu-accepted-content-" + Guid.NewGuid().ToString("N"));

        try
        {
            var written = SampleDetectionContentSeeder.SeedAcceptedContentRepository(root);

            var metadataPath = Path.Combine(root, "detections", "powershell-execution-policy-change", "detection.yaml");
            var queryPath = Path.Combine(root, "detections", "powershell-execution-policy-change", "query.kql");

            Assert.IsGreaterThanOrEqualTo(20, written.Count);
            Assert.IsTrue(File.Exists(metadataPath));
            Assert.IsTrue(File.Exists(queryPath));

            using var repository = new Repository(root);
            Assert.IsNotNull(repository.Head.Tip);
            Assert.IsNotNull(repository.Head.Tip.Tree["detections/powershell-execution-policy-change/detection.yaml"]);
            Assert.IsNotNull(repository.Head.Tip.Tree["detections/powershell-execution-policy-change/query.kql"]);
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }

    [TestMethod]
    public void SeedAcceptedContentRepository_IsIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), "deltazulu-accepted-content-" + Guid.NewGuid().ToString("N"));

        try
        {
            SampleDetectionContentSeeder.SeedAcceptedContentRepository(root);
            var second = SampleDetectionContentSeeder.SeedAcceptedContentRepository(root);

            Assert.IsEmpty(second);
        }
        finally
        {
            DeleteDirectoryIfExists(root);
        }
    }


    [TestMethod]
    public void SeedGovernanceCatalog_WritesDetectionsAndAcceptedVersions()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "deltazulu-governance-samples-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString();

        try
        {
            SchemaInitializer.Initialize(connectionString);

            var seeded = SampleDetectionContentSeeder.SeedGovernanceCatalog(connectionString);

            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            var detectionCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM detections");
            var acceptedCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM detections WHERE lifecycle = 'Accepted'");
            var versionCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM detection_versions");
            var currentVersionCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM detections WHERE current_version_id IS NOT NULL");

            Assert.HasCount(10, seeded);
            Assert.AreEqual(seeded.Count, detectionCount);
            Assert.AreEqual(seeded.Count, acceptedCount);
            Assert.AreEqual(seeded.Count, versionCount);
            Assert.AreEqual(seeded.Count, currentVersionCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [TestMethod]
    public void SeedGovernanceCatalog_IsIdempotent()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "deltazulu-governance-samples-" + Guid.NewGuid().ToString("N") + ".db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString();

        try
        {
            SchemaInitializer.Initialize(connectionString);

            var first = SampleDetectionContentSeeder.SeedGovernanceCatalog(connectionString);
            var second = SampleDetectionContentSeeder.SeedGovernanceCatalog(connectionString);

            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            var detectionCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM detections");
            var versionCount = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM detection_versions");

            Assert.HasCount(10, first);
            Assert.IsEmpty(second);
            Assert.AreEqual(10, detectionCount);
            Assert.AreEqual(10, versionCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        // LibGit2Sharp creates a real Git repository for these tests. On Windows, loose
        // objects, pack files, and repository metadata can carry read-only/hidden/system
        // attributes. Directory.Delete(recursive: true) can then fail with
        // UnauthorizedAccessException even after all repository handles are disposed.
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                     .OrderByDescending(directory => directory.Length))
        {
            File.SetAttributes(directory, FileAttributes.Normal);
        }

        File.SetAttributes(path, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }
}
