using DeltaZulu.Platform.Web.Analytics.Services;

namespace DeltaZulu.Platform.Tests.Analytics.Web;

[TestClass]
public sealed class EditorBusTests
{
    [TestMethod]
    public void RequestInsert_WhenSubscriberExists_DeliversImmediately()
    {
        var bus = new EditorBus();
        string? delivered = null;

        bus.InsertRequested += value => delivered = value;
        bus.RequestInsert("ProcessEvent | take 10");

        Assert.AreEqual("ProcessEvent | take 10", delivered);
    }

    [TestMethod]
    public void RequestInsert_BeforeSubscriberExists_DeliversPendingInsertOnSubscribe()
    {
        var bus = new EditorBus();

        bus.RequestInsert("ProcessEvent | take 10");

        string? delivered = null;
        bus.InsertRequested += value => delivered = value;

        Assert.AreEqual("ProcessEvent | take 10", delivered);
    }

    [TestMethod]
    public void RequestInsert_BeforeSubscriberExists_DeliversOnlyOnce()
    {
        var bus = new EditorBus();

        bus.RequestInsert("ProcessEvent | take 10");

        var firstCount = 0;
        var secondCount = 0;

        bus.InsertRequested += _ => firstCount++;
        bus.InsertRequested += _ => secondCount++;

        Assert.AreEqual(1, firstCount);
        Assert.AreEqual(0, secondCount);
    }
}