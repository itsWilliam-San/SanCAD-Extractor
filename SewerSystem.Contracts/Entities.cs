using System.Collections.Generic;

namespace SewerSystem.Contracts;

public interface IStation
{
    string Id { get; }
    decimal TerrainLevel { get; }
    decimal InvertLevel { get; }
    decimal Depth { get; }
}

public record struct DownstreamConnection(
    Manhole DownstreamManhole,
    IReadOnlyList<Intermediate> Intermediates
);

public record Manhole(
    string Id,
    decimal TerrainLevel,
    decimal InvertLevel,
    decimal Depth,
    IReadOnlyList<Manhole> UpstreamManholes,
    DownstreamConnection? DownstreamConnection
) : IStation;

public record Intermediate(string Id, decimal TerrainLevel, decimal InvertLevel, decimal Depth) : IStation;