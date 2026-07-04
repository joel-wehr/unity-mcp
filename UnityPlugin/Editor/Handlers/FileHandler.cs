using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles file read/write operations within the Unity project.
    /// Provides direct access to project files (scripts, shaders, configs, etc.)
    /// </summary>
    public class FileHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "file_operations" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "read": return ReadFile(p);
                case "write": return WriteFile(p);
                case "list": return ListFiles(p);
                case "exists": return FileExists(p);
                case "search": return SearchFiles(p);
                case "get_script_classes": return GetScriptClasses(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object ReadFile(Dictionary<string, string> p)
        {
            var filePath = p.GetValueOrDefault("path");
            if (string.IsNullOrEmpty(filePath))
                return new { success = false, error = "path is required" };

            var fullPath = ResolveProjectPath(filePath);
            if (!File.Exists(fullPath))
                return new { success = false, error = $"File not found: {filePath}" };

            // Security: ensure path is within the project
            if (!IsWithinProject(fullPath))
                return new { success = false, error = "Path is outside the Unity project directory" };

            try
            {
                var content = File.ReadAllText(fullPath);
                var info = new FileInfo(fullPath);

                return new
                {
                    success = true,
                    path = filePath,
                    fullPath = fullPath,
                    content = content,
                    sizeBytes = info.Length,
                    lastModified = info.LastWriteTimeUtc.ToString("o"),
                    extension = info.Extension
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Failed to read file: {ex.Message}" };
            }
        }

        private object WriteFile(Dictionary<string, string> p)
        {
            var filePath = p.GetValueOrDefault("path");
            var content = p.GetValueOrDefault("content");
            if (string.IsNullOrEmpty(filePath))
                return new { success = false, error = "path is required" };
            if (content == null)
                return new { success = false, error = "content is required" };

            var fullPath = ResolveProjectPath(filePath);

            // Security: ensure path is within the project
            if (!IsWithinProject(fullPath))
                return new { success = false, error = "Path is outside the Unity project directory" };

            try
            {
                // Create directory if needed
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var isNew = !File.Exists(fullPath);
                File.WriteAllText(fullPath, content);

                // If it's inside Assets, import it so Unity picks it up
                if (fullPath.Replace("\\", "/").Contains("/Assets/"))
                {
                    var relativePath = GetRelativePath(fullPath);
                    if (!string.IsNullOrEmpty(relativePath))
                        AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
                }

                return new
                {
                    success = true,
                    path = filePath,
                    fullPath = fullPath,
                    created = isNew,
                    sizeBytes = new FileInfo(fullPath).Length,
                    message = isNew ? $"Created {filePath}" : $"Updated {filePath}"
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Failed to write file: {ex.Message}" };
            }
        }

        private object ListFiles(Dictionary<string, string> p)
        {
            var dirPath = p.GetValueOrDefault("path") ?? "Assets";
            var pattern = p.GetValueOrDefault("pattern") ?? "*.*";
            var recursive = p.GetValueOrDefault("recursive")?.ToLower() == "true";

            var fullPath = ResolveProjectPath(dirPath);
            if (!Directory.Exists(fullPath))
                return new { success = false, error = $"Directory not found: {dirPath}" };

            if (!IsWithinProject(fullPath))
                return new { success = false, error = "Path is outside the Unity project directory" };

            try
            {
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.GetFiles(fullPath, pattern, searchOption)
                    .Where(f => !f.EndsWith(".meta"))
                    .Select(f =>
                    {
                        var info = new FileInfo(f);
                        return new
                        {
                            name = info.Name,
                            path = GetRelativePath(f),
                            extension = info.Extension,
                            sizeBytes = info.Length,
                            lastModified = info.LastWriteTimeUtc.ToString("o")
                        };
                    })
                    .Take(500) // Limit results
                    .ToList();

                return new
                {
                    success = true,
                    directory = dirPath,
                    pattern = pattern,
                    recursive = recursive,
                    count = files.Count,
                    files = files
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Failed to list files: {ex.Message}" };
            }
        }

        private object FileExists(Dictionary<string, string> p)
        {
            var filePath = p.GetValueOrDefault("path");
            if (string.IsNullOrEmpty(filePath))
                return new { success = false, error = "path is required" };

            var fullPath = ResolveProjectPath(filePath);
            var exists = File.Exists(fullPath);
            var isDir = Directory.Exists(fullPath);

            return new
            {
                success = true,
                path = filePath,
                exists = exists || isDir,
                isFile = exists,
                isDirectory = isDir
            };
        }

        private object SearchFiles(Dictionary<string, string> p)
        {
            var query = p.GetValueOrDefault("query");
            var dirPath = p.GetValueOrDefault("path") ?? "Assets";
            var extensionFilter = p.GetValueOrDefault("extension") ?? ".cs";

            if (string.IsNullOrEmpty(query))
                return new { success = false, error = "query (search text) is required" };

            var fullPath = ResolveProjectPath(dirPath);
            if (!Directory.Exists(fullPath))
                return new { success = false, error = $"Directory not found: {dirPath}" };

            if (!IsWithinProject(fullPath))
                return new { success = false, error = "Path is outside the Unity project directory" };

            try
            {
                var matchingFiles = new List<object>();
                var files = Directory.GetFiles(fullPath, $"*{extensionFilter}", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    if (file.EndsWith(".meta")) continue;
                    try
                    {
                        var content = File.ReadAllText(file);
                        if (content.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            // Find matching lines
                            var lines = content.Split('\n');
                            var matchingLines = new List<object>();
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                                {
                                    matchingLines.Add(new { lineNumber = i + 1, text = lines[i].Trim() });
                                    if (matchingLines.Count >= 5) break; // Limit matches per file
                                }
                            }

                            matchingFiles.Add(new
                            {
                                path = GetRelativePath(file),
                                matches = matchingLines
                            });

                            if (matchingFiles.Count >= 50) break; // Limit total results
                        }
                    }
                    catch { /* Skip unreadable files */ }
                }

                return new
                {
                    success = true,
                    query = query,
                    directory = dirPath,
                    extension = extensionFilter,
                    count = matchingFiles.Count,
                    results = matchingFiles
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Search failed: {ex.Message}" };
            }
        }

        private object GetScriptClasses(Dictionary<string, string> p)
        {
            var filePath = p.GetValueOrDefault("path");
            if (string.IsNullOrEmpty(filePath))
                return new { success = false, error = "path is required" };

            var fullPath = ResolveProjectPath(filePath);
            if (!File.Exists(fullPath))
                return new { success = false, error = $"File not found: {filePath}" };

            try
            {
                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(GetRelativePath(fullPath));
                if (monoScript == null)
                    return new { success = false, error = $"Not a valid script asset: {filePath}" };

                var scriptClass = monoScript.GetClass();
                if (scriptClass == null)
                    return new { success = true, path = filePath, message = "Script has no MonoBehaviour or ScriptableObject class" };

                var methods = scriptClass.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                    .Select(m => new { name = m.Name, returnType = m.ReturnType.Name, parameters = m.GetParameters().Select(mp => new { name = mp.Name, type = mp.ParameterType.Name }).ToArray() })
                    .ToList();

                var fields = scriptClass.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                    .Select(f => new { name = f.Name, type = f.FieldType.Name })
                    .ToList();

                var serializedFields = scriptClass.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Where(f => f.GetCustomAttributes(typeof(SerializeField), true).Length > 0)
                    .Select(f => new { name = f.Name, type = f.FieldType.Name })
                    .ToList();

                return new
                {
                    success = true,
                    path = filePath,
                    className = scriptClass.Name,
                    namespaceName = scriptClass.Namespace,
                    baseClass = scriptClass.BaseType?.Name,
                    isMonoBehaviour = typeof(MonoBehaviour).IsAssignableFrom(scriptClass),
                    isScriptableObject = typeof(ScriptableObject).IsAssignableFrom(scriptClass),
                    publicMethods = methods,
                    publicFields = fields,
                    serializedFields = serializedFields
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Failed to analyze script: {ex.Message}" };
            }
        }

        private string ResolveProjectPath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            // Treat relative paths as relative to the project root
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private bool IsWithinProject(string fullPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var normalizedPath = Path.GetFullPath(fullPath).Replace("\\", "/");
            var normalizedRoot = Path.GetFullPath(projectRoot).Replace("\\", "/");
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private string GetRelativePath(string fullPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var normalized = fullPath.Replace("\\", "/");
            var rootNormalized = projectRoot.Replace("\\", "/");
            if (normalized.StartsWith(rootNormalized, StringComparison.OrdinalIgnoreCase))
            {
                var relative = normalized.Substring(rootNormalized.Length);
                if (relative.StartsWith("/")) relative = relative.Substring(1);
                return relative;
            }
            return fullPath;
        }
    }
}
