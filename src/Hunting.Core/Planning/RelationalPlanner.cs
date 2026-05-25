namespace Hunting.Core.Planning;

using Hunting.Core.QueryModel;

public interface IRelationalPlanner
{
    RelNode Plan(RelNode root, PlannerContext context);
}

public interface IPlannerTelemetry
{
    PlannerRunStats? LastRunStats { get; }
}

public sealed record PlannerContext(bool Enabled, int MaxIterations = 3);

public sealed record PlannerRunStats(
    int Iterations,
    int TotalRulesAttempted,
    int TotalRulesApplied,
    IReadOnlyList<PlannerPassStat> PassStats);

public sealed record PlannerPassStat(string Name, int Attempted, int Applied);

internal interface IPlannerPass
{
    string Name { get; }

    RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied);
}

public sealed class RelationalPlanner : IRelationalPlanner, IPlannerTelemetry
{
    private readonly IReadOnlyList<IPlannerPass> _passes;

    public RelationalPlanner()
    {
        _passes = [
            new FilterPushdownPass(),
            new ProjectionPruningPass(),
            new IdentityProjectionCollapsePass(),
            new CommonScalarHoistPass()
        ];
    }

    public PlannerRunStats? LastRunStats { get; private set; }

    public RelNode Plan(RelNode root, PlannerContext context)
    {
        if (!context.Enabled)
        {
            LastRunStats = null;
            return root;
        }

        var current = root;
        var max = Math.Max(1, context.MaxIterations);
        var passAgg = _passes.ToDictionary(p => p.Name, _ => (attempted: 0, applied: 0), StringComparer.Ordinal);
        var iterations = 0;

        for (var i = 0; i < max; i++)
        {
            iterations++;
            var changedAny = false;

            foreach (var pass in _passes)
            {
                var next = pass.Apply(current, out var changed, out var attempted, out var applied);
                passAgg[pass.Name] = (passAgg[pass.Name].attempted + attempted, passAgg[pass.Name].applied + applied);

                if (changed)
                {
                    changedAny = true;
                    current = next;
                }
            }

            if (!changedAny)
            {
                break;
            }
        }

        var passStats = _passes
            .Select(p => new PlannerPassStat(p.Name, passAgg[p.Name].attempted, passAgg[p.Name].applied))
            .ToArray();

        LastRunStats = new PlannerRunStats(
            Iterations: iterations,
            TotalRulesAttempted: passStats.Sum(s => s.Attempted),
            TotalRulesApplied: passStats.Sum(s => s.Applied),
            PassStats: passStats);

        return current;
    }

    private sealed class FilterPushdownPass : IPlannerPass
    {
        public string Name => "FilterPushdownPass";

        public RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied)
        {
            attempted = 0;
            applied = 0;
            var rewritten = RewriteNode(node, ref attempted, ref applied);
            changed = rewritten != node;
            return rewritten;
        }

        private static RelNode RewriteNode(RelNode node, ref int attempted, ref int applied) => node switch
        {
            FilterNode f => RewriteFilter(f, ref attempted, ref applied),
            ProjectNode p => p with { Input = RewriteNode(p.Input, ref attempted, ref applied) },
            ExtendNode e => e with { Input = RewriteNode(e.Input, ref attempted, ref applied) },
            AggregateNode a => a with { Input = RewriteNode(a.Input, ref attempted, ref applied) },
            SortNode s => s with { Input = RewriteNode(s.Input, ref attempted, ref applied) },
            LimitNode l => l with { Input = RewriteNode(l.Input, ref attempted, ref applied) },
            DistinctNode d => d with { Input = RewriteNode(d.Input, ref attempted, ref applied) },
            JoinNode j => j with { Left = RewriteNode(j.Left, ref attempted, ref applied), Right = RewriteNode(j.Right, ref attempted, ref applied) },
            LetBindingNode lb => lb with
            {
                Body = RewriteNode(lb.Body, ref attempted, ref applied),
                TabularValue = lb.TabularValue is null ? null : RewriteNode(lb.TabularValue, ref attempted, ref applied)
            },
            _ => node
        };

        private static RelNode RewriteFilter(FilterNode node, ref int attempted, ref int applied)
        {
            var rewrittenInput = RewriteNode(node.Input, ref attempted, ref applied);
            var rewritten = rewrittenInput == node.Input ? node : node with { Input = rewrittenInput };

            if (rewritten.Input is ProjectNode proj)
            {
                attempted++;
                if (TryPushFilterBelowProject(rewritten.Predicate, proj, out var pushed))
                {
                    applied++;
                    return pushed;
                }
            }

            return rewritten;
        }
    }

    private sealed class IdentityProjectionCollapsePass : IPlannerPass
    {
        public string Name => "IdentityProjectionCollapsePass";

        public RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied)
        {
            attempted = 0;
            applied = 0;
            var rewritten = RewriteNode(node, ref attempted, ref applied);
            changed = rewritten != node;
            return rewritten;
        }

        private static RelNode RewriteNode(RelNode node, ref int attempted, ref int applied) => node switch
        {
            ProjectNode p => RewriteProject(p, ref attempted, ref applied),
            FilterNode f => f with { Input = RewriteNode(f.Input, ref attempted, ref applied) },
            ExtendNode e => e with { Input = RewriteNode(e.Input, ref attempted, ref applied) },
            AggregateNode a => a with { Input = RewriteNode(a.Input, ref attempted, ref applied) },
            SortNode s => s with { Input = RewriteNode(s.Input, ref attempted, ref applied) },
            LimitNode l => l with { Input = RewriteNode(l.Input, ref attempted, ref applied) },
            DistinctNode d => d with { Input = RewriteNode(d.Input, ref attempted, ref applied) },
            JoinNode j => j with { Left = RewriteNode(j.Left, ref attempted, ref applied), Right = RewriteNode(j.Right, ref attempted, ref applied) },
            LetBindingNode lb => lb with
            {
                Body = RewriteNode(lb.Body, ref attempted, ref applied),
                TabularValue = lb.TabularValue is null ? null : RewriteNode(lb.TabularValue, ref attempted, ref applied)
            },
            _ => node
        };

        private static RelNode RewriteProject(ProjectNode node, ref int attempted, ref int applied)
        {
            var rewrittenInput = RewriteNode(node.Input, ref attempted, ref applied);
            var rewritten = rewrittenInput == node.Input ? node : node with { Input = rewrittenInput };

            if (rewritten.Input is ProjectNode inner)
            {
                attempted++;
                // Only collapse when the outer projection is a true pass-through of
                // the inner one: same columns, same order. A narrowing projection
                // such as `project A,B,C | project A,B` is NOT identity — collapsing
                // it to the inner node would leak column C.
                if (IsIdentityProjection(rewritten.Projections)
                    && SameColumnSequence(rewritten.Projections, inner.Projections))
                {
                    applied++;
                    return inner;
                }
            }

            return rewritten;
        }
    }

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
                    foreach (var se in s.Sorts) CollectColumnRefs(se.Expression, sortReq);
                    return s with { Input = RewriteNode(s.Input, sortReq, ref attempted, ref applied) };

                case AggregateNode a:
                    var aggReq = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var g in a.GroupBy) CollectColumnRefs(g, aggReq);
                    foreach (var ag in a.Aggregates) CollectColumnRefs(ag.Expression, aggReq);
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
                    foreach (var pr in d.Projections) CollectColumnRefs(pr.Expression, dreq);
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

    private sealed class CommonScalarHoistPass : IPlannerPass
    {
        public string Name => "CommonScalarHoistPass";

        public RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied)
        {
            attempted = 0;
            applied = 0;
            var rewritten = RewriteNode(node, ref attempted, ref applied);
            changed = rewritten != node;
            return rewritten;
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

        private static RelNode RewriteExtend(ExtendNode e, ref int attempted, ref int applied)
        {
            var input = RewriteNode(e.Input, ref attempted, ref applied);
            var rewrittenExt = TryHoistExtensions(e.Extensions, ref attempted, ref applied, out var changed);
            if (!changed)
            {
                return input == e.Input ? e : e with { Input = input };
            }

            return new ExtendNode(input, rewrittenExt);
        }

        private static IReadOnlyList<ProjectionExpr> TryHoistExtensions(
            IReadOnlyList<ProjectionExpr> extensions,
            ref int attempted,
            ref int applied,
            out bool changed)
        {
            var firstByExpr = new Dictionary<string, string>(StringComparer.Ordinal);
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

        private static ProjectNode? TryHoist(IReadOnlyList<ProjectionExpr> projections, RelNode input, ref int attempted, ref int applied)
        {
            var firstByExpr = new Dictionary<string, string>(StringComparer.Ordinal);
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
    }

    private static bool TryPushFilterBelowProject(ScalarExpr predicate, ProjectNode project, out RelNode rewritten)
    {
        rewritten = project;

        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectColumnRefs(predicate, required);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in project.Projections)
        {
            if (p.Expression is not ColumnRef c)
            {
                continue;
            }

            if (map.ContainsKey(p.Alias))
            {
                return false;
            }

            map[p.Alias] = c.Name;
        }

        foreach (var col in required)
        {
            if (!map.ContainsKey(col))
            {
                return false;
            }
        }

        var remapped = RemapColumns(predicate, map);
        rewritten = new ProjectNode(new FilterNode(project.Input, remapped), project.Projections);
        return true;
    }

    private static bool IsIdentityProjection(IReadOnlyList<ProjectionExpr> projections)
    {
        if (projections.Count == 0)
        {
            return false;
        }

        foreach (var proj in projections)
        {
            if (proj.Expression is not ColumnRef col)
            {
                return false;
            }

            // KQL column names are case-insensitive, so an identity projection may
            // differ only in casing from its source column.
            if (!string.Equals(proj.Alias, col.Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameColumnSequence(
        IReadOnlyList<ProjectionExpr> outer,
        IReadOnlyList<ProjectionExpr> inner)
    {
        if (outer.Count != inner.Count)
        {
            return false;
        }

        for (var i = 0; i < outer.Count; i++)
        {
            if (!string.Equals(outer[i].Alias, inner[i].Alias, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string ScalarKey(ScalarExpr expr) => expr switch
    {
        // Column names are case-insensitive in KQL; qualifier distinguishes join sides.
        ColumnRef c => $"col:{c.Qualifier}:{c.Name.ToLowerInvariant()}",
        LiteralScalar l => $"lit:{l.Kind}:{l.Value}",
        BinaryScalar b => $"bin:{b.Op}:({ScalarKey(b.Left)}):({ScalarKey(b.Right)})",
        UnaryScalar u => $"un:{u.Op}:({ScalarKey(u.Operand)})",
        FunctionCall f => $"fn:{f.Name}({string.Join(',', f.Args.Select(ScalarKey))})",
        CaseScalar c => $"case:{string.Join('|', c.Branches.Select(b => ScalarKey(b.When) + "=>" + ScalarKey(b.Then)))}:else:{ScalarKey(c.Else)}",
        // The window specification (partitioning, ordering, frame) is part of the
        // expression's identity. Omitting it would treat two window functions with
        // different partitions as a common subexpression and deduplicate them,
        // producing wrong analytics.
        WindowScalarExpr w => $"win:{w.FunctionName}({string.Join(',', w.Args.Select(ScalarKey))}){WindowKey(w.Window)}",
        ListScalar l => $"list:{string.Join(',', l.Items.Select(ScalarKey))}",
        StarExpr => "star",
        _ => expr.ToString() ?? expr.GetType().Name
    };

    private static string WindowKey(WindowSpec spec)
    {
        var partition = string.Join(',', spec.PartitionBy.Select(ScalarKey));
        var order = string.Join(',', spec.OrderBy.Select(SortKey));
        var frame = spec.Frame is null ? "" : FrameKey(spec.Frame);
        return $"[part:{partition}|order:{order}|frame:{frame}]";
    }

    private static string SortKey(SortExpr s) => $"{ScalarKey(s.Expression)}:{s.Direction}:{s.Nulls}";

    private static string FrameKey(WindowFrame f) =>
        $"{f.Type}:{BoundKey(f.Start)}:{BoundKey(f.End)}";

    private static string BoundKey(WindowBound b) =>
        $"{b.Kind}:{(b.Offset is null ? "" : ScalarKey(b.Offset))}";

    private static ScalarExpr RemapColumns(ScalarExpr expr, IReadOnlyDictionary<string, string> aliasToSource) => expr switch
    {
        ColumnRef c when aliasToSource.TryGetValue(c.Name, out var src) => new ColumnRef(src),
        BinaryScalar b => b with { Left = RemapColumns(b.Left, aliasToSource), Right = RemapColumns(b.Right, aliasToSource) },
        UnaryScalar u => u with { Operand = RemapColumns(u.Operand, aliasToSource) },
        FunctionCall f => f with { Args = f.Args.Select(a => RemapColumns(a, aliasToSource)).ToArray() },
        CaseScalar c => c with
        {
            Branches = c.Branches.Select(b => (RemapColumns(b.When, aliasToSource), RemapColumns(b.Then, aliasToSource))).ToArray(),
            Else = RemapColumns(c.Else, aliasToSource)
        },
        WindowScalarExpr w => w with
        {
            Args = w.Args.Select(a => RemapColumns(a, aliasToSource)).ToArray(),
            Window = w.Window with
            {
                PartitionBy = w.Window.PartitionBy.Select(p => RemapColumns(p, aliasToSource)).ToArray(),
                OrderBy = w.Window.OrderBy.Select(o => o with { Expression = RemapColumns(o.Expression, aliasToSource) }).ToArray()
            }
        },
        ListScalar l => l with { Items = l.Items.Select(i => RemapColumns(i, aliasToSource)).ToArray() },
        _ => expr
    };

    private static void CollectColumnRefs(ScalarExpr expr, ISet<string> sink)
    {
        switch (expr)
        {
            case ColumnRef c:
                sink.Add(c.Name);
                break;

            case BinaryScalar b:
                CollectColumnRefs(b.Left, sink);
                CollectColumnRefs(b.Right, sink);
                break;

            case UnaryScalar u:
                CollectColumnRefs(u.Operand, sink);
                break;

            case FunctionCall f:
                foreach (var a in f.Args) CollectColumnRefs(a, sink);
                break;

            case CaseScalar c:
                foreach (var (w, t) in c.Branches)
                {
                    CollectColumnRefs(w, sink);
                    CollectColumnRefs(t, sink);
                }
                CollectColumnRefs(c.Else, sink);
                break;

            case WindowScalarExpr w:
                foreach (var a in w.Args) CollectColumnRefs(a, sink);
                foreach (var p in w.Window.PartitionBy) CollectColumnRefs(p, sink);
                foreach (var o in w.Window.OrderBy) CollectColumnRefs(o.Expression, sink);
                break;

            case ListScalar l:
                foreach (var i in l.Items) CollectColumnRefs(i, sink);
                break;
        }
    }
}

public sealed class NoOpRelationalPlanner : IRelationalPlanner, IPlannerTelemetry
{
    public PlannerRunStats? LastRunStats { get; private set; }

    public RelNode Plan(RelNode root, PlannerContext context)
    {
        LastRunStats = context.Enabled
            ? new PlannerRunStats(1, 0, 0, [])
            : null;
        return root;
    }
}