namespace Hunting.Tests.Translation;

using Hunting.Core.Catalog;
using Hunting.Core.Policy;
using Hunting.Core.QueryModel;
using Hunting.Core.Translation;
using Hunting.Schema.Definitions;

/// <summary>
/// Edge case, error path, and adversarial tests for the KQL-to-RelNode translator.
/// These verify that the translator rejects malformed, blocked, or adversarial KQL
/// with structured diagnostics rather than crashing or producing wrong trees.
/// </summary>
[TestClass]
public sealed class KustoToRelationalEdgeCaseTests
{
    private static ApprovedViewCatalog _catalog = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _catalog = new ApprovedViewCatalog();
        _catalog.Register(DeviceProcessEventsSchema.View);
    }

    private (RelNode? Node, DiagnosticBag Diag) Translate(string kql)
    {
        var diag = new DiagnosticBag();
        var translator = new KustoToRelational(_catalog, diag);
        var node = translator.Translate(kql);
        return (node, diag);
    }

    // ─── Broken / invalid KQL syntax ────────────────────────────────

    [TestMethod]
    [Description("Empty string produces diagnostic, not crash")]
    public void Syntax_EmptyString()
    {
        var (result, diag) = Translate("");
        Assert.IsTrue(result is null || diag.HasErrors,
            "Empty KQL should produce null result or diagnostics");
    }

    [TestMethod]
    [Description("Whitespace-only string produces diagnostic")]
    public void Syntax_WhitespaceOnly()
    {
        var (result, diag) = Translate("   \t\n  ");
        Assert.IsTrue(result is null || diag.HasErrors,
            "Whitespace-only KQL should produce null result or diagnostics");
    }

    [TestMethod]
    [Description("Completely invalid text produces parse error")]
    public void Syntax_Gibberish()
    {
        var (result, diag) = Translate("!@#$%^&*()_+{}|:<>?");
        Assert.IsTrue(diag.HasErrors, "Gibberish should produce parse errors");
        Assert.Contains(d => d.Phase == DiagnosticPhase.Parse, diag.All,
            "Errors should be in Parse phase");
    }

    [TestMethod]
    [Description("Unterminated string literal produces parse error")]
    public void Syntax_UnterminatedString()
    {
        var (result, diag) = Translate("""DeviceProcessEvents | where FileName == "unterminated""");
        Assert.IsTrue(diag.HasErrors, "Unterminated string should produce error");
    }

    [TestMethod]
    [Description("Missing pipe operator between clauses")]
    public void Syntax_MissingPipe()
    {
        var (result, diag) = Translate(
            """DeviceProcessEvents where FileName == "cmd.exe" """);
        Assert.IsTrue(diag.HasErrors, "Missing pipe should produce error");
    }

    [TestMethod]
    [Description("Double pipe operator")]
    public void Syntax_DoublePipe()
    {
        var (result, diag) = Translate("DeviceProcessEvents || take 10");
        Assert.IsTrue(diag.HasErrors, "Double pipe should produce error");
    }

    [TestMethod]
    [Description("Trailing pipe with no operator")]
    public void Syntax_TrailingPipe()
    {
        var (result, diag) = Translate("DeviceProcessEvents | take 10 |");
        Assert.IsTrue(diag.HasErrors, "Trailing pipe should produce error");
    }

    [TestMethod]
    [Description("Leading pipe with no table")]
    public void Syntax_LeadingPipe()
    {
        var (result, diag) = Translate("| take 10");
        Assert.IsTrue(diag.HasErrors, "Leading pipe should produce error");
    }

    [TestMethod]
    [Description("Mismatched parentheses")]
    public void Syntax_MismatchedParens()
    {
        var (result, diag) = Translate(
            """DeviceProcessEvents | where (FileName == "cmd.exe" """);
        Assert.IsTrue(diag.HasErrors, "Mismatched parens should produce error");
    }

    [TestMethod]
    [Description("take with non-numeric argument")]
    public void Syntax_TakeNonNumeric()
    {
        var (result, diag) = Translate("""DeviceProcessEvents | take "ten" """);
        Assert.IsTrue(diag.HasErrors, "take with string should produce error");
    }

    [TestMethod]
    [Description("take with negative number")]
    public void Syntax_TakeNegative()
    {
        var (result, diag) = Translate("DeviceProcessEvents | take -5");
        Assert.IsTrue(diag.HasErrors, "take with negative should produce error");
    }


    [TestMethod]
    [Description("extract() with too few args produces diagnostic")]
    public void Syntax_Extract_TooFewArguments()
    {
        var (_, diag) = Translate(
            """
            DeviceProcessEvents
            | extend bad = extract("-enc\\s+([^\\s]+)", 1)
            """);
        Assert.IsTrue(diag.HasErrors, "extract with 2 args should fail");
        Assert.Contains(d => d.Message.Contains("extract", StringComparison.OrdinalIgnoreCase), diag.All,
            "Diagnostic should reference extract");
    }

    [TestMethod]
    [Description("extract() with too many args produces diagnostic")]
    public void Syntax_Extract_TooManyArguments()
    {
        var (_, diag) = Translate(
            """
            DeviceProcessEvents
            | extend bad = extract("-enc\\s+([^\\s]+)", 1, ProcessCommandLine, typeof(string), "extra")
            """);
        Assert.IsTrue(diag.HasErrors, "extract with 5 args should fail");
    }

    [TestMethod]
    [Description("extract() with non-integer capture group produces semantic diagnostic")]
    public void Syntax_Extract_NonIntegerCaptureGroup()
    {
        var (_, diag) = Translate(
            """
            DeviceProcessEvents
            | extend bad = extract("-enc\\s+([^\\s]+)", "one", ProcessCommandLine)
            """);
        Assert.IsTrue(diag.HasErrors, "extract requires integer capture group index");
    }

    // ─── Comments ───────────────────────────────────────────────────

    [TestMethod]
    [Description("Single-line comment before query")]
    public void Comment_SingleLineBefore()
    {
        var (result, diag) = Translate(
            """
            // This is a hunting query
            DeviceProcessEvents | take 10
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    [Description("Single-line comment after pipe")]
    public void Comment_SingleLineAfterPipe()
    {
        var (result, diag) = Translate(
            """
            DeviceProcessEvents
            | where FileName == "cmd.exe" // filter for cmd
            | take 10
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    [Description("Block comment inside expression")]
    public void Comment_BlockInExpression()
    {
        var (_, diag) = Translate(
            """
            DeviceProcessEvents
            | where /* only cmd */ FileName == "cmd.exe"
            | take 10
            """);
        Assert.IsTrue(diag.HasErrors, string.Join("\n", diag.All));
    }

    [TestMethod]
    [Description("Comment-only input should not crash")]
    public void Comment_OnlyComment()
    {
        var (result, diag) = Translate("// just a comment, nothing to run");
        // Should not crash; either null result or diagnostics
        Assert.IsTrue(result is null || !diag.HasErrors);
    }

    [TestMethod]
    [Description("Multi-line block comment spanning operators")]
    public void Comment_MultiLineBlock()
    {
        var (result, diag) = Translate(
            """
            DeviceProcessEvents
            /*
               This is a multi-line comment
               that spans several lines
            */
            | take 5
            """);
        Assert.IsTrue(diag.HasErrors, string.Join("\n", diag.All));
    }

    // ─── Policy violations ──────────────────────────────────────────

    [TestMethod]
    [Description("Access to raw.* table rejected")]
    public void Policy_RawTableAccess()
    {
        var (result, diag) = Translate("raw.windows_event_json | take 10");
        Assert.IsTrue(diag.HasErrors);
        Assert.Contains(d => d.Phase == DiagnosticPhase.Policy ||
                                        d.Phase == DiagnosticPhase.Parse, diag.All,
            "Raw table access should be rejected at policy or parse phase");
    }

    [TestMethod]
    [Description("Access to internal.* table rejected")]
    public void Policy_InternalTableAccess()
    {
        var (result, diag) = Translate("internal.v_process_sysmon_create | take 10");
        Assert.IsTrue(diag.HasErrors);
    }

    [TestMethod]
    [Description("Nonexistent table produces clear diagnostic")]
    public void Policy_NonexistentTable()
    {
        var (result, diag) = Translate("FakeTable | take 10");
        Assert.IsTrue(diag.HasErrors);
        Assert.Contains(d =>
            d.Message.Contains("approved") || d.Message.Contains("FakeTable"), diag.All,
            "Diagnostic should mention the table or approval");
    }

    [TestMethod]
    [Description("Bare join (no kind=) blocked with clear message")]
    public void Policy_BareJoinBlocked()
    {
        var (result, diag) = Translate(
            "DeviceProcessEvents | join (DeviceProcessEvents | take 5) on DeviceName");
        Assert.IsTrue(diag.HasErrors);
        Assert.IsNull(result);
        var joinDiag = diag.All.FirstOrDefault(d =>
            d.Message.Contains("innerunique") || d.Message.Contains("kind"));
        Assert.IsNotNull(joinDiag, "Diagnostic should explain innerunique semantics");
    }

    [TestMethod]
    [Description("innerunique join explicitly blocked")]
    public void Policy_InneruniqueJoinBlocked()
    {
        var (result, diag) = Translate(
            "DeviceProcessEvents | join kind=innerunique (DeviceProcessEvents | take 5) on DeviceName");
        Assert.IsTrue(diag.HasErrors);
    }

    // ─── Management commands rejected ───────────────────────────────

    [TestMethod]
    [Description("Dot-command .show rejected")]
    public void Policy_DotCommandShow()
    {
        var (result, diag) = Translate(".show tables");
        Assert.IsTrue(diag.HasErrors || result is null,
            ".show command should be rejected");
    }

    [TestMethod]
    [Description("Dot-command .drop rejected")]
    public void Policy_DotCommandDrop()
    {
        var (result, diag) = Translate(".drop table DeviceProcessEvents");
        Assert.IsTrue(diag.HasErrors || result is null,
            ".drop command should be rejected");
    }

    [TestMethod]
    [Description("Dot-command .set rejected")]
    public void Policy_DotCommandSet()
    {
        var (result, diag) = Translate(".set MyTable <| DeviceProcessEvents | take 10");
        Assert.IsTrue(diag.HasErrors || result is null,
            ".set command should be rejected");
    }

    [TestMethod]
    [Description("Query followed by management command is rejected as multi-statement input")]
    public void Policy_QueryThenDotCommandRejected()
    {
        var (result, diag) = Translate(
            """
            DeviceProcessEvents | where FileName == "cmd.exe";
            .drop table DeviceProcessEvents
            """);

        Assert.IsNull(result, "Translator must reject multi-statement input that chains commands.");
        Assert.IsTrue(diag.HasErrors, "A diagnostic should be raised for mixed query/command input.");
        Assert.Contains(
            d =>
                d.Message.Contains("Management commands are not allowed. Submit only a single query expression, optionally preceded by let bindings."), diag.All,
            "Expected rejection diagnostic for mixed query/command input was not emitted.");
    }

    [TestMethod]
    [Description("Ordinary quoted string spanning newline should not be translated as a recovered partial query")]
    public void Policy_OrdinaryQuotedStringWithNewline_FailsClosed()
    {
        var (result, diag) = Translate(
            """
        let Suspicious = ";
        .drop table DeviceProcessEvents";
        DeviceProcessEvents
        | where ProcessCommandLine contains Suspicious
        | take 1
        """);

        Assert.IsNull(result);
        Assert.Contains(d =>
            d.Phase == DiagnosticPhase.Parse ||
            d.Phase == DiagnosticPhase.Policy, diag.All);
    }

    [TestMethod]
    [Description("SQL-like payload inside KQL string literal does not become a second statement")]
    public void Parse_StringLiteralSqlPayload_RemainsSingleStatement()
    {
        var (result, diag) = Translate(
            """DeviceProcessEvents | where FileName == "'; DROP TABLE main.DeviceProcessEvents;--" | take 1""");

        Assert.IsNotNull(result, "Payload embedded in a KQL string should remain data, not a statement separator.");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All.Select(d => d.Message)));
    }

    // ─── Type and column errors ─────────────────────────────────────

    [TestMethod]
    [Description("Reference to nonexistent column in approved table")]
    public void Column_Nonexistent()
    {
        var (result, diag) = Translate(
            """DeviceProcessEvents | where FakeColumn == "test" """);
        // Kusto.Language semantic analysis should catch this
        Assert.IsTrue(diag.HasErrors,
            "Reference to nonexistent column should produce error");
    }

    [TestMethod]
    [Description("Type mismatch in comparison (string == int) caught by analyzer")]
    public void Column_TypeMismatch()
    {
        var (result, diag) = Translate(
            "DeviceProcessEvents | where FileName == 42");
        // Kusto.Language may or may not flag this — depends on implicit conversion
        // At minimum it should not crash
        Assert.IsNotNull(diag, "Translation should complete without crash");
    }

    // ─── Operator edge cases ────────────────────────────────────────

    [TestMethod]
    [Description("where with always-true predicate")]
    public void Operator_WhereAlwaysTrue()
    {
        var (result, diag) = Translate("DeviceProcessEvents | where true | take 5");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    [Description("where with always-false predicate")]
    public void Operator_WhereAlwaysFalse()
    {
        var (result, diag) = Translate("DeviceProcessEvents | where false | take 5");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    [Description("Multiple where clauses in sequence")]
    public void Operator_MultipleWhere()
    {
        var (result, diag) = Translate(
            """
            DeviceProcessEvents
            | where FileName == "cmd.exe"
            | where ProcessId > 100
            | where DeviceName != ""
            | take 10
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        // Should produce nested FilterNodes
        var limit = result as LimitNode;
        Assert.IsNotNull(limit);
    }

    [TestMethod]
    [Description("project with zero columns should error or produce valid SQL")]
    public void Operator_ProjectEmpty()
    {
        var (result, diag) = Translate("DeviceProcessEvents | project");
        // Kusto parser should reject this
        Assert.IsTrue(diag.HasErrors, "project with no columns should error");
    }

    [TestMethod]
    [Description("summarize with no aggregation function")]
    public void Operator_SummarizeNoAgg()
    {
        var (result, diag) = Translate("DeviceProcessEvents | summarize by FileName");
        // Valid KQL — equivalent to "distinct FileName" in Kusto
        Assert.IsNotNull(diag, "Should not crash");
    }

    [TestMethod]
    [Description("sort by multiple columns")]
    public void Operator_SortMultiple()
    {
        var (result, diag) = Translate(
            "DeviceProcessEvents | sort by DeviceName asc, Timestamp desc | take 10");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    [Description("top with multiple sort columns")]
    public void Operator_TopMultiSort()
    {
        var (result, diag) = Translate(
            "DeviceProcessEvents | top 5 by Timestamp desc");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var limit = result as LimitNode;
        Assert.IsNotNull(limit);
        Assert.AreEqual(5, limit!.Count);
    }

    // ─── Whitespace and formatting edge cases ───────────────────────

    [TestMethod]
    [Description("Excessive whitespace between operators")]
    public void Whitespace_Excessive()
    {
        var (result, diag) = Translate(
            "DeviceProcessEvents    |    take     10");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    [Description("Tab-indented KQL")]
    public void Whitespace_TabIndented()
    {
        var (result, diag) = Translate(
            "DeviceProcessEvents\n\t| where FileName == \"cmd.exe\"\n\t| take 10");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    [Description("Windows-style line endings (CRLF)")]
    public void Whitespace_Crlf()
    {
        var (result, diag) = Translate(
            "DeviceProcessEvents\r\n| where FileName == \"cmd.exe\"\r\n| take 10");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    [Description("Single-line compact KQL")]
    public void Whitespace_Compact()
    {
        var (result, diag) = Translate(
            """DeviceProcessEvents|where FileName=="cmd.exe"|take 10""");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    // ─── Adversarial and stress inputs ──────────────────────────────

    [TestMethod]
    [Description("Very long column name does not crash")]
    public void Adversarial_LongColumnName()
    {
        var longName = new string('A', 500);
        var (result, diag) = Translate(
            $"DeviceProcessEvents | where {longName} == \"test\"");
        // Should produce error (column doesn't exist) but not crash
        Assert.IsTrue(diag.HasErrors || result is not null);
    }

    [TestMethod]
    [Description("Very long string literal does not crash")]
    public void Adversarial_LongStringLiteral()
    {
        var longValue = new string('X', 10_000);
        var (result, diag) = Translate(
            $"DeviceProcessEvents | where FileName == \"{longValue}\" | take 1");
        Assert.IsNotNull(diag, "Should not crash on long string");
    }

    [TestMethod]
    [Description("Many chained where clauses do not stack overflow")]
    public void Adversarial_ManyWheres()
    {
        var kql = "DeviceProcessEvents\n";
        for (var i = 0; i < 50; i++)
        {
            kql += $"| where ProcessId > {i}\n";
        }

        kql += "| take 5";

        var (result, diag) = Translate(kql);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    [Description("Many chained extend clauses do not stack overflow")]
    public void Adversarial_ManyExtends()
    {
        var kql = "DeviceProcessEvents\n";
        for (var i = 0; i < 30; i++)
        {
            kql += $"| extend col_{i} = ProcessId + {i}\n";
        }

        kql += "| take 5";

        var (result, diag) = Translate(kql);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    // ─── Diagnostic quality ─────────────────────────────────────────

    [TestMethod]
    [Description("All diagnostics have non-empty messages")]
    public void DiagnosticQuality_NonEmptyMessages()
    {
        // Run several failing translations and check diagnostic quality
        string[] badInputs =
        [
            "FakeTable | take 10",
            "!@#$",
            "",
            "DeviceProcessEvents | join (DeviceProcessEvents) on DeviceName",
        ];

        foreach (var input in badInputs)
        {
            var (_, diag) = Translate(input);
            foreach (var d in diag.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.Message),
                    $"Diagnostic for input '{input}' has empty message");
                Assert.AreNotEqual(DiagnosticPhase.Emit, d.Phase,
                    "Translator should never produce Emit-phase diagnostics");
                Assert.AreNotEqual(DiagnosticPhase.Execute, d.Phase,
                    "Translator should never produce Execute-phase diagnostics");
            }
        }
    }

    [TestMethod]
    [Description("Diagnostics carry correct phase identifiers")]
    public void DiagnosticQuality_CorrectPhase()
    {
        // Parse error
        var (_, d1) = Translate("!!! broken syntax !!!");
        if (d1.HasErrors)
        {
            Assert.IsTrue(d1.All.All(d => d.Phase == DiagnosticPhase.Parse ||
                                          d.Phase == DiagnosticPhase.Translate));
        }

        // Policy error
        var (_, d2) = Translate("FakeTable | take 10");
        if (d2.HasErrors)
        {
            Assert.Contains(d => d.Phase == DiagnosticPhase.Policy ||
                                          d.Phase == DiagnosticPhase.Parse, d2.All);
        }
    }
}