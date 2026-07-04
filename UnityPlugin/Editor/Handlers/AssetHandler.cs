using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles Asset-related MCP tool requests.
    /// </summary>
    public class AssetHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "add_asset_to_scene",
            "create_prefab",
            "manage_asset",
            "add_package",
            "add_external_dll"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "add_asset_to_scene":
                    return AddAssetToScene(paramsDict);
                case "create_prefab":
                    return CreatePrefab(paramsDict);
                case "manage_asset":
                    return ManageAsset(paramsDict);
                case "add_package":
                    return AddPackage(paramsDict);
                case "add_external_dll":
                    return AddExternalDll(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        // Used by add_external_dll: the Node side has already downloaded the URL to a temp
        // file and passes the absolute sourcePath. We just need to copy it into the project's
        // Assets/<destinationFolder>/<fileName> and trigger a refresh so Unity imports it
        // (creating the .meta file).
        private object AddExternalDll(Dictionary<string, string> @params)
        {
            var sourcePath = @params.GetValueOrDefault("sourcePath");
            var destinationFolder = @params.GetValueOrDefault("destinationFolder");
            var fileName = @params.GetValueOrDefault("fileName");
            var overwriteStr = @params.GetValueOrDefault("overwrite");
            var overwrite = string.IsNullOrEmpty(overwriteStr)
                ? true
                : !string.Equals(overwriteStr, "false", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(sourcePath))
                return new { success = false, error = "sourcePath is required" };
            if (string.IsNullOrEmpty(destinationFolder))
                return new { success = false, error = "destinationFolder is required" };
            if (string.IsNullOrEmpty(fileName))
                return new { success = false, error = "fileName is required" };

            if (!System.IO.File.Exists(sourcePath))
                return new { success = false, error = $"Source file not found: {sourcePath}" };

            // Reject path traversal — the Node side already validates, defense-in-depth here.
            var safeFolder = destinationFolder.Replace('\\', '/').Trim('/');
            if (safeFolder.Contains("..") || System.IO.Path.IsPathRooted(safeFolder))
                return new { success = false, error = "destinationFolder must be a relative path under Assets/" };
            if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
                return new { success = false, error = "fileName must be a bare file name" };

            // Ensure folder chain exists in the AssetDatabase.
            var projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
            var assetsRelative = "Assets/" + safeFolder;
            var parts = safeFolder.Split('/');
            var current = "Assets";
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, part);
                }
                current = next;
            }

            // Copy the file in. AssetDatabase.Refresh() picks it up and creates the .meta.
            var assetPath = assetsRelative + "/" + fileName;
            var absDest = System.IO.Path.Combine(projectRoot, assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var existed = System.IO.File.Exists(absDest);
            if (existed && !overwrite)
            {
                return new { success = false, error = $"File already exists and overwrite is false: {assetPath}" };
            }

            try
            {
                System.IO.File.Copy(sourcePath, absDest, overwrite);
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Failed to copy DLL into Assets: {ex.Message}" };
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            long sizeBytes = 0;
            try { sizeBytes = new System.IO.FileInfo(absDest).Length; } catch { }

            return new
            {
                success = true,
                assetPath = assetPath,
                fileSizeBytes = sizeBytes,
                overwritten = existed,
                note = "DLL copied into Assets. If it's a managed plugin, Unity's PluginImporter may need additional configuration via update_component / execute_code."
            };
        }

        private object AddAssetToScene(Dictionary<string, string> @params)
        {
            var assetPath = @params.GetValueOrDefault("assetPath");
            var guid = @params.GetValueOrDefault("guid");
            var parentPath = @params.GetValueOrDefault("parentPath");
            var parentIdStr = @params.GetValueOrDefault("parentId");

            // Get asset path from GUID if provided
            if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(guid))
            {
                assetPath = AssetDatabase.GUIDToAssetPath(guid);
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                return new { success = false, error = "assetPath or guid is required" };
            }

            // Load the asset
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                return new { success = false, error = $"Asset not found: {assetPath}" };
            }

            // Find parent if specified
            Transform parent = null;
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parentGo = GameObject.Find(parentPath);
                parent = parentGo?.transform;
            }
            else if (!string.IsNullOrEmpty(parentIdStr) && int.TryParse(parentIdStr, out var parentId))
            {
                var parentGo = McpId.ToObject(parentId) as GameObject;
                parent = parentGo?.transform;
            }

            // Instantiate the asset
            GameObject instance = null;

            if (asset is GameObject prefab)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                // For non-prefab assets, create a new GameObject
                instance = new GameObject(asset.name);
            }

            if (parent != null)
            {
                instance.transform.SetParent(parent);
            }

            // Apply position if specified
            if (@params.TryGetValue("position", out var posStr))
            {
                // Parse position - simplified
                instance.transform.position = Vector3.zero;
            }

            Undo.RegisterCreatedObjectUndo(instance, "MCP Add Asset to Scene");

            return new
            {
                success = true,
                instanceId = McpId.Get(instance),
                name = instance.name,
                assetPath = assetPath
            };
        }

        private object CreatePrefab(Dictionary<string, string> @params)
        {
            var prefabName = @params.GetValueOrDefault("prefabName");
            var componentName = @params.GetValueOrDefault("componentName");

            if (string.IsNullOrEmpty(prefabName))
            {
                return new { success = false, error = "prefabName is required" };
            }

            // Ensure prefabs folder exists
            var prefabsFolder = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabsFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            var prefabPath = $"{prefabsFolder}/{prefabName}.prefab";

            // Create a temporary GameObject
            var tempGo = new GameObject(prefabName);

            // Add component if specified
            if (!string.IsNullOrEmpty(componentName))
            {
                var componentType = GetTypeByName(componentName);
                if (componentType != null)
                {
                    tempGo.AddComponent(componentType);
                }
            }

            // Create prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(tempGo, prefabPath);

            // Clean up temp object
            UnityEngine.Object.DestroyImmediate(tempGo);

            return new
            {
                success = true,
                prefabPath = prefabPath,
                prefabName = prefabName
            };
        }

        private object ManageAsset(Dictionary<string, string> @params)
        {
            var action = @params.GetValueOrDefault("action");

            if (string.IsNullOrEmpty(action))
            {
                return new { success = false, error = "action is required" };
            }

            switch (action.ToLower())
            {
                case "move":
                    return MoveAsset(@params);
                case "delete":
                    return DeleteAsset(@params);
                case "rename":
                    return RenameAsset(@params);
                case "copy":
                    return CopyAsset(@params);
                case "create_folder":
                    return CreateFolder(@params);
                case "get_path":
                    return GetAssetPath(@params);
                default:
                    return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object MoveAsset(Dictionary<string, string> @params)
        {
            var sourcePath = @params.GetValueOrDefault("sourcePath");
            var destPath = @params.GetValueOrDefault("destPath");

            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destPath))
            {
                return new { success = false, error = "sourcePath and destPath are required" };
            }

            var error = AssetDatabase.MoveAsset(sourcePath, destPath);
            if (!string.IsNullOrEmpty(error))
            {
                return new { success = false, error = error };
            }

            return new { success = true, newPath = destPath };
        }

        private object DeleteAsset(Dictionary<string, string> @params)
        {
            var assetPath = @params.GetValueOrDefault("assetPath");

            if (string.IsNullOrEmpty(assetPath))
            {
                return new { success = false, error = "assetPath is required" };
            }

            var deleted = AssetDatabase.DeleteAsset(assetPath);

            return new { success = deleted, assetPath = assetPath };
        }

        private object RenameAsset(Dictionary<string, string> @params)
        {
            var assetPath = @params.GetValueOrDefault("assetPath");
            var newName = @params.GetValueOrDefault("newName");

            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(newName))
            {
                return new { success = false, error = "assetPath and newName are required" };
            }

            var error = AssetDatabase.RenameAsset(assetPath, newName);
            if (!string.IsNullOrEmpty(error))
            {
                return new { success = false, error = error };
            }

            return new { success = true, newName = newName };
        }

        private object CopyAsset(Dictionary<string, string> @params)
        {
            var sourcePath = @params.GetValueOrDefault("sourcePath");
            var destPath = @params.GetValueOrDefault("destPath");

            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destPath))
            {
                return new { success = false, error = "sourcePath and destPath are required" };
            }

            var copied = AssetDatabase.CopyAsset(sourcePath, destPath);

            return new { success = copied, newPath = destPath };
        }

        private object CreateFolder(Dictionary<string, string> @params)
        {
            var parentFolder = @params.GetValueOrDefault("parentFolder") ?? "Assets";
            var folderName = @params.GetValueOrDefault("folderName");

            if (string.IsNullOrEmpty(folderName))
            {
                return new { success = false, error = "folderName is required" };
            }

            var guid = AssetDatabase.CreateFolder(parentFolder, folderName);

            return new
            {
                success = !string.IsNullOrEmpty(guid),
                path = $"{parentFolder}/{folderName}",
                guid = guid
            };
        }

        private object GetAssetPath(Dictionary<string, string> @params)
        {
            var guid = @params.GetValueOrDefault("guid");
            var instanceIdStr = @params.GetValueOrDefault("instanceId");
            var assetPath = @params.GetValueOrDefault("assetPath");

            if (!string.IsNullOrEmpty(guid))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                return new { success = !string.IsNullOrEmpty(path), path = path, guid = guid };
            }

            if (!string.IsNullOrEmpty(instanceIdStr) && int.TryParse(instanceIdStr, out var instanceId))
            {
                var obj = McpId.ToObject(instanceId);
                var path = AssetDatabase.GetAssetPath(obj);
                return new { success = !string.IsNullOrEmpty(path), path = path, instanceId = instanceId };
            }

            if (!string.IsNullOrEmpty(assetPath))
            {
                var foundGuid = AssetDatabase.AssetPathToGUID(assetPath);
                return new { success = !string.IsNullOrEmpty(foundGuid), path = assetPath, guid = foundGuid };
            }

            return new { success = false, error = "guid, instanceId, or assetPath is required" };
        }

        private object AddPackage(Dictionary<string, string> @params)
        {
            var source = @params.GetValueOrDefault("source");
            var packageName = @params.GetValueOrDefault("packageName");
            var version = @params.GetValueOrDefault("version");
            var repositoryUrl = @params.GetValueOrDefault("repositoryUrl");
            var path = @params.GetValueOrDefault("path");
            var branch = @params.GetValueOrDefault("branch");

            if (string.IsNullOrEmpty(source))
            {
                return new { success = false, error = "source is required (registry, github, or disk)" };
            }

            string packageId = null;

            switch (source.ToLower())
            {
                case "registry":
                    if (string.IsNullOrEmpty(packageName))
                    {
                        return new { success = false, error = "packageName is required for registry source" };
                    }
                    packageId = string.IsNullOrEmpty(version) ? packageName : $"{packageName}@{version}";
                    break;

                case "github":
                    if (string.IsNullOrEmpty(repositoryUrl))
                    {
                        return new { success = false, error = "repositoryUrl is required for github source" };
                    }
                    packageId = repositoryUrl;
                    if (!string.IsNullOrEmpty(path))
                    {
                        packageId += $"?path={path}";
                    }
                    if (!string.IsNullOrEmpty(branch))
                    {
                        packageId += packageId.Contains("?") ? $"&branch={branch}" : $"#branch={branch}";
                    }
                    break;

                case "disk":
                    if (string.IsNullOrEmpty(path))
                    {
                        return new { success = false, error = "path is required for disk source" };
                    }
                    packageId = $"file:{path}";
                    break;

                default:
                    return new { success = false, error = $"Unknown source: {source}" };
            }

            // Add package via Package Manager
            var request = UnityEditor.PackageManager.Client.Add(packageId);

            // Wait for completion (simplified - in production use async)
            while (!request.IsCompleted)
            {
                System.Threading.Thread.Sleep(100);
            }

            if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                return new
                {
                    success = true,
                    packageId = packageId,
                    packageName = request.Result?.name,
                    version = request.Result?.version
                };
            }
            else
            {
                return new
                {
                    success = false,
                    error = request.Error?.message ?? "Failed to add package"
                };
            }
        }

        private Type GetTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName) ?? assembly.GetType($"UnityEngine.{typeName}");
                if (type != null) return type;
            }
            return null;
        }
    }
}
