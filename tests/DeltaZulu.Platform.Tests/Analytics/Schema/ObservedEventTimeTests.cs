using System.Text.Json;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Analytics.Schema.Definitions.Medallion;
using DeltaZulu.Platform.Tests.Analytics.Fixtures;
using DuckDB.NET.Data;

namespace DeltaZulu.Platform.Tests.Analytics.Schema;

[TestClass]
public sealed class ObservedEventTimeTests
{
    [TestMethod]
    [DataRow("v_dns_server_query_event", "256", "TimeCreated")]
    [DataRow("v_dns_windows_sysmon_eid22", "22", "UtcTime")]
    [DataRow("v_networksession_windows_security_eid5156", "5156", "TimeCreated")]
    [DataRow("v_networksession_windows_sysmon_eid3", "3", "UtcTime")]
    [DataRow("v_processevent_windows_security_eid4688", "4688", "TimeCreated")]
    [DataRow("v_processevent_windows_sysmon_eid1", "1", "UtcTime")]
    [DataRow("v_authentication_windows_security_eid4624", "4624", "TimeCreated")]
    [DataRow("v_authentication_windows_security_eid4625", "4625", "TimeCreated")]
    public void SilverRetainsEvidenceAndUsesOnlySourceTime(string viewName, string eventId, string field)
    {
        using var connection = new DuckDBConnection("Data Source=:memory:");
        connection.Open();
        MedallionTestDatabase.CreateBronzeTables(connection);
        var view = MedallionSchemaCatalog.ParserViews.Single(v => v.Name == viewName);
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE SCHEMA silver";
        command.ExecuteNonQuery();
        command.CommandText = new SchemaEmitter().EmitParserView(view);
        command.ExecuteNonQuery();

        var expectedUtc = new DateTime(2020, 10, 25, 0, 30, 0, DateTimeKind.Utc);
        Check(null, null, null, "Missing");
        Check(null, null, null, "Null", includeNull: true);
        Check("not-a-clock", null, null, "Invalid");
        Check("2020-02-28T25:30:00Z", null, null, "Invalid");
        Check("10000-01-01T00:00:00Z", null, null, "Invalid");
        Check("2020-02-31T12:30:00Z", null, null, "Invalid");
        Check("2020-10-25T00:30:00Z", null, expectedUtc, null);
        Check("2020-10-25T02:30:00+02:00", null, expectedUtc, null);
        if (field == "UtcTime")
            Check("2020-10-25 00:30:00", null, expectedUtc, null);
        else
            Check("2020-10-25 02:30:00", null, null, "MissingOffset");
        if (viewName == "v_dns_server_query_event")
        {
            Check("bad", "2020-10-25T00:30:00Z", expectedUtc, null);
            Check("2020-10-25T00:30:00Z", "2021-01-01T00:00:00Z", expectedUtc, null);
        }

        void Check(string? primary, string? alternate, DateTime? expected, string? reason, bool includeNull = false)
        {
            var fields = new Dictionary<string, string?> { ["EventID"] = eventId };
            if (primary is not null || includeNull) fields.Add(field, primary);
            if (alternate is not null) fields.Add("EventTime", alternate);
            var json = JsonSerializer.Serialize(fields);
            command.CommandText = $"DELETE FROM {view.Mapping.SourceObject}";
            command.ExecuteNonQuery();
            command.CommandText = $"INSERT INTO {view.Mapping.SourceObject} (ingest_time, raw_log) VALUES (TIMESTAMP '2026-11-25', ?)";
            var parameter = command.CreateParameter();
            parameter.Value = json;
            command.Parameters.Add(parameter);
            command.ExecuteNonQuery();
            command.Parameters.Clear();
            command.CommandText = $"SELECT Timestamp, TimestampNullReason FROM {view.QualifiedName}";
            using var reader = command.ExecuteReader();
            Assert.IsTrue(reader.Read(), "The Silver row must survive a missing or invalid time.");
            Assert.AreEqual(expected, reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0));
            Assert.AreEqual(reason, reader.IsDBNull(1) ? null : reader.GetString(1));
            Assert.IsFalse(reader.Read());
        }
    }
}
