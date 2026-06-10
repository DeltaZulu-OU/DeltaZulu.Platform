namespace DeltaZulu.Hunting.Tests.Render;

using DeltaZulu.Hunting.Render.Services;

[TestClass]
public sealed class RenderTypeClassifierTests
{
    [TestMethod]
    [DataRow("TINYINT")]
    [DataRow("SMALLINT")]
    [DataRow("INTEGER")]
    [DataRow("BIGINT")]
    [DataRow("HUGEINT")]
    [DataRow("UTINYINT")]
    [DataRow("USMALLINT")]
    [DataRow("UINTEGER")]
    [DataRow("UBIGINT")]
    [DataRow("UHUGEINT")]
    [DataRow("FLOAT")]
    [DataRow("DOUBLE")]
    [DataRow("REAL")]
    [DataRow("DECIMAL")]
    [DataRow("DECIMAL(18,2)")]
    public void IsNumeric_DuckDbNumericTypeName_ReturnsTrue(string typeName) => Assert.IsTrue(RenderTypeClassifier.IsNumeric(typeName));

    [TestMethod]
    [DataRow("DATE")]
    [DataRow("TIME")]
    [DataRow("TIMESTAMP")]
    [DataRow("TIMESTAMP_S")]
    [DataRow("TIMESTAMP_MS")]
    [DataRow("TIMESTAMP_NS")]
    [DataRow("TIMESTAMP WITH TIME ZONE")]
    [DataRow("TIMESTAMPTZ")]
    [DataRow("TIME WITH TIME ZONE")]
    [DataRow("TIMETZ")]
    [DataRow("DATETIME")]
    public void IsTemporal_DuckDbTemporalTypeName_ReturnsTrue(string typeName) => Assert.IsTrue(RenderTypeClassifier.IsTemporal(typeName));

    [TestMethod]
    [DataRow("VARCHAR")]
    [DataRow("CHAR")]
    [DataRow("TEXT")]
    [DataRow("STRING")]
    [DataRow("UUID")]
    [DataRow("BOOLEAN")]
    [DataRow("BOOL")]
    [DataRow("ENUM")]
    public void IsCategorical_CategoricalTypeName_ReturnsTrue(string typeName) => Assert.IsTrue(RenderTypeClassifier.IsCategorical(typeName));

    [TestMethod]
    public void IsNumeric_NumericClrType_ReturnsTrue()
    {
        Assert.IsTrue(RenderTypeClassifier.IsNumeric(null, typeof(int)));
        Assert.IsTrue(RenderTypeClassifier.IsNumeric(null, typeof(long)));
        Assert.IsTrue(RenderTypeClassifier.IsNumeric(null, typeof(double)));
        Assert.IsTrue(RenderTypeClassifier.IsNumeric(null, typeof(decimal)));
        Assert.IsTrue(RenderTypeClassifier.IsNumeric(null, typeof(int?)));
    }

    [TestMethod]
    public void IsTemporal_TemporalClrType_ReturnsTrue()
    {
        Assert.IsTrue(RenderTypeClassifier.IsTemporal(null, typeof(DateTime)));
        Assert.IsTrue(RenderTypeClassifier.IsTemporal(null, typeof(DateTimeOffset)));
        Assert.IsTrue(RenderTypeClassifier.IsTemporal(null, typeof(DateOnly)));
        Assert.IsTrue(RenderTypeClassifier.IsTemporal(null, typeof(TimeOnly)));
        Assert.IsTrue(RenderTypeClassifier.IsTemporal(null, typeof(TimeSpan)));
    }

    [TestMethod]
    public void IsCategorical_CategoricalClrType_ReturnsTrue()
    {
        Assert.IsTrue(RenderTypeClassifier.IsCategorical(null, typeof(string)));
        Assert.IsTrue(RenderTypeClassifier.IsCategorical(null, typeof(char)));
        Assert.IsTrue(RenderTypeClassifier.IsCategorical(null, typeof(Guid)));
        Assert.IsTrue(RenderTypeClassifier.IsCategorical(null, typeof(bool)));
        Assert.IsTrue(RenderTypeClassifier.IsCategorical(null, typeof(TestCategory)));
    }

    [TestMethod]
    public void Classify_NumericColumn_ReturnsNumericColumn()
    {
        var column = RenderTypeClassifier.Classify("LaunchCount", "BIGINT");

        Assert.AreEqual("LaunchCount", column.Name);
        Assert.AreEqual("BIGINT", column.TypeName);
        Assert.IsTrue(column.IsNumeric);
        Assert.IsFalse(column.IsTemporal);
        Assert.IsFalse(column.IsCategorical);
    }

    [TestMethod]
    public void Classify_TemporalColumn_ReturnsTemporalColumn()
    {
        var column = RenderTypeClassifier.Classify("Timestamp", "TIMESTAMP");

        Assert.AreEqual("Timestamp", column.Name);
        Assert.IsFalse(column.IsNumeric);
        Assert.IsTrue(column.IsTemporal);
        Assert.IsFalse(column.IsCategorical);
    }

    [TestMethod]
    public void Classify_CategoricalColumn_ReturnsCategoricalColumn()
    {
        var column = RenderTypeClassifier.Classify("AccountName", "VARCHAR");

        Assert.AreEqual("AccountName", column.Name);
        Assert.IsFalse(column.IsNumeric);
        Assert.IsFalse(column.IsTemporal);
        Assert.IsTrue(column.IsCategorical);
    }

    [TestMethod]
    public void Classify_BlankName_Throws() => Assert.ThrowsExactly<ArgumentException>(() => RenderTypeClassifier.Classify(""));

    private enum TestCategory
    {
        One
    }
}