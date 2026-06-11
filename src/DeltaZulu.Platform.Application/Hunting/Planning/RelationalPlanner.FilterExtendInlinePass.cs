
using DeltaZulu.Platform.Application.Hunting.Planning;
using DeltaZulu.Platform.Domain.Hunting.QueryModel;

namespace DeltaZulu.Platform.Domain.Hunting.Planning;
public sealed partial class RelationalPlanner
{
    /// <summary>
    /// <para>
    /// Inlines a computed <see cref="ExtendNode"/> column into a directly-following
    /// <see cref="FilterNode"/> predicate when that column is consumed only by the
    /// filter. This removes a throwaway boolean/intermediate column (and its CTE
    /// stage) that exists solely to feed a <c>where</c>:
    /// </para>
    /// <para><c>... | extend Flag = expr | where Flag</c>  →  <c>... | where expr</c></para>
    /// <para>
    /// The rewrite is gated on liveness: <c>required</c> tracks the columns
    /// downstream operators still need, so a column kept in the <c>project</c>
    /// output (or any ancestor) is never inlined away. It is also gated on a single
    /// reference in the predicate so the expression is not duplicated, and on the
    /// extension not being referenced by a sibling extension in the same node.
    /// </para>
    /// </summary>
    private sealed class FilterExtendInlinePass : IPlannerPass
    {
        public string Name => "FilterExtendInlinePass";

        public RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied)
        {
            attempted = 0;
            applied = 0;
            var rewritten = Rewrite(node, null, ref attempted, ref applied);
            changed = rewritten != node;
            return rewritten;
        }

        private static HashSet<string> AggregateInputRequired(AggregateNode a)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in a.GroupBy)
            {
                CollectColumnRefs(g, set);
            }

            foreach (var ag in a.Aggregates)
            {
                CollectColumnRefs(ag.Expression, set);
            }

            return set;
        }

        private static HashSet<string>? ExtendInputRequired(HashSet<string>? required, ExtendNode e)
        {
            if (required is null)
            {
                return null;
            }

            var aliases = e.Extensions.Select(x => x.Alias).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in required)
            {
                if (!aliases.Contains(r))
                {
                    set.Add(r);
                }
            }

            foreach (var ex in e.Extensions)
            {
                CollectColumnRefs(ex.Expression, set);
            }

            return set;
        }

        private static HashSet<string>? PassThrough(HashSet<string>? required, ScalarExpr extra)
        {
            if (required is null)
            {
                return null;
            }

            var set = new HashSet<string>(required, StringComparer.OrdinalIgnoreCase);
            CollectColumnRefs(extra, set);
            return set;
        }

        private static HashSet<string>? PassThrough(HashSet<string>? required, IEnumerable<ScalarExpr> extras)
        {
            if (required is null)
            {
                return null;
            }

            var set = new HashSet<string>(required, StringComparer.OrdinalIgnoreCase);
            foreach (var e in extras)
            {
                CollectColumnRefs(e, set);
            }

            return set;
        }

        private static HashSet<string> ResetTo(IReadOnlyList<ProjectionExpr> projections)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in projections)
            {
                CollectColumnRefs(p.Expression, set);
            }

            return set;
        }

        // `required` is the set of column names that ancestors consume from this
        // node's output. `null` means "all output columns are required" — used at
        // the query root (every column is returned) and across boundaries (joins,
        // tabular let values) where the live set cannot be tracked precisely. A
        // null set conservatively disables inlining, because no column can be
        // proven dead.
        private static RelNode Rewrite(RelNode node, HashSet<string>? required, ref int attempted, ref int applied) => node switch
        {
            FilterNode f when f.Input is ExtendNode ext => RewriteFilterOverExtend(f, ext, required, ref attempted, ref applied),
            FilterNode f => f with { Input = Rewrite(f.Input, PassThrough(required, f.Predicate), ref attempted, ref applied) },
            // A projection redefines the output schema to its aliases, so the
            // incoming required set no longer applies below it; the input must
            // expose the columns the projection expressions reference.
            ProjectNode p => p with { Input = Rewrite(p.Input, ResetTo(p.Projections), ref attempted, ref applied) },
            DistinctNode d => d with { Input = Rewrite(d.Input, ResetTo(d.Projections), ref attempted, ref applied) },
            AggregateNode a => a with { Input = Rewrite(a.Input, AggregateInputRequired(a), ref attempted, ref applied) },
            ExtendNode e => e with { Input = Rewrite(e.Input, ExtendInputRequired(required, e), ref attempted, ref applied) },
            SortNode s => s with { Input = Rewrite(s.Input, PassThrough(required, s.Sorts.Select(x => x.Expression)), ref attempted, ref applied) },
            LimitNode l => l with { Input = Rewrite(l.Input, required, ref attempted, ref applied) },
            SampleNode sm => sm with { Input = Rewrite(sm.Input, required, ref attempted, ref applied) },
            JoinNode j => j with
            {
                Left = Rewrite(j.Left, null, ref attempted, ref applied),
                Right = Rewrite(j.Right, null, ref attempted, ref applied)
            },
            LetBindingNode lb => lb with
            {
                Body = Rewrite(lb.Body, required, ref attempted, ref applied),
                TabularValue = lb.TabularValue is null ? null : Rewrite(lb.TabularValue, null, ref attempted, ref applied)
            },
            _ => node,
        };

        private static RelNode RewriteFilterOverExtend(
            FilterNode f,
            ExtendNode ext,
            HashSet<string>? required,
            ref int attempted,
            ref int applied)
        {
            var predicate = f.Predicate;
            var remaining = new List<ProjectionExpr>();
            var inlinedAny = false;

            foreach (var extension in ext.Extensions)
            {
                attempted++;

                // Inline only when the extension column is:
                //  - provably dead above the filter (not in `required`, set known),
                //  - an actual computation (a bare passthrough saves nothing and
                //    could shadow a base column),
                //  - not depended on by a sibling extension in this same node, and
                //  - referenced exactly once in the predicate (so the expression is
                //    substituted, not duplicated).
                if (required?.Contains(extension.Alias) == false
                    && extension.Expression is not ColumnRef
                    && !SiblingReferences(ext.Extensions, extension)
                    && CountColumnRefOccurrences(predicate, extension.Alias) == 1)
                {
                    predicate = SubstituteColumn(predicate, extension.Alias, extension.Expression);
                    inlinedAny = true;
                    applied++;
                    continue;
                }

                remaining.Add(extension);
            }

            if (!inlinedAny)
            {
                return f with { Input = Rewrite(ext, PassThrough(required, f.Predicate), ref attempted, ref applied) };
            }

            var newInput = remaining.Count == 0 ? ext.Input : ext with { Extensions = remaining };
            var rewrittenInput = Rewrite(newInput, PassThrough(required, predicate), ref attempted, ref applied);
            return new FilterNode(rewrittenInput, predicate);
        }

        private static bool SiblingReferences(IReadOnlyList<ProjectionExpr> extensions, ProjectionExpr target)
        {
            foreach (var e in extensions)
            {
                if (ReferenceEquals(e, target))
                {
                    continue;
                }

                var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectColumnRefs(e.Expression, refs);
                if (refs.Contains(target.Alias))
                {
                    return true;
                }
            }

            return false;
        }
    }
}