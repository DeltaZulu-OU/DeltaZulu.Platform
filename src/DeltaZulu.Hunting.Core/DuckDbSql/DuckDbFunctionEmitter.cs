namespace DeltaZulu.Hunting.Core.DuckDbSql;

using QueryModel;

internal sealed class DuckDbFunctionEmitter
{
    private readonly DuckDbEmitterContext _context;
    private readonly Func<ScalarExpr, string> _emitScalar;

    internal DuckDbFunctionEmitter(
        DuckDbEmitterContext context,
        Func<ScalarExpr, string> emitScalar)
    {
        _context = context;
        _emitScalar = emitScalar;
    }

    internal string EmitFunction(FunctionCall fn)
    {
        var args = fn.Args.Select(_emitScalar).ToList();
        var name = fn.Name.ToLowerInvariant();

        return name switch
        {
            // String functions
            "tolower" => $"lower({args[0]})",
            "toupper" => $"upper({args[0]})",
            "strlen" => $"length({args[0]})",
            "strcat" => $"concat({string.Join(", ", args)})",
            "strcat_array" => $"array_to_string({args[0]}, {args[1]})",
            "strcat_delim" => $"concat_ws({string.Join(", ", args)})",
            "substring" => $"substring({args[0]}, ({args[1]}) + 1, {args[2]})",
            "replace_string" => $"replace({args[0]}, {args[1]}, {args[2]})",
            "replace_regex" => $"regexp_replace({args[0]}, {args[1]}, {args[2]}, 'g')",
            "split" => $"string_split({args[0]}, {args[1]})",
            "indexof" => $"(strpos({args[0]}, {args[1]}) - 1)",
            "reverse" => $"reverse({args[0]})",
            "trim" => $"regexp_replace(regexp_replace({args[1]}, concat('^(', {args[0]}, ')'), ''), concat('(', {args[0]}, ')$'), '')",
            "trim_start" => $"regexp_replace({args[1]}, concat('^(', {args[0]}, ')'), '')",
            "trim_end" => $"regexp_replace({args[1]}, concat('(', {args[0]}, ')$'), '')",
            "extract" => $"COALESCE(regexp_extract({args[2]}, {args[0]}, CAST({args[1]} AS INTEGER)), '')",
            "parse_path" => $"""
to_json(
    struct_pack(
        root := regexp_extract({args[0]}, '^([A-Za-z]:)', 1),
        directory := regexp_replace({args[0]}, '[^\\/]+$', ''),
        filename := regexp_extract({args[0]}, '([^\\/]+)$', 1),
        extension := regexp_extract({args[0]}, '\.([^\\/.]+)$', 1)
    )
)
""",
            // DateTime functions
            "ago" => EmitAgo(args),
            "now" => "current_timestamp",
            "bin" when args.Count >= 2 => EmitBin(fn.Args, args),
            "bin_at" => $"time_bucket({EmitTimespanArg(fn.Args, 1)}, {args[0]}, {args[2]})",
            "startofday" => $"date_trunc('day', {args[0]})",
            "startofmonth" => $"date_trunc('month', {args[0]})",
            "startofweek" => $"date_trunc('week', {args[0]})",
            "startofyear" => $"date_trunc('year', {args[0]})",
            "endofday" => $"(date_trunc('day', {args[0]}) + INTERVAL '1 day' - INTERVAL '1 microsecond')",
            "endofmonth" => $"(last_day({args[0]})::TIMESTAMP + INTERVAL '23 hours 59 minutes 59 seconds 999999 microseconds')",
            "endofweek" => $"(date_trunc('week', {args[0]}) + INTERVAL '7 days' - INTERVAL '1 microsecond')",
            "endofyear" => $"(date_trunc('year', {args[0]}) + INTERVAL '1 year' - INTERVAL '1 microsecond')",
            // datetime_diff(part, dt1, dt2) → date_diff(part, dt2, dt1)
            // Spec §9.9: KQL returns dt1 - dt2 periods; DuckDB date_diff(part, start, end)
            // = end - start. So to preserve sign: date_diff(part, dt2, dt1)
            "datetime_diff" => $"date_diff({args[0]}, {args[2]}, {args[1]})",
            "datetime_add" => EmitDatetimeAdd(fn.Args, args),
            "datetime_part" => $"date_part({args[0]}, {args[1]})",
            "dayofweek" => $"date_part('dow', {args[0]})",
            "dayofmonth" => $"date_part('day', {args[0]})",
            "dayofyear" => $"date_part('doy', {args[0]})",
            "monthofyear" or "getmonth" => $"date_part('month', {args[0]})",
            "getyear" => $"date_part('year', {args[0]})",
            "hourofday" => $"date_part('hour', {args[0]})",
            "unixtime_seconds_todatetime" => $"to_timestamp({args[0]})",
            "unixtime_milliseconds_todatetime" => $"epoch_ms(CAST({args[0]} AS BIGINT))",
            "unixtime_microseconds_todatetime" => $"make_timestamp({args[0]})",
            "unixtime_nanoseconds_todatetime" => $"make_timestamp_ns({args[0]})",
            "make_datetime" => EmitMakeDatetime(args),
            "todatetime" => $"CAST({args[0]} AS TIMESTAMP)",

            // Type conversion
            "tostring" => $"CAST({args[0]} AS VARCHAR)",
            "tolong" => $"CAST({args[0]} AS BIGINT)",
            "toint" => $"CAST({args[0]} AS INTEGER)",
            "todouble" or "toreal" => $"CAST({args[0]} AS DOUBLE)",
            "tobool" => $"CAST({args[0]} AS BOOLEAN)",
            "todecimal" => $"CAST({args[0]} AS DECIMAL)",
            "decimal" => $"CAST({args[0]} AS DECIMAL)",
            "toguid" => $"CAST({args[0]} AS VARCHAR)",
            "guid" => $"TRY_CAST({args[0]} AS UUID)",
            "countof" => $"((length({args[0]}) - length(replace({args[0]}, {args[1]}, ''))) / nullif(length({args[1]}), 0))",
            "parse_ipv4" => $"CASE WHEN regexp_full_match({args[0]}, '^((25[0-5]|2[0-4][0-9]|1?[0-9]?[0-9])\\.){{3}}(25[0-5]|2[0-4][0-9]|1?[0-9]?[0-9])$') THEN (CAST(split_part({args[0]}, '.', 1) AS BIGINT) * 16777216 + CAST(split_part({args[0]}, '.', 2) AS BIGINT) * 65536 + CAST(split_part({args[0]}, '.', 3) AS BIGINT) * 256 + CAST(split_part({args[0]}, '.', 4) AS BIGINT)) ELSE NULL END",
            "base64_encode_tostring" => $"to_base64(CAST({args[0]} AS BLOB))",
            "base64_decode_tostring" => $"CAST(from_base64({args[0]}) AS VARCHAR)",
            "url_encode" => $"url_encode({args[0]})",
            "url_decode" => $"url_decode({args[0]})",
            "hash_sha256" => EmitStringHash("hash_sha256", "sha256", fn.Args, args),
            "hash_md5" => EmitStringHash("hash_md5", "md5", fn.Args, args),
            "translate" => EmitTranslate(args),

            // Conditional
            "iff" or "iif" => $"CASE WHEN {args[0]} THEN {args[1]} ELSE {args[2]} END",
            "coalesce" => $"COALESCE({string.Join(", ", args)})",
            "max_of" => $"greatest({string.Join(", ", args)})",
            "min_of" => $"least({string.Join(", ", args)})",

            // Null tests
            "isnull" => $"({args[0]} IS NULL)",
            "isnotnull" => $"({args[0]} IS NOT NULL)",
            "isempty" => $"({args[0]} IS NULL OR {args[0]} = '')",
            "isnotempty" => $"({args[0]} IS NOT NULL AND {args[0]} != '')",
            "isnan" => $"isnan({args[0]})",
            "isinf" => $"isinf({args[0]})",

            // JSON
            "parse_json" => $"CAST({args[0]} AS JSON)",
            "bag_keys" => $"json_keys({args[0]})",
            "bag_has_key" => $"(json_extract({args[0]}, concat('$.', {args[1]})) IS NOT NULL)",
            "bag_merge" => $"json_merge_patch({args[0]}, {args[1]})",
            "array_length" => $"CASE WHEN json_valid(CAST({args[0]} AS VARCHAR)) THEN json_array_length({args[0]}) ELSE length({args[0]}) END",
            "array_concat" => $"list_concat({args[0]}, {args[1]})",
            "array_slice" => $"list_slice({args[0]}, ({args[1]}) + 1, ({args[2]}) - ({args[1]}))",

            // Math
            "abs" => $"abs({args[0]})",
            "ceiling" => $"ceil({args[0]})",
            "floor" => $"floor({args[0]})",
            "round" => $"round({args[0]}, {args[1]})",
            "log" => $"ln({args[0]})",
            "log2" => $"log2({args[0]})",
            "log10" => $"log10({args[0]})",
            "pow" => $"power({args[0]}, {args[1]})",
            "sqrt" => $"sqrt({args[0]})",
            "exp" => $"exp({args[0]})",
            "exp2" => $"power(2, {args[0]})",
            "exp10" => $"power(10, {args[0]})",
            "sign" => $"sign({args[0]})",
            "pi" => "pi()",
            "rand" => args.Count switch { 0 => "random()", 1 => $"setseed({args[0]}) + (random()*0)", _ => throw new NotSupportedException("rand() expects 0 or 1 argument.") },
            "cos" => $"cos({args[0]})",
            "sin" => $"sin({args[0]})",
            "tan" => $"tan({args[0]})",
            "acos" => $"acos({args[0]})",
            "asin" => $"asin({args[0]})",
            "atan" => $"atan({args[0]})",
            "atan2" => $"atan2({args[0]}, {args[1]})",
            "format_bytes" => $"CASE WHEN {args[0]} IS NULL THEN NULL WHEN abs({args[0]}) < 1024 THEN concat(CAST(round({args[0]}, 0) AS BIGINT), ' B') WHEN abs({args[0]}) < 1048576 THEN concat(CAST(round({args[0]}/1024.0, 2) AS DOUBLE), ' KB') WHEN abs({args[0]}) < 1073741824 THEN concat(CAST(round({args[0]}/1048576.0, 2) AS DOUBLE), ' MB') ELSE concat(CAST(round({args[0]}/1073741824.0, 2) AS DOUBLE), ' GB') END",

            // Aggregation (when used inside AggregateNode projections)
            "count" => args.Count == 0 ? "count(*)" : $"count({args[0]})",
            "countif" => $"count(*) FILTER (WHERE {args[0]})",
            "sum" => $"sum({args[0]})",
            "sumif" => $"sum({args[0]}) FILTER (WHERE {args[1]})",
            "avg" => $"avg({args[0]})",
            "avgif" => $"avg({args[0]}) FILTER (WHERE {args[1]})",
            "min" => $"min({args[0]})",
            "max" => $"max({args[0]})",
            "dcount" => $"count(DISTINCT {args[0]})",
            "dcountif" => $"count(DISTINCT {args[0]}) FILTER (WHERE {args[1]})",
            "arg_min" => $"arg_min({string.Join(", ", args)})",
            "arg_max" => $"arg_max({string.Join(", ", args)})",
            "make_set" when args.Count == 1 => $"list(DISTINCT {args[0]})",
            "make_set" => $"list_slice(list(DISTINCT {args[0]}), 1, {args[1]})",
            "make_list" when args.Count == 1 => $"list({args[0]})",
            "make_list" => $"list_slice(list({args[0]}), 1, {args[1]})",
            "any" => $"any_value({args[0]})",
            "stdev" => $"stddev_samp({args[0]})",
            "stdevif" => $"stddev_samp({args[0]}) FILTER (WHERE {args[1]})",
            "variance" => $"var_samp({args[0]})",
            "varianceif" => $"var_samp({args[0]}) FILTER (WHERE {args[1]})",
            "percentile" when _context.InAggregateProjection => $"quantile_cont({args[0]}, ({args[1]}) / 100.0)",
            "percentile" => throw new NotSupportedException(
                "KQL function 'percentile' is only supported inside summarize projections."),
            "binary_all_and" => $"bit_and({args[0]})",
            "binary_all_or" => $"bit_or({args[0]})",
            "binary_all_xor" => $"bit_xor({args[0]})",

            // Unknown function: reject rather than emit raw SQL.
            // Emitting an unknown name violates the project safety rule.
            _ => throw new NotSupportedException(
                $"KQL function '{name}' is not in the supported function allowlist. " +
                "Add an explicit mapping or reject it.")
        };
    }

    /// DuckDB official docs do NOT list ago() as a function.
    /// The documented pattern is current_timestamp - INTERVAL.
    /// Source: https://duckdb.org/docs/current/sql/functions/timestamp
    /// </summary>
    private static string EmitAgo(List<string> args) =>
        // args[0] is already the emitted INTERVAL literal (e.g., INTERVAL '7 days')
        $"(current_timestamp - {args[0]})";

    /// <summary>
    /// Emit datetime_add(part, amount, datetime).
    /// KQL: datetime_add('hour', 3, ts) → ts + INTERVAL '3 hours'
    /// We extract the part name from the literal argument to build a valid INTERVAL.
    /// </summary>
    private static string EmitDatetimeAdd(IReadOnlyList<ScalarExpr> rawArgs, List<string> args)
    {
        // Try to extract the part name from the first argument (should be a string literal)
        if (rawArgs.Count >= 1 && rawArgs[0] is LiteralScalar { Value: string partName })
        {
            var unit = partName.ToLowerInvariant() switch
            {
                "year" => "years",
                "quarter" => "months", // 3 months — multiply below
                "month" => "months",
                "week" => "weeks",
                "day" => "days",
                "hour" => "hours",
                "minute" => "minutes",
                "second" => "seconds",
                "millisecond" => "milliseconds",
                "microsecond" => "microseconds",
                _ => partName + "s"
            };

            var multiplier = partName.Equals("quarter", StringComparison.OrdinalIgnoreCase) ? $"(({args[1]}) * 3)" : args[1];
            return $"({args[2]} + ({multiplier}) * INTERVAL '1 {unit}')";
        }

        // Non-literal part: fall back to CAST-based approach
        return $"({args[2]} + CAST(CAST({args[1]} AS VARCHAR) || ' ' || REPLACE({args[0]}, '''', '') AS INTERVAL))";
    }

    /// <summary>
    /// Emit string-only KQL cryptographic hash mappings defensively for callers
    /// that construct RelNode trees directly and therefore bypass translation.
    /// </summary>
    private static string EmitStringHash(
        string kustoName,
        string duckDbName,
        IReadOnlyList<ScalarExpr> rawArgs,
        IReadOnlyList<string> args)
    {
        RequireFunctionArity(kustoName, args, 1);
        if (rawArgs[0] is LiteralScalar { Kind: not LiteralKind.String and not LiteralKind.Null })
        {
            throw new NotSupportedException(
                $"{kustoName}() currently supports only string input because DuckDB and KQL serialize non-string scalars differently.");
        }

        return $"{duckDbName}({args[0]})";
    }

    /// <summary>
    /// KQL translate(searchList, replacementList, source) repeats the final
    /// replacement character when replacementList is shorter than searchList.
    /// DuckDB translate(source, from, to) deletes characters without a matching
    /// target, so pad or truncate the replacement list before emission.
    /// </summary>
    private static string EmitTranslate(IReadOnlyList<string> args)
    {
        RequireFunctionArity("translate", args, 3);
        return $"translate({args[2]}, {args[0]}, CASE WHEN {args[1]} = '' THEN '' ELSE rpad({args[1]}, CAST(length({args[0]}) AS INTEGER), right({args[1]}, 1)) END)";
    }

    private static void RequireFunctionArity(string functionName, IReadOnlyList<string> args, int expected)
    {
        if (args.Count != expected)
        {
            throw new NotSupportedException($"{functionName}() expects exactly {expected} argument(s).");
        }
    }

    /// <summary>
    /// Helper for bin/bin_at: if the argument is a timespan literal,
    /// emit it as INTERVAL directly rather than relying on the generic
    /// EmitScalar which may have already formatted it.
    /// </summary>
    private string EmitTimespanArg(IReadOnlyList<ScalarExpr> args, int index) => _emitScalar(args[index]);

    /// <summary>
    /// KQL bin(value, roundTo) rounds value down to a multiple of roundTo.
    /// A timespan roundTo buckets a datetime: time_bucket needs an explicit
    /// origin at the Unix epoch because DuckDB's default origin differs from
    /// KQL's anchor for multi-day/-week widths. A numeric roundTo bins a number,
    /// which time_bucket cannot do — emit floor arithmetic instead.
    /// </summary>
    private string EmitBin(IReadOnlyList<ScalarExpr> rawArgs, IReadOnlyList<string> args)
    {
        if (rawArgs[1] is LiteralScalar { Kind: LiteralKind.Timespan })
        {
            return $"time_bucket({args[1]}, {args[0]}, TIMESTAMP '1970-01-01')";
        }

        return $"(floor(({args[0]}) / ({args[1]})) * ({args[1]}))";
    }

    /// <summary>
    /// KQL make_datetime accepts (y,m,d), (y,m,d,h,min) or (y,m,d,h,min,s).
    /// DuckDB make_timestamp requires exactly six arguments (seconds as DOUBLE),
    /// so pad any missing trailing components with zeros.
    /// </summary>
    private static string EmitMakeDatetime(IReadOnlyList<string> args)
    {
        var parts = new string[6];
        for (var i = 0; i < 6; i++)
        {
            parts[i] = i < args.Count ? args[i] : (i == 5 ? "0.0" : "0");
        }

        return $"make_timestamp({string.Join(", ", parts)})";
    }
}