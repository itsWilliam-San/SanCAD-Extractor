using Newtonsoft.Json;
using SewerSystem.Contracts;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Extractor.Tests;

public class UnitTest1
{
    [Fact]
    public void MissingDataTest()
    {
        string rawMissingData = File.ReadAllText("fixtures/missingData.json");
        
        var missingData = JsonSerializer.Deserialize<TextEntity[]>(rawMissingData);

        var extractor = new Extractor.Main();
        var manhole = extractor.Entry(missingData);
    }
}