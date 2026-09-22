using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SewerSystem.Contracts
{
    public class TextEntity
    {
        public struct Position
        {
            public double X {get;set;} 
            public double Y {get;set;}
        }

        private string _content;
        
        public string Content
        {
            get => _content;
            set => _content = value?.Trim() ?? string.Empty;
        }

        public Position Coordinates { get; set; }
    }
    
    public interface IExtractor
    {
        public Manhole Entry(TextEntity[] textEntity); 
    }

    public class ExtractorLoader
    {
        public List<IExtractor> LoadPlugins(string path)
        {
            var pluginsLoaded = new List<IExtractor>();

            if (!Directory.Exists(path))
                return  pluginsLoaded;
            
            string[] libs = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories);

            foreach (var type in libs)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(type);

                    foreach (var t in assembly.GetTypes())
                    {
                        if (typeof(IExtractor).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
                        {
                            var extractor = (IExtractor)Activator.CreateInstance(t);
                            pluginsLoaded.Add(extractor);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to load the DLL {type}: {e.Message}");
                    throw;
                }
            }
            
            return pluginsLoaded;
        }
    }
}