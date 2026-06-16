using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Application.Analytics.Planning;
public sealed partial class RelationalPlanner
{
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

        private static RelNode RewriteFilter(FilterNode node, ref int attempted, ref int applied)
        {
            var rewrittenInput = RewriteNode(node.Input, ref attempted, ref applied);
            var rewritten = rewrittenInput == node.Input ? node : node with { Input = rewrittenInput };

            // Keep this pass intentionally narrow: only linear parent->child pushdown
            // through a direct projection wrapper.
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
    }
}