namespace Hunting.Core.Planning;

using Hunting.Core.QueryModel;

public sealed partial class RelationalPlanner
{
    private sealed class ProjectionPruningPass : IPlannerPass
    {
        public string Name => "ProjectionPruningPass";

        public RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied)
        {
            attempted = 0;
            applied = 0;
            // KQL column names are case-insensitive, so the required-column sets
            // tracked through pruning must compare case-insensitively. Using an
            // ordinal comparer would treat `ProcessId` and `processid` as different
            // columns and prune one that is actually still referenced downstream.
            var rewritten = RewriteNode(node, new HashSet<string>(StringComparer.OrdinalIgnoreCase), ref attempted, ref applied);
            changed = rewritten != node;
            return rewritten;
        }

        private static RelNode RewriteNode(RelNode node, HashSet<string> required, ref int attempted, ref int applied)
        {
            switch (node)
            {
                case ProjectNode p:
                    var projRequired = required.Count == 0
                        ? p.Projections.Select(x => x.Alias).ToHashSet(StringComparer.OrdinalIgnoreCase)
                        : required;

                    var kept = new List<ProjectionExpr>();
                    foreach (var pr in p.Projections)
                    {
                        attempted++;
                        if (projRequired.Contains(pr.Alias))
                        {
                            kept.Add(pr);
                        }
                        else
                        {
                            applied++;
                        }
                    }

                    var inputReq = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pr in kept)
                    {
                        CollectColumnRefs(pr.Expression, inputReq);
                    }

                    var inNode = RewriteNode(p.Input, inputReq, ref attempted, ref applied);
                    var pruned = kept.Count == p.Projections.Count ? p : p with { Projections = kept };
                    return pruned with { Input = inNode };

                case FilterNode f:
                    var filterReq = new HashSet<string>(required, StringComparer.OrdinalIgnoreCase);
                    CollectColumnRefs(f.Predicate, filterReq);
                    return f with { Input = RewriteNode(f.Input, filterReq, ref attempted, ref applied) };

                case SortNode s:
                    var sortReq = new HashSet<string>(required, StringComparer.OrdinalIgnoreCase);

                    // Root/visible sort preserves its input row shape. Do not let ORDER BY
                    // columns become the visible projection requirement; otherwise
                    // project A, B, C | sort by B | take N incorrectly becomes project B.
                    if (required.Count > 0)
                    {
                        foreach (var se in s.Sorts)
                        {
                            CollectColumnRefs(se.Expression, sortReq);
                        }
                    }

                    return s with { Input = RewriteNode(s.Input, sortReq, ref attempted, ref applied) };

                case AggregateNode a:
                    var aggReq = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var g in a.GroupBy)
                    {
                        CollectColumnRefs(g, aggReq);
                    }

                    foreach (var ag in a.Aggregates)
                    {
                        CollectColumnRefs(ag.Expression, aggReq);
                    }

                    return a with { Input = RewriteNode(a.Input, aggReq, ref attempted, ref applied) };

                case ExtendNode e:
                    var extAliases = e.Extensions.Select(x => x.Alias).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var extReq = required.Count == 0
                        ? extAliases
                        : required;

                    var extInReq = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Pass-through columns from input remain visible after extend via SELECT *.
                    // If downstream requires a column that is not an extension alias, preserve it.
                    foreach (var req in extReq)
                    {
                        if (!extAliases.Contains(req))
                        {
                            extInReq.Add(req);
                        }
                    }

                    foreach (var ex in e.Extensions)
                    {
                        if (extReq.Contains(ex.Alias))
                        {
                            CollectColumnRefs(ex.Expression, extInReq);
                        }
                    }
                    return e with { Input = RewriteNode(e.Input, extInReq, ref attempted, ref applied) };

                case DistinctNode d:
                    var dreq = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pr in d.Projections)
                    {
                        CollectColumnRefs(pr.Expression, dreq);
                    }

                    return d with { Input = RewriteNode(d.Input, dreq, ref attempted, ref applied) };

                case LimitNode l:
                    return l with { Input = RewriteNode(l.Input, required, ref attempted, ref applied) };

                case JoinNode j:
                    // conservative: do not prune across join boundaries in v1
                    return j with { Left = RewriteNode(j.Left, new HashSet<string>(StringComparer.OrdinalIgnoreCase), ref attempted, ref applied), Right = RewriteNode(j.Right, new HashSet<string>(StringComparer.OrdinalIgnoreCase), ref attempted, ref applied) };

                case LetBindingNode lb:
                    return lb with
                    {
                        Body = RewriteNode(lb.Body, required, ref attempted, ref applied),
                        TabularValue = lb.TabularValue is null ? null : RewriteNode(lb.TabularValue, new HashSet<string>(StringComparer.OrdinalIgnoreCase), ref attempted, ref applied)
                    };

                default:
                    return node;
            }
        }
    }
}
