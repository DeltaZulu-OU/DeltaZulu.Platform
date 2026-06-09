namespace Hunting.Core.DuckDbSql;

internal sealed class DuckDbStageRegistry
{
    private int _stageCounter;
    private readonly List<(string Name, string Sql)> _ctes = [];
    private readonly HashSet<string> _stageNames = new(StringComparer.Ordinal);
    private Dictionary<string, int>? _stageIndexByName;
    private Dictionary<string, int>? _stageReferenceCounts;

    public IReadOnlyList<(string Name, string Sql)> Ctes => _ctes;

    public int StageAdds { get; private set; }
    public int StageRemoves { get; private set; }
    public int StageIndexBuilds { get; private set; }
    public int StageRefCountBuilds { get; private set; }
    public int StageIndexLookups { get; private set; }
    public int StageRefCountLookups { get; private set; }
    public int CacheInvalidations { get; private set; }

    public string NextStage() => $"__kql_stage_{_stageCounter++}";

    public int GetStageIndex(string stageName)
    {
        StageIndexLookups++;
        EnsureStageIndex();
        return _stageIndexByName!.TryGetValue(stageName, out var idx) ? idx : -1;
    }

    public void InvalidateCaches()
    {
        CacheInvalidations++;
        _stageIndexByName = null;
        _stageReferenceCounts = null;
    }

    public void AddStage(string stage, string sql)
    {
        _ctes.Add((stage, sql));
        _stageNames.Add(stage);
        StageAdds++;
        InvalidateCaches();
    }

    public void RemoveStageAt(int idx)
    {
        var stageName = _ctes[idx].Name;
        _ctes.RemoveAt(idx);
        _stageNames.Remove(stageName);
        StageRemoves++;
        InvalidateCaches();
    }

    public void RemoveStagesAtWithoutTracking(int firstIdx, int secondIdx)
    {
        var firstName = _ctes[firstIdx].Name;
        _ctes.RemoveAt(firstIdx);
        var secondName = _ctes[secondIdx].Name;
        _ctes.RemoveAt(secondIdx);
        _stageNames.Remove(firstName);
        _stageNames.Remove(secondName);
        InvalidateCaches();
    }

    public void ReplaceStageSql(int idx, string sql) => _ctes[idx] = (_ctes[idx].Name, sql);

    public bool IsStageReference(string source) => _stageNames.Contains(source);

    public Dictionary<string, int> BuildStageRefCounts()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cte in _ctes)
        {
            foreach (var stageRef in EnumerateStageReferences(cte.Sql))
            {
                counts[stageRef] = counts.TryGetValue(stageRef, out var count) ? count + 1 : 1;
            }
        }

        return counts;
    }

    public int CountStageReferences(string stageName)
    {
        StageRefCountLookups++;
        EnsureStageReferenceCounts();
        return _stageReferenceCounts!.TryGetValue(stageName, out var count) ? count : 0;
    }

    private void EnsureStageIndex()
    {
        if (_stageIndexByName is not null)
        {
            return;
        }
        StageIndexBuilds++;

        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < _ctes.Count; i++)
        {
            index[_ctes[i].Name] = i;
        }

        _stageIndexByName = index;
    }

    private void EnsureStageReferenceCounts()
    {
        if (_stageReferenceCounts is not null)
        {
            return;
        }

        _stageReferenceCounts = BuildStageRefCounts();
        StageRefCountBuilds++;
    }

    private static IEnumerable<string> EnumerateStageReferences(string sql)
    {
        const string prefix = "__kql_stage_";
        var idx = 0;
        while (true)
        {
            idx = sql.IndexOf(prefix, idx, StringComparison.Ordinal);
            if (idx < 0)
            {
                yield break;
            }

            var end = idx + prefix.Length;
            while (end < sql.Length && char.IsAsciiDigit(sql[end]))
            {
                end++;
            }

            if (end > idx + prefix.Length)
            {
                yield return sql[idx..end];
            }

            idx = end;
        }
    }
}
