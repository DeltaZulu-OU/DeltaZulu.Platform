
using DeltaZulu.Platform.Application.Analytics.Planning;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Domain.Analytics.Planning;
public sealed record PlannerContext(bool Enabled, int MaxIterations = 3, int MaxRuleApplications = 10_000);

public sealed record PlannerRunStats(
    int Iterations,
    int TotalRulesAttempted,
    int TotalRulesApplied,
    bool HitRuleApplicationBudget,
    IReadOnlyList<PlannerPassStat> PassStats);

public sealed record PlannerPassStat(string Name, int Attempted, int Applied);

public sealed partial class RelationalPlanner : IRelationalPlanner, IPlannerTelemetry
{
    private static IPlannerPass[] CreateDefaultPasses() => [
    new LookupOutputBindingPass(),
        new FilterPushdownPass(),
        new FilterExtendInlinePass(),
        new ProjectionPruningPass(),
        new IdentityProjectionCollapsePass(),
        new CommonScalarHoistPass()
];

    private readonly IReadOnlyList<IPlannerPass> _passes;

    public RelationalPlanner() : this(CreateDefaultPasses())
    {
    }

    internal RelationalPlanner(IReadOnlyList<IPlannerPass> passes)
    {
        ArgumentNullException.ThrowIfNull(passes);
        if (passes.Count == 0)
        {
            throw new ArgumentException("Planner must have at least one pass.", nameof(passes));
        }

        _passes = passes;
    }

    public PlannerRunStats? LastRunStats { get; private set; }

    public RelNode Plan(RelNode root, PlannerContext context)
    {
        if (!context.Enabled)
        {
            LastRunStats = null;
            return root;
        }

        var result = PlannerRunner.Run(root, context, _passes);
        LastRunStats = result.Stats;
        return result.Node;
    }

    private static string BoundKey(WindowBound b) =>
        $"{b.Kind}:{(b.Offset is null ? "" : ScalarKey(b.Offset))}";

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
                foreach (var a in f.Args)
                {
                    CollectColumnRefs(a, sink);
                }

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
                foreach (var a in w.Args)
                {
                    CollectColumnRefs(a, sink);
                }

                foreach (var p in w.Window.PartitionBy)
                {
                    CollectColumnRefs(p, sink);
                }

                foreach (var o in w.Window.OrderBy)
                {
                    CollectColumnRefs(o.Expression, sink);
                }

                break;

            case ListScalar l:
                foreach (var i in l.Items)
                {
                    CollectColumnRefs(i, sink);
                }

                break;
        }
    }

    private static bool ContainsUnqualifiedColumnRef(ScalarExpr expr, string name)
    {
        switch (expr)
        {
            case ColumnRef c:
                return c.Qualifier is null && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase);

            case BinaryScalar b:
                return ContainsUnqualifiedColumnRef(b.Left, name) || ContainsUnqualifiedColumnRef(b.Right, name);

            case UnaryScalar u:
                return ContainsUnqualifiedColumnRef(u.Operand, name);

            case FunctionCall f:
                foreach (var a in f.Args)
                {
                    if (ContainsUnqualifiedColumnRef(a, name))
                    {
                        return true;
                    }
                }

                return false;

            case CaseScalar cs:
                foreach (var (when, then) in cs.Branches)
                {
                    if (ContainsUnqualifiedColumnRef(when, name) || ContainsUnqualifiedColumnRef(then, name))
                    {
                        return true;
                    }
                }

                return ContainsUnqualifiedColumnRef(cs.Else, name);

            case WindowScalarExpr w:
                foreach (var a in w.Args)
                {
                    if (ContainsUnqualifiedColumnRef(a, name))
                    {
                        return true;
                    }
                }

                foreach (var p in w.Window.PartitionBy)
                {
                    if (ContainsUnqualifiedColumnRef(p, name))
                    {
                        return true;
                    }
                }

                foreach (var o in w.Window.OrderBy)
                {
                    if (ContainsUnqualifiedColumnRef(o.Expression, name))
                    {
                        return true;
                    }
                }

                return false;

            case ListScalar l:
                foreach (var i in l.Items)
                {
                    if (ContainsUnqualifiedColumnRef(i, name))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }

    private static int CountColumnRefOccurrences(ScalarExpr expr, string name)
    {
        var count = 0;
        void Visit(ScalarExpr e)
        {
            switch (e)
            {
                case ColumnRef c when c.Qualifier is null && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase):
                    count++;
                    break;

                case BinaryScalar b:
                    Visit(b.Left);
                    Visit(b.Right);
                    break;

                case UnaryScalar u:
                    Visit(u.Operand);
                    break;

                case FunctionCall f:
                    foreach (var a in f.Args)
                    {
                        Visit(a);
                    }

                    break;

                case CaseScalar cs:
                    foreach (var (w, t) in cs.Branches)
                    {
                        Visit(w);
                        Visit(t);
                    }
                    Visit(cs.Else);
                    break;

                case WindowScalarExpr w:
                    foreach (var a in w.Args)
                    {
                        Visit(a);
                    }

                    foreach (var p in w.Window.PartitionBy)
                    {
                        Visit(p);
                    }

                    foreach (var o in w.Window.OrderBy)
                    {
                        Visit(o.Expression);
                    }

                    break;

                case ListScalar l:
                    foreach (var i in l.Items)
                    {
                        Visit(i);
                    }

                    break;
            }
        }

        Visit(expr);
        return count;
    }

    private static string FrameKey(WindowFrame f) =>
        $"{f.Type}:{BoundKey(f.Start)}:{BoundKey(f.End)}";

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

    private static IReadOnlyList<ScalarExpr> RemapArgs(IReadOnlyList<ScalarExpr> args, IReadOnlyDictionary<string, string> aliasToSource)
    {
        if (args.Count == 0)
        {
            return [];
        }

        var remapped = new ScalarExpr[args.Count];
        for (var i = 0; i < args.Count; i++)
        {
            remapped[i] = RemapColumns(args[i], aliasToSource);
        }

        return remapped;
    }

    private static IReadOnlyList<(ScalarExpr When, ScalarExpr Then)> RemapBranches(
        IReadOnlyList<(ScalarExpr When, ScalarExpr Then)> branches,
        IReadOnlyDictionary<string, string> aliasToSource)
    {
        if (branches.Count == 0)
        {
            return [];
        }

        var mapped = new (ScalarExpr When, ScalarExpr Then)[branches.Count];
        for (var i = 0; i < branches.Count; i++)
        {
            mapped[i] = (RemapColumns(branches[i].When, aliasToSource), RemapColumns(branches[i].Then, aliasToSource));
        }

        return mapped;
    }

    private static ScalarExpr RemapColumns(ScalarExpr expr, IReadOnlyDictionary<string, string> aliasToSource)
    {
        if (aliasToSource.Count == 0)
        {
            return expr;
        }

        return expr switch
        {
            ColumnRef c when aliasToSource.TryGetValue(c.Name, out var src) => new ColumnRef(src),
            BinaryScalar b => b with { Left = RemapColumns(b.Left, aliasToSource), Right = RemapColumns(b.Right, aliasToSource) },
            UnaryScalar u => u with { Operand = RemapColumns(u.Operand, aliasToSource) },
            FunctionCall f => f with { Args = RemapArgs(f.Args, aliasToSource) },
            CaseScalar c => c with
            {
                Branches = RemapBranches(c.Branches, aliasToSource),
                Else = RemapColumns(c.Else, aliasToSource)
            },
            WindowScalarExpr w => w with
            {
                Args = RemapArgs(w.Args, aliasToSource),
                Window = w.Window with
                {
                    PartitionBy = RemapArgs(w.Window.PartitionBy, aliasToSource),
                    OrderBy = RemapSorts(w.Window.OrderBy, aliasToSource)
                }
            },
            ListScalar l => l with { Items = RemapArgs(l.Items, aliasToSource) },
            _ => expr
        };
    }

    private static IReadOnlyList<SortExpr> RemapSorts(IReadOnlyList<SortExpr> sorts, IReadOnlyDictionary<string, string> aliasToSource)
    {
        if (sorts.Count == 0)
        {
            return [];
        }

        var mapped = new SortExpr[sorts.Count];
        for (var i = 0; i < sorts.Count; i++)
        {
            mapped[i] = sorts[i] with { Expression = RemapColumns(sorts[i].Expression, aliasToSource) };
        }

        return mapped;
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
        FunctionCall f => $"fn:{f.Name.ToLowerInvariant()}({string.Join(',', f.Args.Select(ScalarKey))})",
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

    private static string SortKey(SortExpr s) => $"{ScalarKey(s.Expression)}:{s.Direction}:{s.Nulls}";

    private static IReadOnlyList<ScalarExpr> SubstituteArgs(IReadOnlyList<ScalarExpr> args, string name, ScalarExpr replacement)
    {
        if (args.Count == 0)
        {
            return [];
        }

        var substituted = new ScalarExpr[args.Count];
        for (var i = 0; i < args.Count; i++)
        {
            substituted[i] = SubstituteColumn(args[i], name, replacement);
        }

        return substituted;
    }

    private static IReadOnlyList<(ScalarExpr When, ScalarExpr Then)> SubstituteBranches(
        IReadOnlyList<(ScalarExpr When, ScalarExpr Then)> branches,
        string name,
        ScalarExpr replacement)
    {
        if (branches.Count == 0)
        {
            return [];
        }

        var mapped = new (ScalarExpr When, ScalarExpr Then)[branches.Count];
        for (var i = 0; i < branches.Count; i++)
        {
            mapped[i] = (SubstituteColumn(branches[i].When, name, replacement), SubstituteColumn(branches[i].Then, name, replacement));
        }

        return mapped;
    }

    private static ScalarExpr SubstituteColumn(ScalarExpr expr, string name, ScalarExpr replacement)
    {
        if (!ContainsUnqualifiedColumnRef(expr, name))
        {
            return expr;
        }

        return expr switch
        {
            ColumnRef c when c.Qualifier is null && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase) => replacement,
            BinaryScalar b => b with { Left = SubstituteColumn(b.Left, name, replacement), Right = SubstituteColumn(b.Right, name, replacement) },
            UnaryScalar u => u with { Operand = SubstituteColumn(u.Operand, name, replacement) },
            FunctionCall f => f with { Args = SubstituteArgs(f.Args, name, replacement) },
            CaseScalar cs => cs with
            {
                Branches = SubstituteBranches(cs.Branches, name, replacement),
                Else = SubstituteColumn(cs.Else, name, replacement)
            },
            WindowScalarExpr w => w with
            {
                Args = SubstituteArgs(w.Args, name, replacement),
                Window = w.Window with
                {
                    PartitionBy = SubstituteArgs(w.Window.PartitionBy, name, replacement),
                    OrderBy = SubstituteSorts(w.Window.OrderBy, name, replacement)
                }
            },
            ListScalar l => l with { Items = SubstituteArgs(l.Items, name, replacement) },
            _ => expr
        };
    }

    private static IReadOnlyList<SortExpr> SubstituteSorts(IReadOnlyList<SortExpr> sorts, string name, ScalarExpr replacement)
    {
        if (sorts.Count == 0)
        {
            return [];
        }

        var mapped = new SortExpr[sorts.Count];
        for (var i = 0; i < sorts.Count; i++)
        {
            mapped[i] = sorts[i] with { Expression = SubstituteColumn(sorts[i].Expression, name, replacement) };
        }

        return mapped;
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

    private static string WindowKey(WindowSpec spec)
    {
        var partition = string.Join(',', spec.PartitionBy.Select(ScalarKey));
        var order = string.Join(',', spec.OrderBy.Select(SortKey));
        var frame = spec.Frame is null ? "" : FrameKey(spec.Frame);
        return $"[part:{partition}|order:{order}|frame:{frame}]";
    }
}