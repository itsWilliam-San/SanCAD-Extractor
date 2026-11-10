using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using a_DownstreamPVID = System.String;
using a_SegmentID = System.String;
using a_UpstreamID = System.String;
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
    public string name { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string version { get; set; } = string.Empty;
    public string? repository { get; set; }
    public string? downloadUrl { get; set; }
    public string? icon { get; set; }
    public string id { get; set; } = string.Empty;
    public string category { get; set; } = "extraction";
    public List<ExportAction> exports { get; set; } = new List<ExportAction>();
}

public class ExportAction
{
    public string id { get; set; } = string.Empty;
    public string label { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string entrypoint { get; set; } = string.Empty;
    public List<Parameter>? parameters { get; set; }
}

public class Parameter
{
    public string name { get; set; } = string.Empty;
    public string paramType { get; set; } = string.Empty;
    public string label { get; set; } = string.Empty;
    [JsonPropertyName("default")]
    public JsonElement defaultValue { get; set; }
    public bool required { get; set; }
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
    public Dictionary<String, object> psets { get; set; } = new Dictionary<string, object>();

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

#region Flux System DTO

public class DownstreamConnectionTupleConverter : JsonConverter<(string DownstreamPVID, string SegmentID)?>
{
    public override (string DownstreamPVID, string SegmentID)? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected StartArray for DownstreamConnection");
        }

        reader.Read();
        string pvId = reader.GetString() ?? string.Empty;
        reader.Read();
        string segId = reader.GetString() ?? string.Empty;
        reader.Read(); // EndArray

        return (pvId, segId);
    }

    public override void Write(Utf8JsonWriter writer, (string DownstreamPVID, string SegmentID)? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        writer.WriteStringValue(value.Value.DownstreamPVID);
        writer.WriteStringValue(value.Value.SegmentID);
        writer.WriteEndArray();
    }
}

public class SewerSystemDTO
{
    public List<IntermediateDTO> Intermediates { get; set; } = new List<IntermediateDTO>();
    public List<SegmentDTO> Segments { get; set; } = new List<SegmentDTO>();
    public List<StationDTO> Primary { get; set; } = new List<StationDTO>();
}

public class IntermediateDTO
{
    public string ID { get; set; } = string.Empty;
    public decimal TerrainLevel { get; set; }
    public decimal InvertLevel { get; set; }
    public decimal Depth { get; set; }
}

public class ConnectionDTO
{
    public string kind { get; set; } = string.Empty;
    public decimal InvertLevel { get; set; }
}

public class PipeDTO
{
    public string Material { get; set; } = string.Empty;
    public decimal Length { get; set; }
    public decimal Slope { get; set; }
    public decimal Diameter { get; set; }
}

public class SegmentDTO
{
    public string ID { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ConnectionDTO? Connection { get; set; }
    public PipeDTO Pipe { get; set; } = new PipeDTO();
    public List<string> Intermediates { get; set; } = new List<string>();
    public double IntermediateDistance { get; set; }
}

public class StationDTO
{
    public string ID { get; set; } = string.Empty;
    public decimal TerrainLevel { get; set; }
    public decimal InvertLevel { get; set; }
    public decimal Depth { get; set; }
    public List<a_UpstreamID> UpstreamManholes { get; set; } = new List<a_UpstreamID>();

    [JsonConverter(typeof(DownstreamConnectionTupleConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public (string DownstreamPVID, string SegmentID)? DownstreamConnection { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? psets { get; set; }
}

public class RpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    [JsonPropertyName("parameters")]
    public JsonElement? Parameters { get; set; }

    [JsonIgnore]
    public string ActionOrMethod => !string.IsNullOrEmpty(Action) ? Action! : (Method ?? string.Empty);
}

public class RpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RpcError? Error { get; set; }
}

public class RpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

#endregion