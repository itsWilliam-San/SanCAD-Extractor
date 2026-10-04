using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Sewarage.Models;
using SewerSystem.Contracts;

namespace Sewarage.Services;

public static class PluginService
{
   public static List<Plugin> LoadPlugin(string folderPath)
   {
      var plugins = new List<Plugin>();

      foreach (var file in Directory.GetFiles(folderPath, "*.dll"))
      {
         try
         {
            var assembly = Assembly.LoadFile(file);

            var types = assembly.GetTypes();
            foreach (var type in types)
            {
               var attribute = type.GetCustomAttributes<PluginInfoAttribute>();
            }
         }
         catch (Exception ex)
         {
            Debug.WriteLine(ex.Message); 
         }
      }

      return plugins;
   }
}