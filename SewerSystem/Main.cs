using System;
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

    public class SewerSystemApp : IExtensionApplication
    {
        private bool _running = true;

        private void OpenSocket()
        {
            Task.Run(async () =>
            {
                while (_running)
                {
                    try
                    {
                        using var client = new NamedPipeClientStream(".", "ipcChannel", PipeDirection.InOUt);
                        await client.ConnectAsync();

                        using var reader = new StreamReader(client);
                        using var writer = new StreamWriter(client) { AutoFlush = true };

                        while (client.IsConnected && _running)
                        {
                            string mensage = await reader.ReadLineAsync();

                            // Do Something

                            await writer.WriteLineAsync("OK");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Try again later
                        await Task.Delay(2000);
                    }
                }
            });
        }

        private void CloseSocket()
        {
            _running = false;
        }

        public void Initialize()
        {
            OpenSocket();
        }
        public void Terminate()
        {
            CloseSocket();
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

            // var texts = GetTextEntities(db, objectResult.Value);

            var plugins = ExtractorLoader.LoadPlugins(PluginsPath);

            var uiManager = new UiManager();
        }

        // This is used for testing later
        private void SerializeData<T>(T data)
        {
            string jsonString = JsonConvert.SerializeObject(data, Formatting.Indented);

            File.WriteAllText("data.json", jsonString);
        }
    }
}
