using Microsoft.Extensions.DependencyInjection;
using DeltaZulu.Workbench.Application.Abstractions;
using DeltaZulu.Workbench.Domain.Enums;
using DeltaZulu.Workbench.HuntingAdapter;

namespace DeltaZulu.Workbench.Tests.HuntingCoreAdapter;

[TestClass]
public sealed class HuntingCoreQuerySyntaxValidatorTests
{
    [TestMethod]
    public void Validate_PassingParserResult_ReturnsWorkbenchPass()
    {
        var parser = new StubParser(_ => HuntingCoreQueryParseResult.Pass());
        var validator = new HuntingCoreQuerySyntaxValidator(parser);

        var result = validator.Validate(new QuerySyntaxValidationRequest(
            "detections/example/rule.kql",
            DraftContentType.HuntingQuery,
            "SecurityEvent | take 10",
            "example"));

        Assert.IsTrue(result.IsValid);
        Assert.IsEmpty(result.Diagnostics);
        Assert.AreEqual("detections/example/rule.kql", parser.LastRequest?.LogicalPath);
        Assert.AreEqual("SecurityEvent | take 10", parser.LastRequest?.Content);
        Assert.AreEqual("example", parser.LastRequest?.DetectionSlug);
    }

    [TestMethod]
    public void Validate_FailingParserResult_MapsDiagnosticsWithoutExecutingRuntimeQueries()
    {
        var parser = new StubParser(_ => HuntingCoreQueryParseResult.Fail(
            new HuntingCoreQueryDiagnostic("Expected pipe expression.", 2, 5)));
        var validator = new HuntingCoreQuerySyntaxValidator(parser);

        var result = validator.Validate(new QuerySyntaxValidationRequest(
            "detections/example/rule.kql",
            DraftContentType.HuntingQuery,
            "SecurityEvent\nwhere",
            "example"));

        Assert.IsFalse(result.IsValid);
        var diagnostic = result.Diagnostics.Single();
        Assert.AreEqual("Expected pipe expression.", diagnostic.Message);
        Assert.AreEqual(2, diagnostic.Line);
        Assert.AreEqual(5, diagnostic.Column);
        Assert.AreEqual(1, parser.ParseCallCount);
    }

    [TestMethod]
    public void AddHuntingCoreQueryValidation_RegistersWorkbenchValidatorContract()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHuntingCoreQueryParser>(new StubParser(_ => HuntingCoreQueryParseResult.Pass()));

        services.AddHuntingCoreQueryValidation();

        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IQuerySyntaxValidator>();
        Assert.IsInstanceOfType(validator, typeof(HuntingCoreQuerySyntaxValidator));
    }

    private sealed class StubParser(Func<HuntingCoreQueryParseRequest, HuntingCoreQueryParseResult> parse) : IHuntingCoreQueryParser
    {
        private readonly Func<HuntingCoreQueryParseRequest, HuntingCoreQueryParseResult> _parse = parse;

        public HuntingCoreQueryParseRequest? LastRequest { get; private set; }
        public int ParseCallCount { get; private set; }

        public HuntingCoreQueryParseResult Parse(HuntingCoreQueryParseRequest request)
        {
            LastRequest = request;
            ParseCallCount++;
            return _parse(request);
        }
    }
}
