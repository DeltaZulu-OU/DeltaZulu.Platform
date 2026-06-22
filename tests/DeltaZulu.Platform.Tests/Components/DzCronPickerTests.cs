using Bunit;
using DeltaZulu.Platform.Web.Components;
using MudBlazor.Services;

namespace DeltaZulu.Platform.Tests.Components;

[TestClass]
public sealed class DzCronPickerTests
{
    [TestMethod]
    public async Task HourlyCron_ShowsHourlyDescription()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.Render<DzCronPicker>(p => p.Add(c => c.Value, "0 * * * *"));

        var preview = cut.Find(".dz-cron-preview");
        Assert.Contains("Every hour at minute 0", preview.TextContent);
        Assert.Contains("0 * * * *", preview.TextContent);
    }

    [TestMethod]
    public async Task DailyCron_ShowsDailyDescription()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.Render<DzCronPicker>(p => p.Add(c => c.Value, "30 3 * * *"));

        var preview = cut.Find(".dz-cron-preview");
        Assert.Contains("Daily at 03:30 UTC", preview.TextContent);
        Assert.Contains("30 3 * * *", preview.TextContent);
    }

    [TestMethod]
    public async Task WeeklyCron_ShowsWeeklyDescriptionWithDayName()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.Render<DzCronPicker>(p => p.Add(c => c.Value, "0 3 * * 1"));

        var preview = cut.Find(".dz-cron-preview");
        Assert.Contains("Weekly on Monday at 03:00 UTC", preview.TextContent);
    }

    [TestMethod]
    public async Task MonthlyCron_ShowsMonthlyDescription()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.Render<DzCronPicker>(p => p.Add(c => c.Value, "0 3 15 * *"));

        var preview = cut.Find(".dz-cron-preview");
        Assert.Contains("Monthly on day 15 at 03:00 UTC", preview.TextContent);
    }

    [TestMethod]
    public async Task NonStandardCron_TreatedAsCustomWithNoHumanDescription()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.Render<DzCronPicker>(p => p.Add(c => c.Value, "*/5 * * * *"));

        var preview = cut.Find(".dz-cron-preview");
        Assert.Contains("*/5 * * * *", preview.TextContent);
        Assert.IsFalse(preview.TextContent.Contains("Every"), "Custom cron should not produce a standard description");
        Assert.IsFalse(preview.TextContent.Contains("Daily"), "Custom cron should not produce a standard description");
    }

    [TestMethod]
    public async Task CronPreview_ShowsExpressionInCodeElement()
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.Render<DzCronPicker>(p => p.Add(c => c.Value, "0 9 * * 5"));

        var code = cut.Find(".dz-cron-preview code");
        Assert.AreEqual("0 9 * * 5", code.TextContent.Trim());
    }

    [TestMethod]
    [DataRow("0 * * * *",   "Every hour at minute 0")]
    [DataRow("15 * * * *",  "Every hour at minute 15")]
    [DataRow("0 0 * * *",   "Daily at 00:00 UTC")]
    [DataRow("0 12 * * *",  "Daily at 12:00 UTC")]
    [DataRow("0 3 * * 0",   "Weekly on Sunday at 03:00 UTC")]
    [DataRow("0 3 * * 5",   "Weekly on Friday at 03:00 UTC")]
    [DataRow("0 3 1 * *",   "Monthly on day 1 at 03:00 UTC")]
    [DataRow("0 3 28 * *",  "Monthly on day 28 at 03:00 UTC")]
    public async Task CronParsing_ProducesCorrectDescription(string cron, string expectedDescription)
    {
        await using var context = new BunitContext();
        context.Services.AddMudServices();
        context.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = context.Render<DzCronPicker>(p => p.Add(c => c.Value, cron));

        var preview = cut.Find(".dz-cron-preview");
        Assert.Contains(expectedDescription, preview.TextContent);
    }
}
