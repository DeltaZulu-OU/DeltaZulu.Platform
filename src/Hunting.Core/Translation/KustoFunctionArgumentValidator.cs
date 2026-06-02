namespace Hunting.Core.Translation;

using Kusto.Language.Symbols;
using Kusto.Language.Syntax;
using QueryModel;

/// <summary>Applies function-specific arity and literal argument rules.</summary>
internal static class KustoFunctionArgumentValidator
{
    public static void Validate(
        string name,
        IReadOnlyList<ScalarExpr> args,
        IReadOnlyList<Expression> syntaxArgs)
    {
        static void RequireCount(string functionName, IReadOnlyList<ScalarExpr> functionArgs, int expected)
        {
            if (functionArgs.Count != expected)
            {
                throw new NotSupportedException($"{functionName}() expects exactly {expected} argument(s).");
            }
        }

        if (name.Equals("hash_sha256", StringComparison.OrdinalIgnoreCase)
            || name.Equals("hash_md5", StringComparison.OrdinalIgnoreCase))
        {
            RequireStringHashInput(name, syntaxArgs);
        }

        switch (name.ToLowerInvariant())
        {
            case "strcat_array": RequireCount("strcat_array", args, 2); return;
            case "bag_keys": RequireCount("bag_keys", args, 1); return;
            case "bag_has_key": RequireCount("bag_has_key", args, 2); return;
            case "bag_merge": RequireCount("bag_merge", args, 2); return;
            case "array_length": RequireCount("array_length", args, 1); return;
            case "exp2": RequireCount("exp2", args, 1); return;
            case "exp10": RequireCount("exp10", args, 1); return;
            case "hash_sha256": RequireCount("hash_sha256", args, 1); return;
            case "hash_md5": RequireCount("hash_md5", args, 1); return;
            case "translate": RequireCount("translate", args, 3); return;
        }

        static void RequireStringHashInput(string functionName, IReadOnlyList<Expression> expressions)
        {
            if (expressions.Count == 1 && !ReferenceEquals(expressions[0].ResultType, ScalarTypes.String))
            {
                throw new NotSupportedException(
                    $"{functionName}() currently supports only string input because DuckDB and KQL serialize non-string scalars differently.");
            }
        }

        if (!name.Equals("extract", StringComparison.OrdinalIgnoreCase)) { return; }
        if (args.Count is < 3 or > 4)
        {
            throw new NotSupportedException("extract() expects 3 or 4 arguments: regex, captureGroup, source [, typeof(type)].");
        }
        if (args[1] is not LiteralScalar { Kind: LiteralKind.Int or LiteralKind.Long })
        {
            throw new NotSupportedException("extract() captureGroup argument must be an integer literal.");
        }
    }
}
