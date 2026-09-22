using System;
using System.Diagnostics;
using System.IO;

namespace SewerSystem
{
    public class PythonBridge
    {
        private const string PythonPath = "python-runtime/python.exe";

        /// <summary>
        /// Setup the Python ProcessStartInfo, without any args
        /// </summary>
        /// <returns></returns>
        private ProcessStartInfo SetupPythonBridge(string args)
        {
            var pythonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PythonPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                UseShellExecute = false,
                Arguments = $"\"{pythonPath}\" {args}",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            return startInfo;
        }
        
        public void RunPython(string jsonData)
        {
            var jsonTempPath = Path.GetTempFileName();
            File.WriteAllText(jsonTempPath, jsonData);
            
            var startInfo = SetupPythonBridge($"\"{jsonTempPath}\"");

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEnd(); 
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Python bridge exited with code {process.ExitCode}");
                }
            }

            if (File.Exists(jsonTempPath))
            {
                File.Delete(jsonTempPath);
            }
        } 
    }
}