namespace DeltaZulu.Platform.Domain.Analytics.SavedQueries;

public sealed record SavedQueryPage(
    IReadOnlyList<SavedQueryRecord> Items,
    int TotalCount,
    int Offset,
    int Limit)
{
    public int EndOffset => Offset + Items.Count;

    public bool HasMore => EndOffset < TotalCount;
}
