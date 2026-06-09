namespace DeltaZulu.Hunting.Core.Planning;

using DeltaZulu.Hunting.Core.QueryModel;

public sealed partial class RelationalPlanner
{
    private sealed class CommonScalarHoistPass : IPlannerPass
    {
        private const int MinDuplicateCountToHoist = 2;
        private const int MinScalarComplexityToHoist = 3;

        public string Name => "CommonScalarHoistPass";

        public RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied)
        {
            attempted = 0;
            applied = 0;
            var rewritten = RewriteNode(node, ref attempted, ref applied);
            changed = rewritten != node;
            return rewritten;
        }

        private static int EstimateScalarComplexity(ScalarExpr expression) => expression switch
        {
            ColumnRef => 1,
            LiteralScalar => 1,
            UnaryScalar u => 1 + EstimateScalarComplexity(u.Operand),
            BinaryScalar b => 1 + EstimateScalarComplexity(b.Left) + EstimateScalarComplexity(b.Right),
            FunctionCall f => 2 + f.Args.Sum(EstimateScalarComplexity),
            CaseScalar c => 2 + c.Branches.Sum(b => EstimateScalarComplexity(b.When) + EstimateScalarComplexity(b.Then)) + EstimateScalarComplexity(c.Else),
            ListScalar l => 1 + l.Items.Sum(EstimateScalarComplexity),
            WindowScalarExpr w => 3 + w.Args.Sum(EstimateScalarComplexity) + w.Window.PartitionBy.Sum(EstimateScalarComplexity) + w.Window.OrderBy.Sum(o => EstimateScalarComplexity(o.Expression)),
            _ => 2
        };

        private static RelNode RewriteExtend(ExtendNode e, ref int attempted, ref int applied)
        {
            var input = RewriteNode(e.Input, ref attempted, ref applied);
            var rewrittenExt = TryHoistExtensions(e.Extensions, ref attempted, ref applied, out var changed);
            return !changed ? input == e.Input ? e : e with { Input = input } : new ExtendNode(input, rewrittenExt);
        }

        private static RelNode RewriteNode(RelNode node, ref int attempted, ref int applied) => node switch
        {
            ProjectNode p => RewriteProject(p, ref attempted, ref applied),
            ExtendNode e => RewriteExtend(e, ref attempted, ref applied),
            FilterNode f => f with { Input = RewriteNode(f.Input, ref attempted, ref applied) },
            AggregateNode a => a with { Input = RewriteNode(a.Input, ref attempted, ref applied) },
            SortNode s => s with { Input = RewriteNode(s.Input, ref attempted, ref applied) },
            LimitNode l => l with { Input = RewriteNode(l.Input, ref attempted, ref applied) },
            DistinctNode d => d with { Input = RewriteNode(d.Input, ref attempted, ref applied) },
            JoinNode j => j with { Left = RewriteNode(j.Left, ref attempted, ref applied), Right = RewriteNode(j.Right, ref attempted, ref applied) },
            LetBindingNode lb => lb with { Body = RewriteNode(lb.Body, ref attempted, ref applied), TabularValue = lb.TabularValue is null ? null : RewriteNode(lb.TabularValue, ref attempted, ref applied) },
            _ => node
        };

        private static RelNode RewriteProject(ProjectNode p, ref int attempted, ref int applied)
        {
            var input = RewriteNode(p.Input, ref attempted, ref applied);
            var ext = TryHoist(p.Projections, input, ref attempted, ref applied);
            return ext ?? (input == p.Input ? p : p with { Input = input });
        }

        private static bool ShouldHoist(IReadOnlyDictionary<string, int> keyCounts, string key, ScalarExpr expression)
        {
            if (!keyCounts.TryGetValue(key, out var count) || count < MinDuplicateCountToHoist)
            {
                return false;
            }

            return EstimateScalarComplexity(expression) >= MinScalarComplexityToHoist;
        }

        private static ProjectNode? TryHoist(IReadOnlyList<ProjectionExpr> projections, RelNode input, ref int attempted, ref int applied)
        {
            var firstByExpr = new Dictionary<string, string>(StringComparer.Ordinal);
            var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var projection in projections)
            {
                if (projection.Expression is ColumnRef)
                {
                    continue;
                }

                var key = ScalarKey(projection.Expression);
                keyCounts.TryGetValue(key, out var count);
                keyCounts[key] = count + 1;
            }

            var ext = new List<ProjectionExpr>();
            var outProj = new List<ProjectionExpr>();
            var changed = false;

            foreach (var p in projections)
            {
                attempted++;
                if (p.Expression is ColumnRef)
                {
                    outProj.Add(p);
                    continue;
                }

                var key = ScalarKey(p.Expression);
                if (!ShouldHoist(keyCounts, key, p.Expression))
                {
                    outProj.Add(p);
                    continue;
                }

                if (firstByExpr.TryGetValue(key, out var existingAlias))
                {
                    outProj.Add(new ProjectionExpr(p.Alias, new ColumnRef(existingAlias)));
                    applied++;
                    changed = true;
                }
                else
                {
                    firstByExpr[key] = p.Alias;
                    // The ExtendNode computes the expression once as p.Alias; the
                    // outer projection must reference that hoisted column, not
                    // re-emit the full expression (which would evaluate it twice).
                    ext.Add(p);
                    outProj.Add(new ProjectionExpr(p.Alias, new ColumnRef(p.Alias)));
                }
            }

            if (!changed)
            {
                return null;
            }

            var inner = new ExtendNode(input, ext);
            return new ProjectNode(inner, outProj);
        }

        private static IReadOnlyList<ProjectionExpr> TryHoistExtensions(
                            IReadOnlyList<ProjectionExpr> extensions,
            ref int attempted,
            ref int applied,
            out bool changed)
        {
            var firstByExpr = new Dictionary<string, string>(StringComparer.Ordinal);
            var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var extension in extensions)
            {
                if (extension.Expression is ColumnRef)
                {
                    continue;
                }

                var key = ScalarKey(extension.Expression);
                keyCounts.TryGetValue(key, out var count);
                keyCounts[key] = count + 1;
            }

            var rewritten = new List<ProjectionExpr>();
            changed = false;

            foreach (var e in extensions)
            {
                attempted++;
                if (e.Expression is ColumnRef)
                {
                    rewritten.Add(e);
                    continue;
                }

                var key = ScalarKey(e.Expression);
                if (!ShouldHoist(keyCounts, key, e.Expression))
                {
                    rewritten.Add(e);
                    continue;
                }

                if (firstByExpr.TryGetValue(key, out var existingAlias))
                {
                    rewritten.Add(new ProjectionExpr(e.Alias, new ColumnRef(existingAlias)));
                    applied++;
                    changed = true;
                }
                else
                {
                    firstByExpr[key] = e.Alias;
                    rewritten.Add(e);
                }
            }

            return rewritten;
        }
    }
}
