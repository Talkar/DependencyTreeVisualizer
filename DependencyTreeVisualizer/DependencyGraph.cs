using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace DependencyTreeVisualizer
{
    internal interface IDependencyGraph
    {
        void GenerateGraph(string filePath, int depth, bool includeCoreComponents, bool includeServices);
    }
    internal class DependencyGraph : IDependencyGraph
    {

        private readonly HashSet<string> visitedFiles = new HashSet<string>();
        private readonly Dictionary<string, List<string>> fileDependencies = new Dictionary<string, List<string>>();

        private void ExploreFile(string filePath, int depth, bool includeCoreComponents, bool includeServices)
        {
            if (visitedFiles.Contains(filePath) || depth <= 0) return;
            visitedFiles.Add(filePath);

            string fileContent = File.ReadAllText(filePath);
            var matches = Regex.Matches(fileContent, @"import\s+(?:.+\s+from\s+)?['""](.*?)['""]");

            foreach (Match match in matches)
            {
                string relativePath = match.Groups[1].Value;
                string fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath), relativePath));

                // Assuming .tsx extension for simplicity; adjust based on actual import patterns
                if (Path.HasExtension(filePath) && !Path.GetExtension(filePath).Equals(".tsx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!Path.HasExtension(fullPath))
                    fullPath += ".tsx";

                if (!fileDependencies.ContainsKey(filePath))
                {
                    fileDependencies[filePath] = new List<string>();
                }
                if (File.Exists(fullPath))
                {
                    if (!includeCoreComponents && fullPath.IndexOf("scripts\\components\\core", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }
                    if (!includeServices && fullPath.IndexOf("Scripts\\services", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    fileDependencies[filePath].Add(fullPath);
                    ExploreFile(fullPath, depth - 1, includeCoreComponents, includeServices); // Decrement depth
                }
            }
        }

        public void GenerateGraph(string filePath, int depth, bool includeCoreComponents, bool includeServices)
        {
            ExploreFile(filePath, depth, includeCoreComponents, includeServices);
            var dotFilePath = PrintGraph();
            GenerateAndShowImage(dotFilePath);
            visitedFiles.Clear();
            fileDependencies.Clear();
        }

        private string PrintGraph()
        {
            string tempFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");
            // Replace the .tmp extension with .png
            tempFileName = Path.ChangeExtension(tempFileName, ".dot");
            using (StreamWriter file = new StreamWriter(tempFileName))
            {
                file.WriteLine("digraph G {");
                file.WriteLine("node [shape=box];"); // Sets the shape of nodes to boxes

                // Define nodes and edges
                foreach (var fileDependency in fileDependencies)
                {
                    string fromNode = Path.GetFileNameWithoutExtension(fileDependency.Key);
                    foreach (var dependency in fileDependency.Value)
                    {
                        string toNode = Path.GetFileNameWithoutExtension(dependency);
                        file.WriteLine($"\"{fromNode}\" -> \"{toNode}\";");
                    }
                }

                file.WriteLine("}");

            }
            return tempFileName;
        }

        private void GenerateAndShowImage(string dotFilePath)
        {
            string tempFileName = Path.ChangeExtension(dotFilePath, ".png");
            ProcessStartInfo processInfo = new ProcessStartInfo("dot", $"-Tpng \"{dotFilePath}\" -o \"{tempFileName}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            };

            Process process = Process.Start(processInfo);
            process.WaitForExit(Convert.ToInt32(TimeSpan.FromSeconds(5).TotalMilliseconds));
            if (process.ExitCode != 0)
            {
                throw new Exception($"Error generating image. ExitCode: {process.ExitCode}");
            }
            Process.Start(tempFileName);
        }
    }
}
