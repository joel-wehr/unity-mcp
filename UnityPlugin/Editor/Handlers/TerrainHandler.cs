using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles Unity Terrain creation, inspection, and modification.
    /// </summary>
    public class TerrainHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "terrain" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "create": return CreateTerrain(p);
                case "get_info": return GetTerrainInfo(p);
                case "set_height": return SetHeight(p);
                case "get_height": return GetHeight(p);
                case "flatten": return Flatten(p);
                case "set_detail": return SetDetail(p);
                case "paint_texture": return PaintTexture(p);
                case "add_tree": return AddTree(p);
                case "get_layers": return GetTerrainLayers(p);
                case "add_layer": return AddTerrainLayer(p);
                case "set_settings": return SetSettings(p);
                case "list": return ListTerrains();
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private Terrain FindTerrain(Dictionary<string, string> p)
        {
            var terrainName = p.GetValueOrDefault("terrainName");
            if (!string.IsNullOrEmpty(terrainName))
            {
                var go = GameObject.Find(terrainName);
                if (go != null) return go.GetComponent<Terrain>();
            }

            var terrainId = p.GetValueOrDefault("terrainId");
            if (!string.IsNullOrEmpty(terrainId) && int.TryParse(terrainId, out var id))
            {
                var obj = McpId.ToObject(id) as GameObject;
                if (obj != null) return obj.GetComponent<Terrain>();
            }

            // Return first terrain
            return Terrain.activeTerrain;
        }

        private object CreateTerrain(Dictionary<string, string> p)
        {
            var name = p.GetValueOrDefault("terrainName") ?? "Terrain";
            var widthStr = p.GetValueOrDefault("width") ?? "500";
            var heightStr = p.GetValueOrDefault("height") ?? "600";
            var lengthStr = p.GetValueOrDefault("length") ?? "500";
            var resStr = p.GetValueOrDefault("heightmapResolution") ?? "513";

            float.TryParse(widthStr, out var width);
            float.TryParse(heightStr, out var height);
            float.TryParse(lengthStr, out var length);
            int.TryParse(resStr, out var res);

            if (width <= 0) width = 500;
            if (height <= 0) height = 600;
            if (length <= 0) length = 500;
            if (res < 33) res = 513;

            var terrainData = new TerrainData();
            terrainData.heightmapResolution = res;
            terrainData.size = new Vector3(width, height, length);

            var assetPath = p.GetValueOrDefault("assetPath") ?? $"Assets/{name}_Data.asset";
            if (!assetPath.EndsWith(".asset")) assetPath += ".asset";
            AssetDatabase.CreateAsset(terrainData, assetPath);

            var go = Terrain.CreateTerrainGameObject(terrainData);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, $"Create Terrain {name}");

            return new
            {
                success = true,
                instanceId = McpId.Get(go),
                name = go.name,
                dataPath = assetPath,
                size = new { x = width, y = height, z = length },
                heightmapResolution = res
            };
        }

        private object GetTerrainInfo(Dictionary<string, string> p)
        {
            var terrain = FindTerrain(p);
            if (terrain == null)
                return new { success = false, error = "No terrain found" };

            var data = terrain.terrainData;
            return new
            {
                success = true,
                name = terrain.name,
                instanceId = McpId.Get(terrain.gameObject),
                position = new { x = terrain.transform.position.x, y = terrain.transform.position.y, z = terrain.transform.position.z },
                size = new { x = data.size.x, y = data.size.y, z = data.size.z },
                heightmapResolution = data.heightmapResolution,
                alphamapResolution = data.alphamapResolution,
                detailResolution = data.detailResolution,
                terrainLayerCount = data.terrainLayers?.Length ?? 0,
                treeInstanceCount = data.treeInstanceCount,
                treePrototypeCount = data.treePrototypes.Length,
                detailPrototypeCount = data.detailPrototypes.Length,
                drawHeightmap = terrain.drawHeightmap,
                drawTreesAndFoliage = terrain.drawTreesAndFoliage,
                basemapDistance = terrain.basemapDistance,
                materialType = terrain.materialTemplate?.name ?? "default"
            };
        }

        private object SetHeight(Dictionary<string, string> p)
        {
            var terrain = FindTerrain(p);
            if (terrain == null)
                return new { success = false, error = "No terrain found" };

            var xStr = p.GetValueOrDefault("x") ?? "0";
            var zStr = p.GetValueOrDefault("z") ?? "0";
            var heightStr = p.GetValueOrDefault("height") ?? "0";
            var radiusStr = p.GetValueOrDefault("radius") ?? "1";

            float.TryParse(xStr, out var x);
            float.TryParse(zStr, out var z);
            float.TryParse(heightStr, out var h);
            int.TryParse(radiusStr, out var radius);
            if (radius < 1) radius = 1;

            var data = terrain.terrainData;
            var res = data.heightmapResolution;

            // Convert world position to heightmap coordinates
            var pos = terrain.transform.position;
            var size = data.size;
            var hmX = Mathf.RoundToInt((x - pos.x) / size.x * (res - 1));
            var hmZ = Mathf.RoundToInt((z - pos.z) / size.z * (res - 1));
            var normalizedHeight = h / size.y;

            // Apply height in radius
            var startX = Mathf.Max(0, hmX - radius);
            var startZ = Mathf.Max(0, hmZ - radius);
            var endX = Mathf.Min(res - 1, hmX + radius);
            var endZ = Mathf.Min(res - 1, hmZ + radius);
            var width = endX - startX + 1;
            var heightCount = endZ - startZ + 1;

            Undo.RegisterCompleteObjectUndo(data, "Set Terrain Height");
            var heights = data.GetHeights(startX, startZ, width, heightCount);

            for (int iz = 0; iz < heightCount; iz++)
            {
                for (int ix = 0; ix < width; ix++)
                {
                    var dist = Vector2.Distance(new Vector2(startX + ix, startZ + iz), new Vector2(hmX, hmZ));
                    if (dist <= radius)
                    {
                        var falloff = 1f - (dist / radius);
                        heights[iz, ix] = Mathf.Lerp(heights[iz, ix], normalizedHeight, falloff);
                    }
                }
            }

            data.SetHeights(startX, startZ, heights);
            return new { success = true, message = $"Set height at ({x},{z}) to {h} with radius {radius}" };
        }

        private object GetHeight(Dictionary<string, string> p)
        {
            var terrain = FindTerrain(p);
            if (terrain == null)
                return new { success = false, error = "No terrain found" };

            var xStr = p.GetValueOrDefault("x") ?? "0";
            var zStr = p.GetValueOrDefault("z") ?? "0";
            float.TryParse(xStr, out var x);
            float.TryParse(zStr, out var z);

            var worldHeight = terrain.SampleHeight(new Vector3(x, 0, z));
            return new { success = true, x = x, z = z, height = worldHeight };
        }

        private object Flatten(Dictionary<string, string> p)
        {
            var terrain = FindTerrain(p);
            if (terrain == null)
                return new { success = false, error = "No terrain found" };

            var heightStr = p.GetValueOrDefault("height") ?? "0";
            float.TryParse(heightStr, out var height);

            var data = terrain.terrainData;
            var normalizedHeight = height / data.size.y;
            var res = data.heightmapResolution;

            Undo.RegisterCompleteObjectUndo(data, "Flatten Terrain");
            var heights = new float[res, res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    heights[z, x] = normalizedHeight;

            data.SetHeights(0, 0, heights);
            return new { success = true, message = $"Flattened terrain to height {height}" };
        }

        private object SetDetail(Dictionary<string, string> p)
        {
            var terrain = FindTerrain(p);
            if (terrain == null)
                return new { success = false, error = "No terrain found" };

            var data = terrain.terrainData;
            if (data.detailPrototypes.Length == 0)
                return new { success = false, error = "No detail prototypes defined on terrain" };

            var layerStr = p.GetValueOrDefault("layer") ?? "0";
            int.TryParse(layerStr, out var layer);
            if (layer < 0 || layer >= data.detailPrototypes.Length)
                return new { success = false, error = $"Detail layer {layer} out of range (0-{data.detailPrototypes.Length - 1})" };

            var xStr = p.GetValueOrDefault("x") ?? "0";
            var zStr = p.GetValueOrDefault("z") ?? "0";
            var densityStr = p.GetValueOrDefault("density") ?? "1";
            var radiusStr = p.GetValueOrDefault("radius") ?? "5";

            int.TryParse(xStr, out var x);
            int.TryParse(zStr, out var z);
            int.TryParse(densityStr, out var density);
            int.TryParse(radiusStr, out var radius);

            var detailRes = data.detailResolution;
            var details = data.GetDetailLayer(0, 0, detailRes, detailRes, layer);

            var startX = Mathf.Max(0, x - radius);
            var startZ = Mathf.Max(0, z - radius);
            var endX = Mathf.Min(detailRes - 1, x + radius);
            var endZ = Mathf.Min(detailRes - 1, z + radius);

            Undo.RegisterCompleteObjectUndo(data, "Set Terrain Detail");
            for (int iz = startZ; iz <= endZ; iz++)
                for (int ix = startX; ix <= endX; ix++)
                    details[iz, ix] = density;

            data.SetDetailLayer(0, 0, layer, details);
            return new { success = true, message = $"Set detail layer {layer} density {density} at ({x},{z}) radius {radius}" };
        }

        private object PaintTexture(Dictionary<string, string> p)
        {
            var terrain = FindTerrain(p);
            if (terrain == null)
                return new { success = false, error = "No terrain found" };

            var data = terrain.terrainData;
            if (data.terrainLayers == null || data.terrainLayers.Length == 0)
                return new { success = false, error = "No terrain layers defined" };

            var layerStr = p.GetValueOrDefault("layer") ?? "0";
            int.TryParse(layerStr, out var layer);
            if (layer < 0 || layer >= data.terrainLayers.Length)
                return new { success = false, error = $"Layer {layer} out of range (0-{data.terrainLayers.Length - 1})" };

            var xStr = p.GetValueOrDefault("x") ?? "0";
            var zStr = p.GetValueOrDefault("z") ?? "0";
            var radiusStr = p.GetValueOrDefault("radius") ?? "5";
            var strengthStr = p.GetValueOrDefault("strength") ?? "1";

            int.TryParse(xStr, out var x);
            int.TryParse(zStr, out var z);
            int.TryParse(radiusStr, out var radius);
            float.TryParse(strengthStr, out var strength);
            strength = Mathf.Clamp01(strength);

            var alphaRes = data.alphamapResolution;
            var layerCount = data.terrainLayers.Length;
            var alphamaps = data.GetAlphamaps(0, 0, alphaRes, alphaRes);

            var startX = Mathf.Max(0, x - radius);
            var startZ = Mathf.Max(0, z - radius);
            var endX = Mathf.Min(alphaRes - 1, x + radius);
            var endZ = Mathf.Min(alphaRes - 1, z + radius);

            Undo.RegisterCompleteObjectUndo(data, "Paint Terrain Texture");
            for (int iz = startZ; iz <= endZ; iz++)
            {
                for (int ix = startX; ix <= endX; ix++)
                {
                    var dist = Vector2.Distance(new Vector2(ix, iz), new Vector2(x, z));
                    if (dist <= radius)
                    {
                        var falloff = (1f - dist / radius) * strength;
                        for (int l = 0; l < layerCount; l++)
                        {
                            if (l == layer)
                                alphamaps[iz, ix, l] = Mathf.Lerp(alphamaps[iz, ix, l], 1f, falloff);
                            else
                                alphamaps[iz, ix, l] = Mathf.Lerp(alphamaps[iz, ix, l], 0f, falloff);
                        }
                    }
                }
            }

            data.SetAlphamaps(0, 0, alphamaps);
            return new { success = true, message = $"Painted layer {layer} at ({x},{z}) radius {radius} strength {strength}" };
        }

        private object AddTree(Dictionary<string, string> p)
        {
            var terrain = FindTerrain(p);
            if (terrain == null)
                return new { success = false, error = "No terrain found" };

            var data = terrain.terrainData;
            if (data.treePrototypes.Length == 0)
                return new { success = false, error = "No tree prototypes defined on terrain" };

            var protoStr = p.GetValueOrDefault("prototypeIndex") ?? "0";
            int.TryParse(protoStr, out var protoIndex);
            if (protoIndex < 0 || protoIndex >= data.treePrototypes.Length)
                return new { success = false, error = $"Prototype {protoIndex} out of range (0-{data.treePrototypes.Length - 1})" };

            var xStr = p.GetValueOrDefault("x") ?? "0.5";
            var zStr = p.GetValueOrDefault("z") ?? "0.5";
            var scaleStr = p.GetValueOrDefault("scale") ?? "1";
            float.TryParse(xStr, out var x);
            float.TryParse(zStr, out var z);
            float.TryParse(scaleStr, out var scale);

            Undo.RegisterCompleteObjectUndo(data, "Add Tree");
            var instance = new TreeInstance
            {
                position = new Vector3(x, 0, z), // normalized 0-1 coords
                prototypeIndex = protoIndex,
                widthScale = scale,
                heightScale = scale,
                color = Color.white,
                lightmapColor = Color.white,
                rotation = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
            };

            var trees = new List<TreeInstance>(data.treeInstances);
            trees.Add(instance);
            data.treeInstances = trees.ToArray();

            return new { success = true, message = $"Added tree prototype {protoIndex} at ({x},{z}) scale {scale}", treeCount = data.treeInstanceCount };
        }

        private object GetTerrainLayers(Dictionary<string, string> p)
        {
            var terrain = FindTerrain(p);
            if (terrain == null)
                return new { success = false, error = "No terrain found" };

            var layers = terrain.terrainData.terrainLayers;
            var list = layers?.Select((l, i) => new
            {
                index = i,
                name = l?.name ?? "null",
                diffuseTexture = l?.diffuseTexture?.name ?? "none",
                normalMapTexture = l?.normalMapTexture?.name ?? "none",
                tileSize = l != null ? new { x = l.tileSize.x, y = l.tileSize.y } : null,
                tileOffset = l != null ? new { x = l.tileOffset.x, y = l.tileOffset.y } : null
            }).ToList();

            return new { success = true, count = list?.Count ?? 0, layers = list };
        }

        private object AddTerrainLayer(Dictionary<string, string> p)
        {
            var terrain = FindTerrain(p);
            if (terrain == null)
                return new { success = false, error = "No terrain found" };

            var texturePath = p.GetValueOrDefault("texturePath");
            if (string.IsNullOrEmpty(texturePath))
                return new { success = false, error = "texturePath is required" };

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
                return new { success = false, error = $"Texture not found at: {texturePath}" };

            var tileSizeStr = p.GetValueOrDefault("tileSize") ?? "15";
            float.TryParse(tileSizeStr, out var tileSize);
            if (tileSize <= 0) tileSize = 15;

            var newLayer = new TerrainLayer
            {
                diffuseTexture = texture,
                tileSize = new Vector2(tileSize, tileSize)
            };

            var layerPath = p.GetValueOrDefault("layerPath") ?? $"Assets/TerrainLayer_{texture.name}.terrainlayer";
            AssetDatabase.CreateAsset(newLayer, layerPath);

            var data = terrain.terrainData;
            var layers = data.terrainLayers != null ? new List<TerrainLayer>(data.terrainLayers) : new List<TerrainLayer>();
            layers.Add(newLayer);
            data.terrainLayers = layers.ToArray();

            return new { success = true, layerIndex = layers.Count - 1, layerPath = layerPath, message = $"Added terrain layer with texture '{texture.name}'" };
        }

        private object SetSettings(Dictionary<string, string> p)
        {
            var terrain = FindTerrain(p);
            if (terrain == null)
                return new { success = false, error = "No terrain found" };

            var changed = new List<string>();

            if (p.TryGetValue("drawHeightmap", out var dh))
            { terrain.drawHeightmap = dh.ToLower() == "true"; changed.Add("drawHeightmap"); }

            if (p.TryGetValue("drawTreesAndFoliage", out var dt))
            { terrain.drawTreesAndFoliage = dt.ToLower() == "true"; changed.Add("drawTreesAndFoliage"); }

            if (p.TryGetValue("basemapDistance", out var bd) && float.TryParse(bd, out var bdf))
            { terrain.basemapDistance = bdf; changed.Add("basemapDistance"); }

            if (p.TryGetValue("detailObjectDistance", out var dod) && float.TryParse(dod, out var dodf))
            { terrain.detailObjectDistance = dodf; changed.Add("detailObjectDistance"); }

            if (p.TryGetValue("treeDistance", out var td) && float.TryParse(td, out var tdf))
            { terrain.treeDistance = tdf; changed.Add("treeDistance"); }

            if (p.TryGetValue("treeBillboardDistance", out var tbd) && float.TryParse(tbd, out var tbdf))
            { terrain.treeBillboardDistance = tbdf; changed.Add("treeBillboardDistance"); }

            if (p.TryGetValue("heightmapPixelError", out var hpe) && float.TryParse(hpe, out var hpef))
            { terrain.heightmapPixelError = hpef; changed.Add("heightmapPixelError"); }

            EditorUtility.SetDirty(terrain);
            return new { success = true, changed = changed.ToArray(), message = $"Updated {changed.Count} terrain settings" };
        }

        private object ListTerrains()
        {
            var terrains = Terrain.activeTerrains;
            var list = terrains.Select(t => new
            {
                name = t.name,
                instanceId = McpId.Get(t.gameObject),
                position = new { x = t.transform.position.x, y = t.transform.position.y, z = t.transform.position.z },
                size = new { x = t.terrainData.size.x, y = t.terrainData.size.y, z = t.terrainData.size.z }
            }).ToList();

            return new { success = true, count = list.Count, terrains = list };
        }
    }
}
