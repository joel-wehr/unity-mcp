using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Advanced prefab operations: variants, overrides, nesting, unpacking.
    /// Complements the existing create_prefab tool with richer prefab workflow.
    /// </summary>
    public class PrefabHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "prefab" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "get_info": return GetPrefabInfo(p);
                case "create_variant": return CreateVariant(p);
                case "get_overrides": return GetOverrides(p);
                case "apply_overrides": return ApplyOverrides(p);
                case "revert_overrides": return RevertOverrides(p);
                case "unpack": return Unpack(p);
                case "open": return OpenPrefab(p);
                case "close": return ClosePrefab();
                case "instantiate": return InstantiatePrefab(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private GameObject FindGO(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("objectPath");
            var id = p.GetValueOrDefault("objectId");
            if (!string.IsNullOrEmpty(path)) return GameObject.Find(path);
            if (!string.IsNullOrEmpty(id) && int.TryParse(id, out var iid))
                return McpId.ToObject(iid) as GameObject;
            return Selection.activeGameObject;
        }

        private object GetPrefabInfo(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            var assetPath = p.GetValueOrDefault("assetPath");

            if (go == null && string.IsNullOrEmpty(assetPath))
                return new { success = false, error = "objectPath/objectId or assetPath required" };

            if (go != null)
            {
                var status = PrefabUtility.GetPrefabInstanceStatus(go);
                var type = PrefabUtility.GetPrefabAssetType(go);
                var sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                var isPartOfPrefab = PrefabUtility.IsPartOfAnyPrefab(go);
                var isVariant = PrefabUtility.IsPartOfVariantPrefab(go);

                return new
                {
                    success = true,
                    instanceStatus = status.ToString(),
                    assetType = type.ToString(),
                    sourcePrefabPath = sourcePath,
                    isPartOfPrefab = isPartOfPrefab,
                    isVariant = isVariant,
                    hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(go, false)
                };
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                    return new { success = false, error = $"Prefab not found at: {assetPath}" };

                var type = PrefabUtility.GetPrefabAssetType(prefab);
                var isVariant = PrefabUtility.IsPartOfVariantPrefab(prefab);

                return new
                {
                    success = true,
                    assetType = type.ToString(),
                    isVariant = isVariant,
                    path = assetPath,
                    name = prefab.name,
                    childCount = prefab.transform.childCount,
                    components = prefab.GetComponents<Component>().Select(c => c.GetType().Name).ToArray()
                };
            }
        }

        private object CreateVariant(Dictionary<string, string> p)
        {
            var basePrefabPath = p.GetValueOrDefault("assetPath");
            var variantPath = p.GetValueOrDefault("variantPath");

            if (string.IsNullOrEmpty(basePrefabPath))
                return new { success = false, error = "assetPath (base prefab) is required" };
            if (string.IsNullOrEmpty(variantPath))
                return new { success = false, error = "variantPath is required" };

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
            if (basePrefab == null)
                return new { success = false, error = $"Base prefab not found at: {basePrefabPath}" };

            // Instantiate, then save as variant
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            if (!variantPath.EndsWith(".prefab")) variantPath += ".prefab";

            var variant = PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            UnityEngine.Object.DestroyImmediate(instance);

            if (variant == null)
                return new { success = false, error = $"Failed to create variant at: {variantPath}" };

            return new
            {
                success = true,
                basePrefab = basePrefabPath,
                variantPath = variantPath,
                message = $"Created prefab variant at {variantPath}"
            };
        }

        private object GetOverrides(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return new { success = false, error = "Object is not a prefab instance" };

            var modifications = PrefabUtility.GetPropertyModifications(go);
            var addedObjects = PrefabUtility.GetAddedComponents(go);
            var removedObjects = PrefabUtility.GetRemovedComponents(go);
            var addedGOs = PrefabUtility.GetAddedGameObjects(go);

            var mods = modifications?.Select(m => new
            {
                target = m.target?.name ?? "null",
                propertyPath = m.propertyPath,
                value = m.value
            }).Take(50).ToList();

            return new
            {
                success = true,
                propertyModifications = mods?.Count ?? 0,
                addedComponents = addedObjects.Count,
                removedComponents = removedObjects.Count,
                addedGameObjects = addedGOs.Count,
                modifications = mods
            };
        }

        private object ApplyOverrides(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return new { success = false, error = "Object is not a prefab instance" };

            var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            PrefabUtility.ApplyPrefabInstance(prefabRoot, InteractionMode.AutomatedAction);

            return new { success = true, message = "Applied all overrides to prefab asset" };
        }

        private object RevertOverrides(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return new { success = false, error = "Object is not a prefab instance" };

            PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);
            return new { success = true, message = "Reverted all overrides" };
        }

        private object Unpack(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return new { success = false, error = "Object is not a prefab instance" };

            var completely = p.GetValueOrDefault("completely")?.ToLower() == "true";
            if (completely)
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            else
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            return new { success = true, message = $"Unpacked prefab {(completely ? "completely" : "outermost root")}" };
        }

        private object OpenPrefab(Dictionary<string, string> p)
        {
            var assetPath = p.GetValueOrDefault("assetPath");
            if (string.IsNullOrEmpty(assetPath))
            {
                var go = FindGO(p);
                if (go != null)
                    assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            }

            if (string.IsNullOrEmpty(assetPath))
                return new { success = false, error = "assetPath or objectPath/objectId required" };

            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));
            return new { success = true, message = $"Opened prefab: {assetPath}" };
        }

        private object ClosePrefab()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return new { success = true, message = "No prefab stage open" };

            UnityEditor.SceneManagement.StageUtility.GoToMainStage();
            return new { success = true, message = "Closed prefab stage, returned to main stage" };
        }

        private object InstantiatePrefab(Dictionary<string, string> p)
        {
            var assetPath = p.GetValueOrDefault("assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { success = false, error = "assetPath is required" };

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                return new { success = false, error = $"Prefab not found at: {assetPath}" };

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            // Parse optional position
            var posJson = p.GetValueOrDefault("position");
            if (!string.IsNullOrEmpty(posJson))
            {
                var pos = JsonRpcParamsParser.ParseToDictionary(posJson);
                float.TryParse(pos.GetValueOrDefault("x") ?? "0", out var x);
                float.TryParse(pos.GetValueOrDefault("y") ?? "0", out var y);
                float.TryParse(pos.GetValueOrDefault("z") ?? "0", out var z);
                instance.transform.position = new Vector3(x, y, z);
            }

            // Parse optional parent
            var parentPath = p.GetValueOrDefault("parentPath");
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = GameObject.Find(parentPath);
                if (parent != null) instance.transform.SetParent(parent.transform);
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {prefab.name}");

            return new
            {
                success = true,
                instanceId = McpId.Get(instance),
                name = instance.name,
                message = $"Instantiated prefab '{prefab.name}'"
            };
        }
    }
}
