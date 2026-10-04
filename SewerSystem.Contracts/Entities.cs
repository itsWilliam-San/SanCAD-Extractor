using System;
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
    public List<Manhole> UpstreamManholes { get; set; } = new List<Manhole>();
    public DownstreamConnection? DownstreamConnection { get; set; }

    public Manhole GetBranchRoot()
    {
        var current = this;

        while (current.UpstreamManholes.Count == 1)
        {
            current = current.UpstreamManholes[0];
        }

        return current;
    }
    
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

[AttributeUsage(AttributeTargets.Class)]
public class PluginInfoAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    public string Version { get; }
    public string Author { get; }
    
    public PluginInfoAttribute(string name, string description, string version, string author)
    {
        Name = name;
        Description = description;
        Version = version;
        Author = author;
    }
}
