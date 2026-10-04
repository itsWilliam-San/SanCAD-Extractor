namespace Sewarage.Models;

public class Plugin
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Version { get; init; }
    public string? Author { get; init; }
    public Status CurrentStatus {get; set; }
}

public enum Status
{
    Running,
    Ready,
}