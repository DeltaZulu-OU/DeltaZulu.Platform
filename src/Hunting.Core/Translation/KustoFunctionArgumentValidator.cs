namespace Hunting.Core.Translation;

using QueryModel;

/// <summary>Applies function-specific arity and literal argument rules.</summary>
internal static class KustoFunctionArgumentValidator
{
    public static void Validate(string name, IReadOnlyList<ScalarExpr> args)
    {
        static void RequireCount(string functionName, IReadOnlyList<ScalarExpr> functionArgs, int expected)
        {
            if (functionArgs.Count != expected)
            {
                throw new NotSupportedException($"{functionName}() expects exactly {expected} argument(s).");
            }
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
