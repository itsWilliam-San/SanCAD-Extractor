using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SewerSystem.Contracts;
using Xunit;

namespace Extractor.Tests;

public class UnitTest1
{
    [Fact]
    public void MissingDataTest()
    {
        string rawMissingData = File.ReadAllText("fixtures/missingData.json");
        var missingData = JsonSerializer.Deserialize<TextEntity[]>(rawMissingData);
        Assert.NotNull(missingData);

        var extractor = new Extractor.SancadExtractor();
        Assert.Throws<InvalidOperationException>(() => extractor.Entry(missingData!));
    }

    [Fact]
    public void ValidDataExtractionTest()
    {
        string rawValidData = File.ReadAllText("fixtures/validData.json");
        var validData = JsonSerializer.Deserialize<TextEntity[]>(rawValidData);
        Assert.NotNull(validData);

        var extractor = new Extractor.SancadExtractor();
        var systemDto = extractor.Entry(validData!);

        Assert.NotNull(systemDto);
        Assert.Equal(2, systemDto.Primary.Count);
        Assert.Single(systemDto.Segments);
        Assert.Single(systemDto.Intermediates);

        // Check start manhole (SB1-1)
        var firstManhole = systemDto.Primary.First(x => x.ID == "SB1-1");
        Assert.Equal(100.00m, firstManhole.TerrainLevel);
        Assert.Equal(50.00m, firstManhole.InvertLevel);
        Assert.Equal(50.00m, firstManhole.Depth);
        Assert.NotNull(firstManhole.DownstreamConnection);
        Assert.Equal("SB1-2", firstManhole.DownstreamConnection!.Value.DownstreamPVID);
        Assert.Equal("1-1", firstManhole.DownstreamConnection!.Value.SegmentID);

        // Check end manhole (SB1-2)
        var secondManhole = systemDto.Primary.First(x => x.ID == "SB1-2");
        Assert.Equal(98.00m, secondManhole.TerrainLevel);
        Assert.Equal(48.00m, secondManhole.InvertLevel);
        Assert.Equal(50.00m, secondManhole.Depth);
        Assert.Null(secondManhole.DownstreamConnection);
        Assert.Contains("SB1-1", secondManhole.UpstreamManholes);

        // Check intermediate (1-1E1)
        var intermediate = systemDto.Intermediates.First();
        Assert.Equal("1-1E1", intermediate.ID);
        Assert.Equal(99.00m, intermediate.TerrainLevel);
        Assert.Equal(49.50m, intermediate.InvertLevel);
        Assert.Equal(49.50m, intermediate.Depth);

        // Check segment (1-1)
        var segment = systemDto.Segments.First();
        Assert.Equal("1-1", segment.ID);
        Assert.Equal("PVC", segment.Pipe.Material);
        Assert.Equal(40m, segment.Pipe.Length);
        Assert.Equal(0.005m, segment.Pipe.Slope);
        Assert.Equal(0.150m, segment.Pipe.Diameter); // converted from mm to meters
        Assert.Equal(20.0, segment.IntermediateDistance);
        Assert.Contains("1-1E1", segment.Intermediates);

        // Verify JSON serialization follows network-v1.json specification
        var json = JsonSerializer.Serialize(systemDto, new JsonSerializerOptions { WriteIndented = true });
        Assert.Contains("\"Intermediates\":", json);
        Assert.Contains("\"Segments\":", json);
        Assert.Contains("\"Primary\":", json);
        Assert.Contains("\"DownstreamConnection\": [", json);

        // Verify roundtrip deserialization
        var roundTrip = JsonSerializer.Deserialize<SewerSystemDTO>(json);
        Assert.NotNull(roundTrip);
        Assert.Equal(systemDto.Primary.Count, roundTrip!.Primary.Count);
        Assert.Equal(systemDto.Segments.Count, roundTrip.Segments.Count);
        Assert.Equal(systemDto.Intermediates.Count, roundTrip.Intermediates.Count);
    }

    [Fact]
    public void JsonRpcResponseSerializationTest()
    {
        var sampleDto = new SewerSystemDTO
        {
            Primary =
            {
                new StationDTO
                {
                    ID = "PV-01",
                    TerrainLevel = 100m,
                    InvertLevel = 50m,
                    Depth = 50m,
                    UpstreamManholes = { "PV-02" },
                    DownstreamConnection = ("PV-03", "SEG-ABC")
                },
                new StationDTO
                {
                    ID = "PV-03",
                    TerrainLevel = 98m,
                    InvertLevel = 48m,
                    Depth = 50m,
                    UpstreamManholes = { "PV-01" },
                    DownstreamConnection = null
                }
            },
            Segments =
            {
                new SegmentDTO
                {
                    ID = "SEG-ABC",
                    Connection = new ConnectionDTO { kind = "DropPipe", InvertLevel = 10m },
                    Pipe = new PipeDTO { Material = "PVC", Length = 10m, Slope = 0.56m, Diameter = 0.05m },
                    Intermediates = { "L-01" },
                    IntermediateDistance = 20.0
                }
            },
            Intermediates =
            {
                new IntermediateDTO
                {
                    ID = "L-01",
                    TerrainLevel = 100m,
                    InvertLevel = 50m,
                    Depth = 50m
                }
            }
        };

        var response = new RpcResponse
        {
            JsonRpc = "2.0",
            Id = 1,
            Result = sampleDto
        };

        var json = JsonSerializer.Serialize(response);
        Assert.Contains("\"jsonrpc\":\"2.0\"", json);
        Assert.Contains("\"id\":1", json);
        Assert.Contains("\"result\":{", json);
        Assert.Contains("[\"PV-03\",\"SEG-ABC\"]", json);

        // Last manhole downstream connection is null and omitted
        using var doc = JsonDocument.Parse(json);
        var primaryArray = doc.RootElement.GetProperty("result").GetProperty("Primary");
        Assert.Equal(2, primaryArray.GetArrayLength());
        var firstPv = primaryArray[0];
        Assert.True(firstPv.TryGetProperty("DownstreamConnection", out var dsProp));
        Assert.Equal(JsonValueKind.Array, dsProp.ValueKind);

        var secondPv = primaryArray[1];
        Assert.False(secondPv.TryGetProperty("DownstreamConnection", out _));
    }

    [Fact]
    public void JsonRpcRequestParsingTest()
    {
        string requestJsonMethod = "{\"jsonrpc\": \"2.0\", \"method\": \"extract_data\", \"id\": 42}";
        var req1 = JsonSerializer.Deserialize<RpcRequest>(requestJsonMethod);
        Assert.NotNull(req1);
        Assert.Equal("extract_data", req1!.ActionOrMethod);

        string requestJsonAction = "{\"jsonrpc\": \"2.0\", \"action\": \"SancadExtractor:Main.cs:main\", \"id\": \"abc\"}";
        var req2 = JsonSerializer.Deserialize<RpcRequest>(requestJsonAction);
        Assert.NotNull(req2);
        Assert.Equal("SancadExtractor:Main.cs:main", req2!.ActionOrMethod);
    }

    [Fact]
    public void ManifestHandshakeContentTest()
    {
        // Check manifest.json exists and conforms to manifest-v1.md
        var manifestPath = Path.Combine("..", "..", "..", "..", "SewerSystem", "manifest.json");
        Assert.True(File.Exists(manifestPath), $"manifest.json should exist at {manifestPath}");

        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json);

        Assert.NotNull(manifest);
        Assert.Equal("SanCAD Exporter", manifest!.name);
        Assert.Equal("com.william.sancad-extractor", manifest.id);
        Assert.Equal("extraction", manifest.category);
        Assert.NotEmpty(manifest.exports);

        var export = manifest.exports.First();
        Assert.Equal("extract_data", export.id);
        Assert.Equal("SancadExtractor:Main.cs:main", export.entrypoint);
    }

    [Fact]
    public async Task TcpHandshakeAndJsonRpcRoundTripTest()
    {
        // Start a mock TCP server simulating the Rust orchestrator on dynamic port
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            using var stream = serverClient.GetStream();
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8) { AutoFlush = true };

            // 1. Read Handshake line
            var handshakeLine = await reader.ReadLineAsync();
            Assert.NotNull(handshakeLine);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(handshakeLine!);
            Assert.NotNull(manifest);
            Assert.Equal("com.william.sancad-extractor", manifest!.id);

            // 2. Send JSON-RPC request to client
            await writer.WriteLineAsync("{\"jsonrpc\": \"2.0\", \"method\": \"extract_data\", \"id\": 999}");

            // 3. Read JSON-RPC response from client
            var responseLine = await reader.ReadLineAsync();
            Assert.NotNull(responseLine);
            var response = JsonSerializer.Deserialize<RpcResponse>(responseLine!);
            Assert.NotNull(response);
            Assert.Equal(999, ((JsonElement)response!.Id!).GetInt32());
        });

        // Client connects to server
        using (var client = new System.Net.Sockets.TcpClient())
        {
            await client.ConnectAsync(System.Net.IPAddress.Loopback, port);
            using var clientStream = client.GetStream();
            using var clientReader = new StreamReader(clientStream, System.Text.Encoding.UTF8);
            using var clientWriter = new StreamWriter(clientStream, System.Text.Encoding.UTF8) { AutoFlush = true };

            // Client sends handshake
            var manifestJson = "{\"name\":\"SanCAD Exporter\",\"id\":\"com.william.sancad-extractor\",\"category\":\"extraction\",\"exports\":[]}";
            await clientWriter.WriteLineAsync(manifestJson);

            // Client reads JSON-RPC request
            var reqLine = await clientReader.ReadLineAsync();
            var req = JsonSerializer.Deserialize<RpcRequest>(reqLine!);
            Assert.Equal("extract_data", req!.ActionOrMethod);

            // Client sends JSON-RPC response
            var rpcResponse = new RpcResponse
            {
                JsonRpc = "2.0",
                Id = req.Id,
                Result = new SewerSystemDTO()
            };
            await clientWriter.WriteLineAsync(JsonSerializer.Serialize(rpcResponse));
        }

        await serverTask;
        listener.Stop();
    }
}
