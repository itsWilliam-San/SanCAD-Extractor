using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SewerSystem.Contracts;
using ZwSoft.ZwCAD.ApplicationServices.Core;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Runtime;

[assembly: CommandClass(typeof(SewerSystem.Entry))]

namespace SewerSystem
{
    public class Entry
    {
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
            
            SerializeData(texts);
        }
        
        // This is used for testing later
        private void SerializeData<T>(T data)
        {
            string jsonString = JsonConvert.SerializeObject(data, Formatting.Indented); 
            
            File.WriteAllText("data.json", jsonString);
        }
    }
}