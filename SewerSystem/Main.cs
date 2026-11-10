using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Exception = System.Exception;
using SewerSystem.Contracts;
using ZwSoft.ZwCAD.ApplicationServices.Core;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Runtime;

[assembly: CommandClass(typeof(SewerSystem.Main))]
[assembly: ExtensionApplication(typeof(SewerSystem.SewerSystemApp))]

namespace SewerSystem
{
    public class BridgeService
    {
        private static BridgeService? _instance;
        private static readonly object _lock = new object();

        public Channel<RpcRequest> SocketToCad { get; }
        public Channel<RpcResponse> CadToSocket { get; }

        public object? ActiveRequestId { get; set; }

        private BridgeService()
        {
            SocketToCad = Channel.CreateUnbounded<RpcRequest>();
            CadToSocket = Channel.CreateUnbounded<RpcResponse>();
        }

        public static BridgeService Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ??= new BridgeService();
                }
            }
        }
    }

    public class PluginTcpClient : IDisposable
    {
        private static PluginTcpClient? _instance;
        private static readonly object _lock = new object();

        private const string IP = "127.0.0.1";
        private const int PORT = 8080;

        private CancellationTokenSource? _cts;
        private TcpClient? _client;
        private NetworkStream? _stream;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private bool _disposed;
        private readonly AutoResetEvent _reconnectTrigger = new AutoResetEvent(false);

        public static PluginTcpClient Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ??= new PluginTcpClient();
                }
            }
        }

        private static void WriteCadMessage(string message)
        {
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                doc?.Editor?.WriteMessage(message);
            }
            catch
            {
                // Ignora se invocado fora do contexto com documento ativo
            }
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => ConnectionLoopAsync(_cts.Token));
        }

        public void Reconnect()
        {
            _reconnectTrigger.Set();
        }

        private static string LoadManifestJson()
        {
            var candidates = new[]
            {
                Path.Combine(Path.GetDirectoryName(typeof(SewerSystemApp).Assembly.Location) ?? string.Empty, "manifest.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manifest.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "manifest.json")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    var raw = File.ReadAllText(path, Encoding.UTF8);
                    using var doc = JsonDocument.Parse(raw);
                    return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = false });
                }
            }

            throw new FileNotFoundException("manifest.json não foi encontrado.");
        }

        private async Task ConnectionLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                try
                {
                    WriteCadMessage($"\n[SewerSystem] Conectando ao orquestrador em {IP}:{PORT}...\n");
                    _client = new TcpClient();
                    await _client.ConnectAsync(IP, PORT);
                    _stream = _client.GetStream();
                    _reader = new StreamReader(_stream, new UTF8Encoding(false));
                    _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };

                    // Envio do Handshake (manifest.json minificado em linha única com \n)
                    var manifestJson = LoadManifestJson();
                    await _writer.WriteLineAsync(manifestJson);
                    await _writer.FlushAsync();
                    WriteCadMessage("\n[SewerSystem] Conectado e Handshake enviado com sucesso!\n");

                    // Task responsável por escutar o canal CadToSocket e transmitir ao orquestrador
                    var sendTask = Task.Run(async () =>
                    {
                        var readerChannel = BridgeService.Instance.CadToSocket.Reader;
                        while (!cancellationToken.IsCancellationRequested && _writer != null)
                        {
                            var response = await readerChannel.ReadAsync(cancellationToken);
                            var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
                            {
                                WriteIndented = false
                            });
                            await _writer.WriteLineAsync(responseJson);
                            await _writer.FlushAsync();
                        }
                    }, cancellationToken);

                    // Loop de leitura de requisições JSON-RPC do socket
                    while (!cancellationToken.IsCancellationRequested && _reader != null)
                    {
                        var line = await _reader.ReadLineAsync();
                        if (line == null)
                        {
                            // Conexão encerrada pelo servidor
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        try
                        {
                            var request = JsonSerializer.Deserialize<RpcRequest>(line);
                            if (request != null)
                            {
                                var action = request.ActionOrMethod;
                                if (action.Equals("extract_data", StringComparison.OrdinalIgnoreCase) ||
                                    action.Equals("SancadExtractor:Main.cs:main", StringComparison.OrdinalIgnoreCase))
                                {
                                    BridgeService.Instance.ActiveRequestId = request.Id;
                                    await BridgeService.Instance.SocketToCad.Writer.WriteAsync(request, cancellationToken);

                                    var doc = Application.DocumentManager.MdiActiveDocument;
                                    if (doc != null)
                                    {
                                        doc.SendStringToExecute("EXTRACTDATA\n", true, false, false);
                                    }
                                    else
                                    {
                                        BridgeService.Instance.CadToSocket.Writer.TryWrite(new RpcResponse
                                        {
                                            JsonRpc = "2.0",
                                            Id = request.Id,
                                            Error = new RpcError { Code = -32000, Message = "Nenhum documento ativo aberto no CAD." }
                                        });
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteCadMessage($"\n[SewerSystem] Erro ao processar requisição JSON-RPC: {ex.Message}\n");
                        }
                    }

                    await sendTask;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested && !_disposed)
                    {
                        WriteCadMessage($"\n[SewerSystem] Não foi possível conectar ao orquestrador ({ex.Message}). Tentando novamente em 5 segundos ou use RECONNECTBRIDGE.\n");
                    }
                }
                finally
                {
                    CleanupConnection();
                }

                if (!cancellationToken.IsCancellationRequested && !_disposed)
                {
                    // Aguarda 5 segundos ou sinal do comando RECONNECTBRIDGE
                    await Task.Run(() => _reconnectTrigger.WaitOne(5000), cancellationToken);
                }
            }
        }

        private void CleanupConnection()
        {
            try { _writer?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _client?.Close(); } catch { }

            _writer = null;
            _reader = null;
            _stream = null;
            _client = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts?.Cancel();
            _reconnectTrigger.Set();
            CleanupConnection();
            _reconnectTrigger.Dispose();
            _cts?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public class SewerSystemApp : IExtensionApplication
    {
        public void Initialize()
        {
            try
            {
                // Inicia o bridge e canais de conexão
                _ = BridgeService.Instance;
                PluginTcpClient.Instance.Start();
                var doc = Application.DocumentManager.MdiActiveDocument;
                doc?.Editor?.WriteMessage("\n[SewerSystem] Plugin carregado com sucesso!\n");
            }
            catch (Exception ex)
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                doc?.Editor?.WriteMessage($"\n[SewerSystem] Falha ao inicializar plugin: {ex.Message}\n"); // Atualmente dando erro por causa do System.Text.Json que náo esta sendo possivel carregar pois nao encontra o assembly System.Text.Json.dll.
            }
        }

        public void Terminate()
        {
            PluginTcpClient.Instance.Dispose();
        }
    }

    public class Main
    {
        private readonly SelectionFilter _filter = new SelectionFilter(new[]
        {
            new TypedValue((int)DxfCode.Start, "TEXT")
        });

        private static TextEntity[] GetTextEntities(Database db, SelectionSet entities)
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
            if (acDoc == null)
            {
                return;
            }

            var db = acDoc.Database;
            var ed = acDoc.Editor;

            var pso = new PromptSelectionOptions
            {
                MessageForAdding = "\nSelecione os textos da tabela SanCAD: "
            };

            var objectResult = ed.GetSelection(pso, _filter);

            if (objectResult.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[SewerSystem] Seleção cancelada pelo usuário.\n");
                BridgeService.Instance.CadToSocket.Writer.TryWrite(new RpcResponse
                {
                    JsonRpc = "2.0",
                    Id = BridgeService.Instance.ActiveRequestId,
                    Error = new RpcError
                    {
                        Code = -32000,
                        Message = "Selection cancelled by user"
                    }
                });
                return;
            }

            try
            {
                var texts = GetTextEntities(db, objectResult.Value);
                var extractor = new Extractor.SancadExtractor();
                var result = extractor.Entry(texts);

                BridgeService.Instance.CadToSocket.Writer.TryWrite(new RpcResponse
                {
                    JsonRpc = "2.0",
                    Id = BridgeService.Instance.ActiveRequestId,
                    Result = result
                });

                ed.WriteMessage($"\n[SewerSystem] Extração concluída com sucesso! ({result.Primary.Count} poços, {result.Segments.Count} trechos, {result.Intermediates.Count} estacas)\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[SewerSystem] Erro na extração: {ex.Message}\n");
                BridgeService.Instance.CadToSocket.Writer.TryWrite(new RpcResponse
                {
                    JsonRpc = "2.0",
                    Id = BridgeService.Instance.ActiveRequestId,
                    Error = new RpcError
                    {
                        Code = -32000,
                        Message = ex.Message
                    }
                });
            }
        }

        [CommandMethod("RECONNECTBRIDGE")]
        public void ReconnectBridge()
        {
            var acDoc = Application.DocumentManager.MdiActiveDocument;
            acDoc?.Editor?.WriteMessage("\n[SewerSystem] Solicitando reconexão imediata ao orquestrador...\n");
            PluginTcpClient.Instance.Reconnect();
        }
    }
}
