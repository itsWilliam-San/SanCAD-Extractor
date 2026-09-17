using System.Collections.Generic;

namespace SewerSystem.Contracts;

public abstract class Station
{
    public string Id { get; }
    public decimal TerrainLevel { get; }
    public decimal InvertLevel { get; }
    public decimal Depth { get; }

    protected Station(string id, decimal terrainLevel, decimal invertLevel, decimal depth)
    {
        Id = id;
        TerrainLevel = terrainLevel;
        InvertLevel = invertLevel;
        Depth = depth;
    }
}

public class Intermediate : Station
{
    public Intermediate(string id, decimal terrainLevel, decimal invertLevel, decimal depth) 
        : base(id,  terrainLevel, invertLevel, depth)
    {
    }
}

public class Manhole : Station
{
    public List<Manhole> UpstreamManholes { get; }
    public DownstreamConnection? DownstreamConnection { get; }
    
    public Manhole(string id, decimal terrainLevel, decimal invertLevel, decimal depth) 
        : base(id,  terrainLevel, invertLevel, depth)
    {
    }
}

public interface IConnection
{
    decimal InvertLevel { get; }
}

public readonly record struct StepDrop(decimal InvertLevel) : IConnection;
public readonly record struct DropPipe(decimal InvertLevel) : IConnection;

public readonly record struct DownstreamConnection(
    Manhole DownstreamManhole,
    IConnection? Connection,
    IReadOnlyList<Intermediate> Intermediates,
    Segment Segment
);

public readonly record struct Pipe(
    string Material,
    decimal Length,
    ushort DiameterInMillimeters
);

public record Segment(
    string Id,
    decimal IncrementalLength,
    decimal Slope,
    Pipe Pipe
);
