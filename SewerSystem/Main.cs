using System;
using System.Net.Sockets;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SewerSystem.Contracts;
using ZwSoft.ZwCAD.ApplicationServices.Core;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Runtime;

[assembly: CommandClass(typeof(SewerSystem.Main))]
[assembly: ExtensionApplication(typeof(SewerSystem.SewerSystemApp))]

namespace SewerSystem
{
    private class class PluginTcpClient : IDisposable
    {
        BridgeService _bridgeInstance;
        const string IP = "127.0.0.1";
        const string PORT = "8080";

        private TcpClient client;
        private NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer
        private bool _disposed = false;

        public PluginTcpClient()
        {
            _bridgeInstance = BridgeService.Instace;
        }

        private async void SendHanshake(PluginManifest manifest)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false
            };

            var manifestJson = JsonSerializer.Serialize(manifest, options);

            await writer.WriteAsync(manifest);
        }

        public void Start(PluginManifest manifest)
        {
            Connect(IP, PORT);

            Task.Run(async () =>
            {
                await SendHanshake(manifest);
                while (true)
                {
                    var message = await reader.ReadLineAsync();

                    await _bridgeInstance.SocketToCad.Writer.WriteAsync(message);
                }
            })
        }

        private void Connect(string ip, int port)
        {
            client = new TcpClient(ip, port);
            stream = _client.GetStream();

            reader = new StreamReader(stream, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            stream?.Dispose();
            client?.Close();
            GC.SuppressFinalize(this);
        }
    }

    public class SewerSystemApp : IExtensionApplication
    {
        PluginTcpClient _tcpInstance;

        readonly string ManifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manifest.json");

        private static PluginManifest LoadManifest()
        {
            if (File.Exists(ManifestPath))
            {
                var jsonString = File.ReadAllText(ManifestPath)
                var manifest = JsonSerializer.Deserialize<PluginManifest>(jsonString) ?? throw new InvalidOperationException("The manifest is invalid");

                return manifest;
            } else
            {
                throw new Exception("Couldn't find Manifest file");
            }
        }

        public SewerSystemApp()
        {
            _tcpInstance = PluginTcpClient.new();
        }

        public void Initialize()
        {
            _tcpInstance.Start(LoadManifest());
        }
        public void Terminate()
        {
            _client.Dispose();
        }
    }
    
    public class BridgeService
    {
        private static BridgeService _instance;
        private static readonly object _lock = new object();

        public Channel<RpcCommandRequest> SocketToCad { get; }
        public Channel<RpcCommandRequest> CadToSocket { get; }

        private BridgeService()
        {
            SocketToCad = Channel.CreateUnbounded<RpcCommandRequest>();
            CadToSocket = Channel.CreateUnbounded<RpcCommandRequest>();
        }

        public void Start()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    var SocketToCadTask = SocketToCad.Reader.WaitToReadAsync().AsTask();
                    var CadToSocketTask = CadToSocket.Reader.WaitToReadAsync().AsTask();

                    var taskFinished = await Task.WhenAny(SocketToCadTask, CadToSocketTask);

                    if (taskFinished == SocketToCadReader && await SocketToCadTask)
                    {
                        while (SocketToCad.Reader.TryRead(out var msg)) {
                            // 
                        }
                    } else if (taskFinished == CadToSocketTask && await CadToSocketTask)
                    {
                        while (CadToSocket.Reader.TryRead(out var msg))
                        {
                            //
                        }
                    }
                }
            });
        }

        public static BridgeService Instace
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new BridgeService();
                    }
                    return _instance;
                }
            }
        }
    }

    public class Main
    {
        private readonly string PluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

        private readonly SelectionFilter
            _filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT") });

        private TextEntity[] GetTextEntities(Database db, SelectionSet entities)
        {
            using (var t = db.TransactionManager.StartTransaction())
            {
                return entities.GetObjectIds()
                    .Select(id => (Entity)t.GetObject(id, OpenMode.ForRead))
                    .OfType<DBText>()
                    .Select(dbtext => new TextEntity
                    {
                        Content = dbtext.TextString,
                        Coordinates = new TextEntity.Position
                        {
                            X = dbtext.Position.X,
                            Y = dbtext.Position.Y
                        }
                    })
                    .ToArray();
            }
        }

        [CommandMethod("ExtractData")]
        public void ExtractData()
        {
            var acDoc = Application.DocumentManager.MdiActiveDocument;
            var db = acDoc.Database;
            var ed = acDoc.Editor;

            var objectResult = ed.GetSelection(new PromptSelectionOptions(), _filter);

            if (objectResult.Status != PromptStatus.OK)
            {
                return;
            }

            var texts = GetTextEntities(db, objectResult.Value);

            var extractor = SancadExtractor.new();

            var result = extractor.Entry();

            BridgeService.Instace.CadToSocket.Writer.Write(result);
        }
    }
}
