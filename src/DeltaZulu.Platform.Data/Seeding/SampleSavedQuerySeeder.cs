namespace DeltaZulu.Platform.Data.Hunting.SavedQueries;

using System.Text;
using DeltaZulu.Platform.Application.Hunting.SavedQueries;
using DeltaZulu.Platform.Domain.Hunting.Samples;

internal static class SampleSavedQuerySeeder
{
    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static async Task SeedMissingAsync(
        ISavedQueryRepository savedQueries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(savedQueries);

        await savedQueries.EnsureInitializedAsync(cancellationToken);

        foreach (var sample in SampleQueryCatalog.All)
        {
            var id = CreateId(sample);

            if (await savedQueries.GetAsync(id, cancellationToken) is not null)
            {
                continue;
            }

            var record = new SavedQueryRecord(
                id,
                sample.Label,
                $"Seeded sample query for {sample.Category}.",
                sample.Kql.Trim(),
                SeedTimestamp,
                SeedTimestamp,
                LastRunAt: null);

            await savedQueries.SaveAsync(record, cancellationToken);
        }
    }

    internal static string CreateId(SampleQuery sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        return $"sample-{Slug(sample.Category)}-{Slug(sample.Label)}";
    }

    private static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (builder.Length == 0 || previousWasSeparator)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        if (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.ToString();
    }
}