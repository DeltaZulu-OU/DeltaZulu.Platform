namespace DeltaZulu.Hunting.Core.Planning;

using DeltaZulu.Hunting.Core.QueryModel;

public sealed partial class RelationalPlanner
{
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
}