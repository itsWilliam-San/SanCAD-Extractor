using SewerSystem.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SewerSystem
{
    internal class ExtractorLoader
    {
        public static List<IExtractor> LoadPlugins(string path)
        {
            var pluginsLoaded = new List<IExtractor>();

            if (!Directory.Exists(path))
                return pluginsLoaded;
            
            string[] libs = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories);

            foreach (var type in libs)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(type);
                    var assemblyName = assembly.GetName().Name;

                    if (assemblyName == null 
                        || assemblyName.StartsWith("System") 
                        || assemblyName.StartsWith("Microsoft")
                        || assemblyName.StartsWith("Design"))
                        continue;

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    } catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray();
                    }

                    foreach (var t in types)
                    {
                        if (typeof(IExtractor).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                        {
                            var extractor = (IExtractor)Activator.CreateInstance(t);
                            pluginsLoaded.Add(extractor);
                        }
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load the DLL {type}: {e.Message}");
                }
            }
            
            return pluginsLoaded;
        }
    }
}
