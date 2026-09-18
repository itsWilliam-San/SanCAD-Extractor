using SewerSystem.Contracts;

namespace Extractor.Tests;

public class UnitTest1
{
    private static IEnumerable<TextEntity[]> ProfileData()
    {
        yield return new TextEntity[]
        {
            new List<String> {"Linha1", "Linha2"},
        }
    }
    
    
    [Theory]
    [MemberData(nameof(ProfileData))]
    
    public void Test1(TextEntity[] rawData)
    {
    }
}