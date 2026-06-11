namespace DeltaZulu.Platform.Domain.Hunting.Planning;

using DeltaZulu.Platform.Domain.Hunting.QueryModel;

public sealed partial class RelationalPlanner
{
    private sealed class LookupOutputBindingPass : IPlannerPass
    {
        public string Name => "LookupOutputBindingPass";

        public RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied)
        {
            attempted = 1;
            applied = 0;
            var rewritten = Rewrite(node, ref applied);
            changed = !ReferenceEquals(rewritten, node) && rewritten != node;
            return rewritten;
        }

        private static HashSet<string> AddAliases(HashSet<string> set, IEnumerable<string> aliases)
        {
            foreach (var alias in aliases)
            {
                set.Add(alias);
            }

            return set;
        }

        private static ProjectionExpr[] CopyProjectionExprs(IReadOnlyList<ProjectionExpr> source)
        {
            var copy = new ProjectionExpr[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static ScalarExpr[] CopyScalarExprs(IReadOnlyList<ScalarExpr> source)
        {
            var copy = new ScalarExpr[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static SortExpr[] CopySortExprs(IReadOnlyList<SortExpr> source)
        {
            var copy = new SortExpr[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static HashSet<string> JoinKeyRightNames(ScalarExpr pred)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Visit(ScalarExpr e)
            {
                if (e is BinaryScalar b && b.Op == ScalarBinaryOp.And) { Visit(b.Left); Visit(b.Right); return; }
                if (e is BinaryScalar eq && eq.Op == ScalarBinaryOp.Eq && eq.Right is ColumnRef { Qualifier: JoinSide.Right } rc)
                {
                    keys.Add(rc.Name);
                }
            }
            Visit(pred);
            return keys;
        }

        private static IReadOnlyDictionary<string, string> LookupOwners(RelNode node)
        {
            if (node is not JoinNode { Flavor: JoinFlavor.Lookup } join)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var left = OutputNames(join.Left);
            var right = OutputNames(join.Right);
            var keyCols = JoinKeyRightNames(join.OnPredicate);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in left)
            {
                map[c] = JoinSide.Left;
            }

            foreach (var c in right)
            {
                if (keyCols.Contains(c))
                {
                    continue;
                }

                if (!map.ContainsKey(c))
                {
                    map[c] = JoinSide.Right;
                }
            }
            return map;
        }

        private static HashSet<string> OutputNames(RelNode node) => node switch
        {
            ProjectNode p => ToCaseInsensitiveSet(p.Projections.Select(x => x.Alias)),
            ExtendNode e => AddAliases(OutputNames(e.Input), e.Extensions.Select(x => x.Alias)),
            AggregateNode a => AddAliases(
                ToCaseInsensitiveSet(a.Aggregates.Select(x => x.Alias)),
                a.GroupBy.OfType<ColumnRef>().Select(c => c.Name)),
            DistinctNode d => ToCaseInsensitiveSet(d.Projections.Select(x => x.Alias)),
            FilterNode f => OutputNames(f.Input),
            SortNode s => OutputNames(s.Input),
            LimitNode l => OutputNames(l.Input),
            SampleNode s => OutputNames(s.Input),
            JoinNode j => AddAliases(OutputNames(j.Left), OutputNames(j.Right)),
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };

        private static RelNode Rewrite(RelNode node, ref int applied) => node switch
        {
            ProjectNode p => RewriteProject(p, ref applied),
            FilterNode f => RewriteFilter(f, ref applied),
            SortNode s => RewriteSort(s, ref applied),
            ExtendNode e => RewriteExtend(e, ref applied),
            AggregateNode a => RewriteAggregate(a, ref applied),
            LimitNode l => l with { Input = Rewrite(l.Input, ref applied) },
            SampleNode s => s with { Input = Rewrite(s.Input, ref applied) },
            DistinctNode d => d with { Input = Rewrite(d.Input, ref applied) },
            JoinNode j => j with { Left = Rewrite(j.Left, ref applied), Right = Rewrite(j.Right, ref applied) },
            LetBindingNode lb => lb with
            {
                Body = Rewrite(lb.Body, ref applied),
                TabularValue = lb.TabularValue is null ? null : Rewrite(lb.TabularValue, ref applied)
            },
            _ => node
        };

        private static AggregateNode RewriteAggregate(AggregateNode a, ref int applied)
        {
            var input = Rewrite(a.Input, ref applied);
            var owner = LookupOwners(input);
            ScalarExpr[]? groupBy = null;
            for (var i = 0; i < a.GroupBy.Count; i++)
            {
                var rewritten = RewriteScalar(a.GroupBy[i], owner, ref applied);
                if (!Equals(rewritten, a.GroupBy[i]))
                {
                    groupBy ??= CopyScalarExprs(a.GroupBy);
                    groupBy[i] = rewritten;
                }
            }

            ProjectionExpr[]? aggregates = null;
            for (var i = 0; i < a.Aggregates.Count; i++)
            {
                var agg = a.Aggregates[i];
                var rewrittenExpr = RewriteScalar(agg.Expression, owner, ref applied);
                if (!Equals(rewrittenExpr, agg.Expression))
                {
                    aggregates ??= CopyProjectionExprs(a.Aggregates);
                    aggregates[i] = agg with { Expression = rewrittenExpr };
                }
            }

            return ReferenceEquals(input, a.Input) && groupBy is null && aggregates is null
                ? a
                : (a with
                {
                    Input = input,
                    GroupBy = groupBy ?? a.GroupBy,
                    Aggregates = aggregates ?? a.Aggregates
                });
        }

        private static ExtendNode RewriteExtend(ExtendNode e, ref int applied)
        {
            var input = Rewrite(e.Input, ref applied);
            var owner = LookupOwners(input);
            ProjectionExpr[]? ext = null;
            for (var i = 0; i < e.Extensions.Count; i++)
            {
                var expr = e.Extensions[i];
                var rewrittenExpr = RewriteScalar(expr.Expression, owner, ref applied);
                if (!Equals(rewrittenExpr, expr.Expression))
                {
                    ext ??= CopyProjectionExprs(e.Extensions);
                    ext[i] = expr with { Expression = rewrittenExpr };
                }
            }
            return ReferenceEquals(input, e.Input) && ext is null ? e : (e with { Input = input, Extensions = ext ?? e.Extensions });
        }

        private static FilterNode RewriteFilter(FilterNode f, ref int applied)
        {
            var input = Rewrite(f.Input, ref applied);
            var owner = LookupOwners(input);
            return f with { Input = input, Predicate = RewriteScalar(f.Predicate, owner, ref applied) };
        }

        private static ProjectNode RewriteProject(ProjectNode p, ref int applied)
        {
            var input = Rewrite(p.Input, ref applied);
            var owner = LookupOwners(input);
            ProjectionExpr[]? projections = null;
            for (var i = 0; i < p.Projections.Count; i++)
            {
                var proj = p.Projections[i];
                var rewrittenExpr = RewriteScalar(proj.Expression, owner, ref applied);
                if (!Equals(rewrittenExpr, proj.Expression))
                {
                    projections ??= CopyProjectionExprs(p.Projections);
                    projections[i] = proj with { Expression = rewrittenExpr };
                }
            }
            return ReferenceEquals(input, p.Input) && projections is null
                ? p
                : (p with { Input = input, Projections = projections ?? p.Projections });
        }

        private static ScalarExpr RewriteScalar(ScalarExpr expr, IReadOnlyDictionary<string, string> owner, ref int applied)
        {
            switch (expr)
            {
                case ColumnRef { Qualifier: null } c when owner.TryGetValue(c.Name, out var q):
                    applied++;
                    return new ColumnRef(c.Name, q);

                case BinaryScalar b:
                    return b with
                    {
                        Left = RewriteScalar(b.Left, owner, ref applied),
                        Right = RewriteScalar(b.Right, owner, ref applied)
                    };

                case UnaryScalar u:
                    return u with { Operand = RewriteScalar(u.Operand, owner, ref applied) };

                case FunctionCall f:
                    {
                        var args = new ScalarExpr[f.Args.Count];
                        for (var i = 0; i < f.Args.Count; i++)
                        {
                            args[i] = RewriteScalar(f.Args[i], owner, ref applied);
                        }

                        return f with { Args = args };
                    }
                case CaseScalar c:
                    {
                        var branches = new (ScalarExpr When, ScalarExpr Then)[c.Branches.Count];
                        for (var i = 0; i < c.Branches.Count; i++)
                        {
                            var b = c.Branches[i];
                            branches[i] = (RewriteScalar(b.When, owner, ref applied), RewriteScalar(b.Then, owner, ref applied));
                        }
                        return c with { Branches = branches, Else = RewriteScalar(c.Else, owner, ref applied) };
                    }
                case WindowScalarExpr w:
                    {
                        var args = new ScalarExpr[w.Args.Count];
                        for (var i = 0; i < w.Args.Count; i++)
                        {
                            args[i] = RewriteScalar(w.Args[i], owner, ref applied);
                        }

                        var part = new ScalarExpr[w.Window.PartitionBy.Count];
                        for (var i = 0; i < part.Length; i++)
                        {
                            part[i] = RewriteScalar(w.Window.PartitionBy[i], owner, ref applied);
                        }

                        var order = new SortExpr[w.Window.OrderBy.Count];
                        for (var i = 0; i < order.Length; i++)
                        {
                            var o = w.Window.OrderBy[i];
                            order[i] = o with { Expression = RewriteScalar(o.Expression, owner, ref applied) };
                        }
                        return w with { Args = args, Window = w.Window with { PartitionBy = part, OrderBy = order } };
                    }
                case ListScalar l:
                    {
                        var items = new ScalarExpr[l.Items.Count];
                        for (var i = 0; i < l.Items.Count; i++)
                        {
                            items[i] = RewriteScalar(l.Items[i], owner, ref applied);
                        }

                        return l with { Items = items };
                    }
                default:
                    return expr;
            }
        }

        private static SortNode RewriteSort(SortNode s, ref int applied)
        {
            var input = Rewrite(s.Input, ref applied);
            var owner = LookupOwners(input);
            SortExpr[]? sorts = null;
            for (var i = 0; i < s.Sorts.Count; i++)
            {
                var sort = s.Sorts[i];
                var rewrittenExpr = RewriteScalar(sort.Expression, owner, ref applied);
                if (!Equals(rewrittenExpr, sort.Expression))
                {
                    sorts ??= CopySortExprs(s.Sorts);
                    sorts[i] = sort with { Expression = rewrittenExpr };
                }
            }
            return ReferenceEquals(input, s.Input) && sorts is null ? s : (s with { Input = input, Sorts = sorts ?? s.Sorts });
        }

        private static HashSet<string> ToCaseInsensitiveSet(IEnumerable<string> source)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in source)
            {
                set.Add(item);
            }

            return set;
        }
    }
}