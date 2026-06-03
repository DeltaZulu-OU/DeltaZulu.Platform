namespace Hunting.Render.Tabular;

public sealed record RenderColumn
{
    public required string Name { get; init; }

    public string? TypeName { get; init; }

    public Type? ClrType { get; init; }

    public bool IsNumeric { get; init; }

    public bool IsTemporal { get; init; }

    public bool IsCategorical { get; init; }
}
