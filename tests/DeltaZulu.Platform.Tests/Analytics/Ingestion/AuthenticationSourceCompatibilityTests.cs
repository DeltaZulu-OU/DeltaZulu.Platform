using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Ingestion;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Ingestion.Ndjson;
using DeltaZulu.Platform.Ingestion.PubSub;

namespace DeltaZulu.Platform.Tests.Analytics.Ingestion;

[TestClass]
public sealed class AuthenticationSourceCompatibilityTests
{
    // Synthetic source-shaped fixtures derived from Agent WindowsEventLogInput.ToDictionary
    // at e2aac89dafc2f8499e4e4d22ebed97cec75714fe. Not captured Agent output.
    // The common assertions describe authentication semantics, not Windows event codes.
    [TestMethod]
    [DataRow(4624, "LogonSuccess")]
    [DataRow(4625, "LogonFailure")]
    public async Task WindowsAgentShape_MapsToAuthentication(int eventId, string action)
    {
        using var factory = new DuckDbConnectionFactory("DataSource=:memory:");
        var applier = new SchemaApplier(factory);
        applier.ApplyStatements(new SchemaEmitter().EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: SchemaConventions.InternalTables,
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews));

        var raw = $$"""
            {"ProviderName":"Microsoft-Windows-Security-Auditing","EventId":{{eventId}},"Channel":"Security","RecordId":42,"MachineName":"test-host","TimeCreated":"2026-11-26T10:00:00Z","TargetUserName":"alice","IpAddress":"192.0.2.10","IpPort":"12345","LogonType":"3"}
            """;
        var envelope = new RawLogEnvelope("raw.security",
            new DateTimeOffset(2026, 11, 26, 10, 1, 0, TimeSpan.Zero),
            "Security", "Microsoft-Windows-Security-Auditing", "test-host", raw);
        var roundTrip = RawLogNdjsonCodec.Read(RawLogNdjsonCodec.Write([envelope]));
        using var subscriber = new DuckDbRawLogSubscriber(applier,
            new Dictionary<string, string> { ["raw.security"] = "bronze.windows_security_event" });
        await subscriber.HandleAsync(new RawLogBatch("fixture", "raw.security", roundTrip),
            TestContext.CancellationToken);

        Assert.AreEqual(1L, applier.QueryScalar("SELECT count(*) FROM bronze.windows_security_event"));
        Assert.AreEqual(1L, applier.QueryScalar("SELECT count(*) FROM golden.Authentication"));
        Assert.AreEqual(action, applier.QueryScalar("SELECT ActionType FROM golden.Authentication"));
        Assert.AreEqual("alice", applier.QueryScalar("SELECT AccountName FROM golden.Authentication"));
        Assert.AreEqual("test-host", applier.QueryScalar("SELECT DeviceName FROM golden.Authentication"));
        Assert.AreEqual("192.0.2.10", applier.QueryScalar("SELECT SourceIP FROM golden.Authentication"));
        Assert.AreEqual("42", applier.QueryScalar("SELECT ReportId FROM golden.Authentication"));
    }

    public TestContext TestContext { get; set; }
}
