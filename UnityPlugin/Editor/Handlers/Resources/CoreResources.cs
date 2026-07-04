using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers.Resources
{
    /// <summary>
    /// Handles unity://scenes_hierarchy resource
    /// </summary>
    public class SceneHierarchyResource : IResourceHandler
    {
        public string[] SupportedUris => new[] { "unity://scenes_hierarchy" };

        public object Handle(string uri, string paramsJson)
        {
            var scenes = new List<object>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                var rootObjects = scene.GetRootGameObjects();
                var gameObjects = new List<object>();

                foreach (var root in rootObjects)
                {
                    gameObjects.Add(GetGameObjectHierarchy(root, 0, 3)); // Max depth of 3 for hierarchy
                }

                scenes.Add(new
                {
                    name = scene.name,
                    path = scene.path,
                    isLoaded = scene.isLoaded,
                    isDirty = scene.isDirty,
                    rootCount = rootObjects.Length,
                    gameObjects = gameObjects
                });
            }

            return new
            {
                success = true,
                sceneCount = scenes.Count,
                scenes = scenes
            };
        }

        private object GetGameObjectHierarchy(GameObject go, int currentDepth, int maxDepth)
        {
            var children = new List<object>();

            if (currentDepth < maxDepth)
            {
                foreach (Transform child in go.transform)
                {
                    children.Add(GetGameObjectHierarchy(child.gameObject, currentDepth + 1, maxDepth));
                }
            }
            else if (go.transform.childCount > 0)
            {
                children = null; // Indicate there are more children but not included
            }

            return new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                activeSelf = go.activeSelf,
                childCount = go.transform.childCount,
                children = children
            };
        }
    }

    /// <summary>
    /// Handles unity://gameobject/{id} resource
    /// </summary>
    public class GameObjectResource : IResourceHandler
    {
        public string[] SupportedUris => new[] { "unity://gameobject/{id}" };

        public object Handle(string uri, string paramsJson)
        {
            // Extract ID from URI
            var parts = uri.Split('/');
            var idStr = parts.Last();

            if (!int.TryParse(idStr, out var instanceId))
            {
                return new { success = false, error = "Invalid instance ID" };
            }

            var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (go == null)
            {
                return new { success = false, error = "GameObject not found" };
            }

            return new
            {
                success = true,
                gameObject = new
                {
                    instanceId = go.GetInstanceID(),
                    name = go.name,
                    tag = go.tag,
                    layer = go.layer,
                    activeSelf = go.activeSelf,
                    activeInHierarchy = go.activeInHierarchy,
                    isStatic = go.isStatic,
                    transform = new
                    {
                        position = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z },
                        rotation = new { x = go.transform.rotation.eulerAngles.x, y = go.transform.rotation.eulerAngles.y, z = go.transform.rotation.eulerAngles.z },
                        scale = new { x = go.transform.localScale.x, y = go.transform.localScale.y, z = go.transform.localScale.z }
                    },
                    components = go.GetComponents<Component>().Select(c => new
                    {
                        type = c.GetType().Name,
                        fullType = c.GetType().FullName,
                        enabled = (c is Behaviour b) ? b.enabled : true
                    }).ToList(),
                    childCount = go.transform.childCount,
                    parent = go.transform.parent != null ? new
                    {
                        instanceId = go.transform.parent.gameObject.GetInstanceID(),
                        name = go.transform.parent.name
                    } : null
                }
            };
        }
    }

    /// <summary>
    /// Handles unity://logs/{type} resource
    /// </summary>
    public class ConsoleLogsResource : IResourceHandler
    {
        public string[] SupportedUris => new[] { "unity://logs/{type}", "unity://logs" };

        public object Handle(string uri, string paramsJson)
        {
            // This would integrate with ConsoleHandler's log cache
            // For now return a placeholder
            return new
            {
                success = true,
                note = "Use get_console_logs tool for log retrieval"
            };
        }
    }

    /// <summary>
    /// Handles unity://assets resource
    /// </summary>
    public class AssetsResource : IResourceHandler
    {
        public string[] SupportedUris => new[] { "unity://assets" };

        public object Handle(string uri, string paramsJson)
        {
            var folders = new[] { "Assets/Scenes", "Assets/Prefabs", "Assets/Materials", "Assets/Scripts" };
            var assetsByFolder = new Dictionary<string, List<object>>();

            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                var guids = AssetDatabase.FindAssets("", new[] { folder });
                var assets = new List<object>();

                foreach (var guid in guids.Take(50)) // Limit per folder
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset != null)
                    {
                        assets.Add(new
                        {
                            guid = guid,
                            path = path,
                            name = asset.name,
                            type = asset.GetType().Name
                        });
                    }
                }

                assetsByFolder[folder] = assets;
            }

            return new
            {
                success = true,
                folders = assetsByFolder
            };
        }
    }

    /// <summary>
    /// Handles unity://packages resource
    /// </summary>
    public class PackagesResource : IResourceHandler
    {
        public string[] SupportedUris => new[] { "unity://packages" };

        public object Handle(string uri, string paramsJson)
        {
            var request = UnityEditor.PackageManager.Client.List(true);
            while (!request.IsCompleted)
            {
                System.Threading.Thread.Sleep(10);
            }

            var packages = new List<object>();

            if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                foreach (var package in request.Result)
                {
                    packages.Add(new
                    {
                        name = package.name,
                        displayName = package.displayName,
                        version = package.version,
                        source = package.source.ToString(),
                        resolvedPath = package.resolvedPath
                    });
                }
            }

            return new
            {
                success = request.Status == UnityEditor.PackageManager.StatusCode.Success,
                packageCount = packages.Count,
                packages = packages
            };
        }
    }
}
