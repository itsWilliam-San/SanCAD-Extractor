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
        const string IP = "127.0.0.1";
        const string PORT = "8080";

        private TcpClient client;
        private NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer
        private bool _disposed = false;

        public static void Start(PluginManifest manifest)
        {
            Connect(IP, PORT);

            var options = new JsonSerializerOptions
            {
                WriteIndented = false
            };

            var manifestJson = JsonSerializer.Serialize(manifest, options);

            writer.WriteLine(manifest);
            writer.Flush();

            // Let anyone read this, then we start reading and waiting for messages

            var newThread = new Thread(new ThreadStart(() =>
            {
                while (true)
                {
                    var message = reader.ReadLine();

                    // Read JSON-RPC from Client
                }
            }));
        }

        private void Connect(string ip, int port)
        {
            client = new TcpClient(ip, port);
            stream = _client.GetStream();

            reader = new StreamReader(stream, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _client?.Close();
            GC.SuppressFinalize(this);
        }
    }

    public class SewerSystemApp : IExtensionApplication
    {
        public class PluginMessagebUS

        PluginTcpClient _client;
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


        public void Initialize()
        {
            // Essa channel é para a Thread do Socket envie para a thread do ZwCAD
            Channel.CreateUnbounded<RpcCommandRequest>();


            _client = new();
            _client.Connect(IP, PORT);

        }
        public void Terminate()
        {
            _client.Dispose();
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

        [CommandMethod("SewerSystem")]
        public void SewerSystem()
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

            extractor.Entry();
        }
    }
}
