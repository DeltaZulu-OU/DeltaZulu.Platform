using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Mapping;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion;
using DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion.Silver.Lookups;
using DeltaZulu.Platform.Tests.Analytics.Fixtures;
using static DeltaZulu.Platform.Domain.Analytics.Mapping.MapDsl;

namespace DeltaZulu.Platform.Tests.Analytics.Schema;

[TestClass]
public sealed class WindowsFieldLookupResolutionTests
{
    private static DuckDbConnectionFactory _factory = null!;
    private static SchemaApplier _applier = null!;

    [ClassCleanup]
    public static void Cleanup() => _factory.Dispose();

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _factory = new DuckDbConnectionFactory("DataSource=:memory:");
        _applier = new SchemaApplier(_factory);
        var emitter = new SchemaEmitter();

        var ddl = emitter.EmitAll(
            rawTables: SchemaConventions.RawTables,
            internalTables: [],
            parserViews: SchemaConventions.ParserViews,
            canonicalViews: SchemaConventions.CanonicalViews);

        _applier.ApplyStatements(ddl);
        _applier.ExecuteRaw(MedallionTestData.GetMedallionSeedSql());
    }

    #region MapDsl LookupCase unit tests

    [TestMethod]
    public void LookupCase_KnownValue_ReturnsCaseExpr()
    {
        var lookup = new Dictionary<string, string> { ["10"] = "RemoteInteractive" };
        var result = LookupCase(Col("LogonType"), lookup);

        Assert.IsInstanceOfType<CaseExpr>(result);
        Assert.AreEqual(1, result.Branches.Count);
        Assert.IsInstanceOfType<LiteralExpr>(result.Else);
        Assert.IsNull(((LiteralExpr)result.Else).Value);
    }

    [TestMethod]
    public void LookupCase_MultipleBranches_GeneratesAllBranches()
    {
        var result = LookupCase(Col("LogonType"), WindowsLogonTypeLookup.Values);

        Assert.AreEqual(WindowsLogonTypeLookup.Values.Count, result.Branches.Count);
    }

    [TestMethod]
    public void LookupCase_EmptyDictionary_ReturnsNullElse()
    {
        var result = LookupCase(Col("x"), new Dictionary<string, string>());

        Assert.AreEqual(0, result.Branches.Count);
        Assert.IsInstanceOfType<LiteralExpr>(result.Else);
        Assert.IsNull(((LiteralExpr)result.Else).Value);
    }

    [TestMethod]
    public void MapResolved_CreatesResolvedFieldName()
    {
        var lookup = new Dictionary<string, string> { ["3"] = "Network" };
        var projection = MapResolved("LogonType", Col("LogonType"), lookup);

        Assert.AreEqual("LogonType_resolved", projection.TargetColumn);
        Assert.IsInstanceOfType<CaseExpr>(projection.Expression);
    }

    [TestMethod]
    public void LookupCase_CaseInsensitive_WrapsInputInUpper()
    {
        var lookup = new Dictionary<string, string> { ["0xAB"] = "Found" };
        var result = LookupCase(Col("Status"), lookup, caseInsensitive: true);

        Assert.AreEqual(1, result.Branches.Count);
        var branch = result.Branches[0];
        var eq = Assert.IsInstanceOfType<BinaryExpr>(branch.When);
        var upper = Assert.IsInstanceOfType<FunctionExpr>(eq.Left);
        Assert.AreEqual("UPPER", upper.Name);
        var lit = Assert.IsInstanceOfType<LiteralExpr>(eq.Right);
        Assert.AreEqual("0XAB", (string)lit.Value!);
    }

    [TestMethod]
    public void MapWithResolved_ReturnsTwoProjections()
    {
        var lookup = new Dictionary<string, string> { ["3"] = "Network" };
        var projections = MapWithResolved("LogonType", Col("LogonType"), lookup);

        Assert.AreEqual(2, projections.Length);
        Assert.AreEqual("LogonType", projections[0].TargetColumn);
        Assert.AreEqual("LogonType_resolved", projections[1].TargetColumn);
        Assert.IsInstanceOfType<ColumnExpr>(projections[0].Expression);
        Assert.IsInstanceOfType<CaseExpr>(projections[1].Expression);
    }

    #endregion

    #region Lookup catalog content tests

    [TestMethod]
    public void LogonTypeLookup_ContainsExpectedEntries()
    {
        Assert.IsTrue(WindowsLogonTypeLookup.Values.ContainsKey("2"));
        Assert.IsTrue(WindowsLogonTypeLookup.Values.ContainsKey("3"));
        Assert.IsTrue(WindowsLogonTypeLookup.Values.ContainsKey("10"));
        Assert.AreEqual("Interactive", WindowsLogonTypeLookup.Values["2"]);
        Assert.AreEqual("Network", WindowsLogonTypeLookup.Values["3"]);
        Assert.AreEqual("RemoteInteractive", WindowsLogonTypeLookup.Values["10"]);
    }

    [TestMethod]
    public void NtStatusLookup_ContainsExpectedEntries()
    {
        Assert.IsTrue(NtStatusLookup.Values.ContainsKey("0xC000006A"));
        Assert.IsTrue(NtStatusLookup.Values.ContainsKey("0xC000006D"));
        Assert.IsTrue(NtStatusLookup.Values.ContainsKey("0xC0000234"));
        Assert.AreEqual("STATUS_WRONG_PASSWORD", NtStatusLookup.Values["0xC000006A"]);
        Assert.AreEqual("STATUS_LOGON_FAILURE", NtStatusLookup.Values["0xC000006D"]);
        Assert.AreEqual("STATUS_ACCOUNT_LOCKED_OUT", NtStatusLookup.Values["0xC0000234"]);
    }

    [TestMethod]
    public void NtStatusLookup_IsCaseInsensitive()
    {
        Assert.AreEqual(
            NtStatusLookup.Values["0xC000006A"],
            NtStatusLookup.Values["0xc000006a"]);
    }

    [TestMethod]
    public void WellKnownSidLookup_ContainsExpectedEntries()
    {
        Assert.IsTrue(WellKnownSidLookup.Values.ContainsKey("S-1-5-18"));
        Assert.IsTrue(WellKnownSidLookup.Values.ContainsKey("S-1-5-19"));
        Assert.IsTrue(WellKnownSidLookup.Values.ContainsKey("S-1-5-20"));
        Assert.IsTrue(WellKnownSidLookup.Values.ContainsKey("S-1-0-0"));
        Assert.AreEqual("LocalSystem", WellKnownSidLookup.Values["S-1-5-18"]);
        Assert.AreEqual("LocalService", WellKnownSidLookup.Values["S-1-5-19"]);
        Assert.AreEqual("NetworkService", WellKnownSidLookup.Values["S-1-5-20"]);
        Assert.AreEqual("Nobody", WellKnownSidLookup.Values["S-1-0-0"]);
    }

    [TestMethod]
    public void KerberosEncryptionTypeLookup_ContainsExpectedEntries()
    {
        Assert.AreEqual("RC4_HMAC", KerberosEncryptionTypeLookup.Values["0x17"]);
        Assert.AreEqual("AES256_CTS_HMAC_SHA1_96", KerberosEncryptionTypeLookup.Values["0x12"]);
    }

    [TestMethod]
    public void MessageResourceIdLookup_ContainsExpectedEntries()
    {
        Assert.AreEqual("Yes", MessageResourceIdLookup.Values["%%1842"]);
        Assert.AreEqual("No", MessageResourceIdLookup.Values["%%1843"]);
        Assert.AreEqual("Unknown user name or bad password", MessageResourceIdLookup.Values["%%2313"]);
    }

    #endregion

    #region SchemaEmitter CASE expression tests

    [TestMethod]
    public void SchemaEmitter_LookupCase_EmitsDuckDbCaseWhen()
    {
        var lookup = new Dictionary<string, string>
        {
            ["10"] = "RemoteInteractive",
            ["3"] = "Network"
        };

        var view = new ParserViewDef(
            "silver",
            "v_test_lookup",
            "test-source",
            "TestEvent",
            new MappingQueryDef(
                "bronze.test_source",
                null,
                [
                    Map("LogonType", Col("lt")),
                    MapResolved("LogonType", Col("lt"), lookup)
                ]),
            [
                new ColumnDef("LogonType", DuckDbType.Varchar, KustoType.String),
                new ColumnDef("LogonType_resolved", DuckDbType.Varchar, KustoType.String)
            ]);

        var sql = new SchemaEmitter().EmitParserView(view);

        Assert.Contains("CASE", sql);
        Assert.Contains("WHEN", sql);
        Assert.Contains("'RemoteInteractive'", sql);
        Assert.Contains("'Network'", sql);
        Assert.Contains("LogonType_resolved", sql);
    }

    #endregion

    #region Golden Authentication integration tests

    [TestMethod]
    public void Authentication_FlowsIntoGoldenView()
    {
        var count = _applier.QueryScalar("SELECT count(*) FROM golden.Authentication");

        Assert.IsGreaterThanOrEqualTo(5, count, "Expected authentication rows in golden.Authentication.");
    }

    [TestMethod]
    public void Authentication_LogonType10_ResolvesToRemoteInteractive()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE LogonType = '10' AND LogonType_resolved = 'RemoteInteractive'");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Authentication_LogonType3_ResolvesToNetwork()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE LogonType = '3' AND LogonType_resolved = 'Network'");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Authentication_StatusHex_ResolvesToNtStatus()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE Status = '0xC000006A' AND Status_resolved = 'STATUS_WRONG_PASSWORD'");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Authentication_SubStatusHex_ResolvesToNtStatus()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE SubStatus = '0xC000006A' AND SubStatus_resolved = 'STATUS_WRONG_PASSWORD'");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Authentication_WellKnownSid_ResolvesToName()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE SubjectUserSid = 'S-1-5-18' AND SubjectUserSid_resolved = 'LocalSystem'");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Authentication_NobodySid_ResolvesToNobody()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE SubjectUserSid = 'S-1-0-0' AND SubjectUserSid_resolved = 'Nobody'");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Authentication_LowercaseStatusHex_ResolvesToNtStatus()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE Status = '0xc000006d' AND Status_resolved = 'STATUS_LOGON_FAILURE'");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Authentication_LowercaseSubStatusHex_ResolvesToNtStatus()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE SubStatus = '0xc000006a' AND SubStatus_resolved = 'STATUS_WRONG_PASSWORD'");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Authentication_UnknownLogonType_ResolvedIsNull()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE LogonType = '99' AND LogonType_resolved IS NULL");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Authentication_UnknownStatus_ResolvedIsNull()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE Status = '0xDEADBEEF' AND Status_resolved IS NULL");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Authentication_OriginalFieldPreserved()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE LogonType = '10'");
        Assert.IsGreaterThanOrEqualTo(1, count, "Original LogonType field must be preserved.");

        var statusCount = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE Status = '0xC000006A'");
        Assert.IsGreaterThanOrEqualTo(1, statusCount, "Original Status field must be preserved.");
    }

    [TestMethod]
    public void Authentication_ActionTypeLogonSuccess()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE ActionType = 'LogonSuccess'");

        Assert.IsGreaterThanOrEqualTo(2, count);
    }

    [TestMethod]
    public void Authentication_ActionTypeLogonFailure()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE ActionType = 'LogonFailure'");

        Assert.IsGreaterThanOrEqualTo(3, count);
    }

    [TestMethod]
    public void Authentication_FailureReasonResolved()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE FailureReason = '%%2313' AND FailureReason_resolved = 'Unknown user name or bad password'");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Authentication_UnknownFailureReason_ResolvedIsNull()
    {
        var count = _applier.QueryScalar(
            "SELECT count(*) FROM golden.Authentication WHERE FailureReason = '%%9999' AND FailureReason_resolved IS NULL");

        Assert.IsGreaterThanOrEqualTo(1, count);
    }

    [TestMethod]
    public void Validate_AuthenticationGoldenView()
    {
        var view = MedallionSchemaCatalog.CanonicalViews
            .First(v => v.Name == "Authentication");
        var mismatches = _applier.Validate(view);
        var missing = mismatches.Where(m => m.ActualType == "MISSING").ToList();

        Assert.IsEmpty(missing,
            $"golden.Authentication missing columns:\n{string.Join("\n", missing.Select(m => m.Message))}");
    }

    [TestMethod]
    public void ExistingGoldenViews_StillValid()
    {
        foreach (var view in SchemaConventions.CanonicalViews.Where(v => v.Name != "Authentication"))
        {
            var mismatches = _applier.Validate(view);
            var missing = mismatches.Where(m => m.ActualType == "MISSING").ToList();
            Assert.IsEmpty(missing,
                $"{view.QualifiedName} missing columns:\n{string.Join("\n", missing.Select(m => m.Message))}");
        }
    }

    [TestMethod]
    public void ExistingGoldenViews_DataStillFlows()
    {
        Assert.IsGreaterThanOrEqualTo(10, _applier.QueryScalar("SELECT count(*) FROM golden.ProcessEvent"));
        Assert.IsGreaterThanOrEqualTo(4, _applier.QueryScalar("SELECT count(*) FROM golden.NetworkSession"));
        Assert.IsGreaterThanOrEqualTo(5, _applier.QueryScalar("SELECT count(*) FROM golden.Dns"));
    }

    #endregion
}
