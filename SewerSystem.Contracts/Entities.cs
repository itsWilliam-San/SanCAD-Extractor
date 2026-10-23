using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace SewerSystem.Contracts;

public class TextEntity
{
    public string Content { get; set; } = string.Empty;
    public Position Coordinates { get; set; } = new Position();

    public class Position
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}

public class PluginManifest
{
    public string name { get; set; }
    public string description { get; set; }
    public string version { get; set; }
    public string? repository { get; set; }
    public string? downloadUrl { get; set; }
    public string? icon { get; set; }
    public string id { get; set; }
    public List<ExportAction> exports { get; set; }
}

public class ExportAction
{
    public string id { get; set; }
    public string label { get; set; }
    public string description { get; set; }
    public string entrypoint { get; set; }
    public List<Parameter>? parameters { get; set; }
}

public class Parameter
{
    public string name { get; set; }
    public string paramType { get; set; }
    public string label { get; set; }
    [JsonPropertyName("default")]
    public JsonElement defaultValue {get;set;}
    public List<JsonElement>? options { get; set; }
}

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
    public (string, string) DownstreamConnection { get; set; }
    public Dictionary<String, object> psets { get; set; }

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
    ushort Diameter,
    decimal Slope
);

public record Segment(
    string Id,
    double IntermediateDistance,
    List<string> Intermediates,
    IConnection? Connection,
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
