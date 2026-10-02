using System.Text.Json;
using LocalServerAllocatorModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace AllocatorTests;

public class LocalServerIdsTests
{
    const string TwoPlayersSameServer =
        """{"players":[{"id":"p1","customData":{"localServerId":"dev-a"}},{"id":"p2","customData":{"localServerId":"dev-a"}}]}""";

    static IEnumerable<TestCaseData> SameServerInEachJsonLibrary()
    {
        yield return new TestCaseData(JsonSerializer.Deserialize<Dictionary<string, object>>(TwoPlayersSameServer)!)
            .SetName("FromMatchProperties_PlayersShareALocalServerId_ReturnsIt(System.Text.Json)");
        yield return new TestCaseData(JObject.Parse(TwoPlayersSameServer).ToObject<Dictionary<string, object>>()!)
            .SetName("FromMatchProperties_PlayersShareALocalServerId_ReturnsIt(Newtonsoft)");
    }

    [TestCaseSource(nameof(SameServerInEachJsonLibrary))]
    public void FromMatchProperties_PlayersShareALocalServerId_ReturnsIt(Dictionary<string, object> properties)
    {
        Assert.That(LocalServerIds.FromMatchProperties(properties), Is.EqualTo("dev-a"));
    }

    [Test]
    public void FromMatchProperties_CustomDataIsAJsonString_ReturnsTheLocalServerId()
    {
        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(
            """{"players":[{"id":"p1","customData":"{\"localServerId\":\"dev-a\"}"}]}""")!;

        Assert.That(LocalServerIds.FromMatchProperties(properties), Is.EqualTo("dev-a"));
    }

    [TestCase("""{"players":[{"id":"p1","customData":"not json"}]}""",
        TestName = "FromMatchProperties_CustomDataIsAStringThatIsNotJson_ReturnsNull")]
    [TestCase("""{"players":[{"id":"p1","customData":{"localServerId":"dev-a"}},{"id":"p2","customData":{"localServerId":"dev-b"}}]}""",
        TestName = "FromMatchProperties_PlayersTargetTwoServers_ReturnsNull")]
    [TestCase("""{"players":[{"id":"p1","customData":{"localServerId":"dev-a"}},{"id":"p2","customData":{}}]}""",
        TestName = "FromMatchProperties_PlayerWithoutLocalServerId_ReturnsNull")]
    [TestCase("""{"players":[{"id":"p1"}]}""",
        TestName = "FromMatchProperties_PlayerWithoutCustomData_ReturnsNull")]
    [TestCase("""{"players":[{"id":"p1","customData":{"localServerId":"../etc"}}]}""",
        TestName = "FromMatchProperties_InvalidLocalServerId_ReturnsNull")]
    [TestCase("""{"teams":[]}""",
        TestName = "FromMatchProperties_NoPlayers_ReturnsNull")]
    public void FromMatchProperties_NoSharedValidLocalServerId(string json)
    {
        Assert.That(LocalServerIds.FromMatchProperties(JsonSerializer.Deserialize<Dictionary<string, object>>(json)), Is.Null);
    }

    [Test]
    public void ToControlSessionId_PrefixesTheLocalServerId()
    {
        Assert.That(LocalServerIds.ToControlSessionId("dev-a"), Is.EqualTo("local-dev-a"));
    }
}
