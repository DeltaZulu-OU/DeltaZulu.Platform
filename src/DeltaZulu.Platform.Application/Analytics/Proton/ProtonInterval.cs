namespace DeltaZulu.Platform.Application.Analytics.Proton;

/// <summary>
/// Represents a Timeplus Proton time interval in its compact string form (e.g. "5s", "2m", "1h").
/// Used for SCHEDULE, TIMEOUT, BATCH TIMEOUT, and LIMIT PER clauses in Proton DDL.
/// Not to be confused with the SQL INTERVAL keyword used inside SELECT queries — that is emitted
/// by ProtonSqlQueryEmitter as "INTERVAL N UNIT" based on the KQL timespan expression.
/// </summary>
public readonly struct ProtonInterval
{
    private readonly int _value;
    private readonly string _unit;

    private ProtonInterval(int value, string unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        _value = value;
        _unit = unit;
    }

    public static ProtonInterval Seconds(int n) => new(n, "s");
    public static ProtonInterval Minutes(int n) => new(n, "m");
    public static ProtonInterval Hours(int n)   => new(n, "h");
    public static ProtonInterval Days(int n)    => new(n, "d");
    public static ProtonInterval Weeks(int n)   => new(n, "w");

    public override string ToString() => $"{_value}{_unit}";
}
