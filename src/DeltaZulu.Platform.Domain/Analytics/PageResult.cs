namespace DeltaZulu.Platform.Domain.Analytics;

public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Offset,
    int Limit)
{
    public int EndOffset => Offset + Items.Count;

    public bool HasMore => EndOffset < TotalCount;
}
