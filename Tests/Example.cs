namespace Tests;

public class Example
{
    [SetUp]
    public void Setup()
    {
    }

    [TearDown]
    public void TearDown()
    {
    }

    [Test]
    public void PassTest()
    {
        Assert.Pass("This test always passes");
    }
}
