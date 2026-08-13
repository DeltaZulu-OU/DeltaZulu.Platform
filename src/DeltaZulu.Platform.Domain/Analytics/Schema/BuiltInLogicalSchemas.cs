namespace DeltaZulu.Platform.Domain.Analytics.Schema;

/// <summary>Versioned source schemas that exercise the registry-backed parser boundary.</summary>
public static class BuiltInLogicalSchemas
{
    public static LogicalSchemaVersion CefFirewallV1 { get; } = new(
        "cef", "cef_firewall", 1,
        [
            new("CefVersion", LogicalFieldType.String(), Parser: new("cef:header:version", ParserFieldPlacement.TopLevel)),
            new("DeviceVendor", LogicalFieldType.String(), Parser: new("cef:header:deviceVendor", ParserFieldPlacement.TopLevel)),
            new("DeviceProduct", LogicalFieldType.String(), Parser: new("cef:header:deviceProduct", ParserFieldPlacement.TopLevel)),
            new("SignatureId", LogicalFieldType.String(), Parser: new("cef:header:signatureId", ParserFieldPlacement.TopLevel)),
            new("Severity", LogicalFieldType.Integer(), Parser: new("cef:header:severity", ParserFieldPlacement.TopLevel)),
            new("EventTime", LogicalFieldType.Timestamp(), Parser: new("cef:extension:rt", ParserFieldPlacement.TopLevel, Canonicalization: ParserCanonicalization.Utc)),
            new("SourceIp", LogicalFieldType.IpAddress(), Parser: new("cef:extension:src", ParserFieldPlacement.TopLevel)),
            new("DestinationIp", LogicalFieldType.IpAddress(), Parser: new("cef:extension:dst", ParserFieldPlacement.TopLevel)),
            new("SessionGuid", LogicalFieldType.Uuid(), Parser: new("cef:extension:cs2", ParserFieldPlacement.TopLevel)),
            new("Blocked", LogicalFieldType.Boolean(), Parser: new("cef:extension:cs4", ParserFieldPlacement.TopLevel, BooleanLexemes: new("false", "true"))),
            new("SessionDuration", LogicalFieldType.Duration(LogicalDurationUnit.Microseconds), Parser: new("cef:extension:cn1", ParserFieldPlacement.TopLevel)),
            new("TransactionAmount", LogicalFieldType.Decimal(18, 2), Parser: new("cef:extension:cn2", ParserFieldPlacement.TopLevel)),
            new("Extensions", LogicalFieldType.Dynamic(), Parser: new("cef:extension:*", ParserFieldPlacement.TopLevel)),
            new("AgentBuild", LogicalFieldType.String(), Parser: new("cef:extension:cs3", ParserFieldPlacement.DynamicBag, "$.Extensions.cs3"))
        ], "CEF firewall Silver source-family contract.");
}
