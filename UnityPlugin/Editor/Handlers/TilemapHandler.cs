using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles Unity Tilemap operations: create, paint, erase, inspect.
    /// </summary>
    public class TilemapHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "tilemap" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "create": return CreateTilemap(p);
                case "get_info": return GetTilemapInfo(p);
                case "set_tile": return SetTile(p);
                case "erase_tile": return EraseTile(p);
                case "fill_area": return FillArea(p);
                case "clear": return ClearTilemap(p);
                case "get_tile": return GetTileAt(p);
                case "get_bounds": return GetBounds(p);
                case "list": return ListTilemaps();
                case "list_tiles": return ListTileAssets(p);
                case "create_tile": return CreateTileAsset(p);
                case "set_color": return SetTileColor(p);
                case "compress_bounds": return CompressBounds(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private Tilemap FindTilemap(Dictionary<string, string> p)
        {
            var tilemapName = p.GetValueOrDefault("tilemapName");
            if (!string.IsNullOrEmpty(tilemapName))
            {
                var go = GameObject.Find(tilemapName);
                if (go != null) return go.GetComponent<Tilemap>();
            }

            var tilemapId = p.GetValueOrDefault("tilemapId");
            if (!string.IsNullOrEmpty(tilemapId) && int.TryParse(tilemapId, out var id))
            {
                var obj = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (obj != null) return obj.GetComponent<Tilemap>();
            }

            // Return first tilemap found
            return UnityEngine.Object.FindFirstObjectByType<Tilemap>();
        }

        private object CreateTilemap(Dictionary<string, string> p)
        {
            var name = p.GetValueOrDefault("name") ?? "Tilemap";
            var parentPath = p.GetValueOrDefault("parentPath");
            var sortingOrder = 0;
            int.TryParse(p.GetValueOrDefault("sortingOrder") ?? "0", out sortingOrder);

            // Find or create Grid parent
            GameObject gridGO = null;
            if (!string.IsNullOrEmpty(parentPath))
                gridGO = GameObject.Find(parentPath);

            if (gridGO == null)
            {
                // Check if a Grid exists
                var existingGrid = UnityEngine.Object.FindFirstObjectByType<Grid>();
                if (existingGrid != null)
                    gridGO = existingGrid.gameObject;
                else
                {
                    gridGO = new GameObject("Grid");
                    gridGO.AddComponent<Grid>();
                    Undo.RegisterCreatedObjectUndo(gridGO, "Create Grid");
                }
            }

            var tilemapGO = new GameObject(name);
            tilemapGO.transform.SetParent(gridGO.transform);
            var tilemap = tilemapGO.AddComponent<Tilemap>();
            var renderer = tilemapGO.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            Undo.RegisterCreatedObjectUndo(tilemapGO, $"Create Tilemap {name}");

            return new
            {
                success = true,
                instanceId = tilemapGO.GetInstanceID(),
                name = tilemapGO.name,
                gridName = gridGO.name,
                message = $"Created tilemap '{name}' under grid '{gridGO.name}'"
            };
        }

        private object GetTilemapInfo(Dictionary<string, string> p)
        {
            var tilemap = FindTilemap(p);
            if (tilemap == null)
                return new { success = false, error = "No Tilemap found" };

            var bounds = tilemap.cellBounds;
            var renderer = tilemap.GetComponent<TilemapRenderer>();

            return new
            {
                success = true,
                name = tilemap.gameObject.name,
                instanceId = tilemap.gameObject.GetInstanceID(),
                cellSize = new { x = tilemap.cellSize.x, y = tilemap.cellSize.y, z = tilemap.cellSize.z },
                boundsMin = new { x = bounds.xMin, y = bounds.yMin },
                boundsMax = new { x = bounds.xMax, y = bounds.yMax },
                boundsSize = new { x = bounds.size.x, y = bounds.size.y },
                tileCount = CountTiles(tilemap),
                sortingOrder = renderer?.sortingOrder ?? 0,
                sortingLayerName = renderer?.sortingLayerName ?? "Default",
                color = $"({tilemap.color.r:F2}, {tilemap.color.g:F2}, {tilemap.color.b:F2}, {tilemap.color.a:F2})",
                orientation = tilemap.orientation.ToString()
            };
        }

        private int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            var bounds = tilemap.cellBounds;
            foreach (var pos in bounds.allPositionsWithin)
            {
                if (tilemap.HasTile(pos)) count++;
            }
            return count;
        }

        private object SetTile(Dictionary<string, string> p)
        {
            var tilemap = FindTilemap(p);
            if (tilemap == null)
                return new { success = false, error = "No Tilemap found" };

            var tilePath = p.GetValueOrDefault("tilePath");
            if (string.IsNullOrEmpty(tilePath))
                return new { success = false, error = "tilePath is required (path to a Tile asset)" };

            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
            if (tile == null)
                return new { success = false, error = $"Tile asset not found at: {tilePath}" };

            int.TryParse(p.GetValueOrDefault("x") ?? "0", out var x);
            int.TryParse(p.GetValueOrDefault("y") ?? "0", out var y);

            Undo.RecordObject(tilemap, "Set Tile");
            tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            EditorUtility.SetDirty(tilemap);

            return new { success = true, message = $"Set tile at ({x},{y}) to '{tile.name}'" };
        }

        private object EraseTile(Dictionary<string, string> p)
        {
            var tilemap = FindTilemap(p);
            if (tilemap == null)
                return new { success = false, error = "No Tilemap found" };

            int.TryParse(p.GetValueOrDefault("x") ?? "0", out var x);
            int.TryParse(p.GetValueOrDefault("y") ?? "0", out var y);

            Undo.RecordObject(tilemap, "Erase Tile");
            tilemap.SetTile(new Vector3Int(x, y, 0), null);
            EditorUtility.SetDirty(tilemap);

            return new { success = true, message = $"Erased tile at ({x},{y})" };
        }

        private object FillArea(Dictionary<string, string> p)
        {
            var tilemap = FindTilemap(p);
            if (tilemap == null)
                return new { success = false, error = "No Tilemap found" };

            var tilePath = p.GetValueOrDefault("tilePath");
            if (string.IsNullOrEmpty(tilePath))
                return new { success = false, error = "tilePath is required" };

            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
            if (tile == null)
                return new { success = false, error = $"Tile asset not found at: {tilePath}" };

            int.TryParse(p.GetValueOrDefault("startX") ?? "0", out var sx);
            int.TryParse(p.GetValueOrDefault("startY") ?? "0", out var sy);
            int.TryParse(p.GetValueOrDefault("endX") ?? "10", out var ex);
            int.TryParse(p.GetValueOrDefault("endY") ?? "10", out var ey);

            var minX = Mathf.Min(sx, ex);
            var maxX = Mathf.Max(sx, ex);
            var minY = Mathf.Min(sy, ey);
            var maxY = Mathf.Max(sy, ey);
            var count = 0;

            Undo.RecordObject(tilemap, "Fill Area");
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                    count++;
                }
            }
            EditorUtility.SetDirty(tilemap);

            return new { success = true, tilesPlaced = count, message = $"Filled area ({minX},{minY}) to ({maxX},{maxY}) with '{tile.name}'" };
        }

        private object ClearTilemap(Dictionary<string, string> p)
        {
            var tilemap = FindTilemap(p);
            if (tilemap == null)
                return new { success = false, error = "No Tilemap found" };

            Undo.RecordObject(tilemap, "Clear Tilemap");
            tilemap.ClearAllTiles();
            EditorUtility.SetDirty(tilemap);

            return new { success = true, message = $"Cleared all tiles from '{tilemap.gameObject.name}'" };
        }

        private object GetTileAt(Dictionary<string, string> p)
        {
            var tilemap = FindTilemap(p);
            if (tilemap == null)
                return new { success = false, error = "No Tilemap found" };

            int.TryParse(p.GetValueOrDefault("x") ?? "0", out var x);
            int.TryParse(p.GetValueOrDefault("y") ?? "0", out var y);

            var pos = new Vector3Int(x, y, 0);
            var tile = tilemap.GetTile(pos);
            var sprite = tilemap.GetSprite(pos);
            var color = tilemap.GetColor(pos);
            var flags = tilemap.GetTileFlags(pos);

            return new
            {
                success = true,
                x = x,
                y = y,
                hasTile = tile != null,
                tileName = tile?.name ?? "none",
                spriteName = sprite?.name ?? "none",
                color = $"({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})",
                flags = flags.ToString()
            };
        }

        private object GetBounds(Dictionary<string, string> p)
        {
            var tilemap = FindTilemap(p);
            if (tilemap == null)
                return new { success = false, error = "No Tilemap found" };

            var bounds = tilemap.cellBounds;
            return new
            {
                success = true,
                min = new { x = bounds.xMin, y = bounds.yMin },
                max = new { x = bounds.xMax, y = bounds.yMax },
                size = new { x = bounds.size.x, y = bounds.size.y }
            };
        }

        private object ListTilemaps()
        {
            var tilemaps = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            var list = tilemaps.Select(t =>
            {
                var renderer = t.GetComponent<TilemapRenderer>();
                return new
                {
                    name = t.gameObject.name,
                    instanceId = t.gameObject.GetInstanceID(),
                    tileCount = CountTiles(t),
                    sortingOrder = renderer?.sortingOrder ?? 0
                };
            }).ToList();

            return new { success = true, count = list.Count, tilemaps = list };
        }

        private object ListTileAssets(Dictionary<string, string> p)
        {
            var folder = p.GetValueOrDefault("folder") ?? "Assets";
            var guids = AssetDatabase.FindAssets("t:TileBase", new[] { folder });

            var tiles = guids.Take(100).Select(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                return new
                {
                    name = tile?.name ?? "unknown",
                    path = path,
                    type = tile?.GetType().Name ?? "unknown"
                };
            }).ToList();

            return new { success = true, count = tiles.Count, totalFound = guids.Length, tiles = tiles };
        }

        private object CreateTileAsset(Dictionary<string, string> p)
        {
            var spritePath = p.GetValueOrDefault("spritePath");
            var tilePath = p.GetValueOrDefault("tilePath");

            if (string.IsNullOrEmpty(spritePath))
                return new { success = false, error = "spritePath is required" };
            if (string.IsNullOrEmpty(tilePath))
                return new { success = false, error = "tilePath is required" };

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
                return new { success = false, error = $"Sprite not found at: {spritePath}" };

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;

            // Optional color
            var colorStr = p.GetValueOrDefault("color");
            if (!string.IsNullOrEmpty(colorStr))
            {
                var cp = JsonRpcParamsParser.ParseToDictionary(colorStr);
                float.TryParse(cp.GetValueOrDefault("r") ?? "1", out var r);
                float.TryParse(cp.GetValueOrDefault("g") ?? "1", out var g);
                float.TryParse(cp.GetValueOrDefault("b") ?? "1", out var b);
                float.TryParse(cp.GetValueOrDefault("a") ?? "1", out var a);
                tile.color = new Color(r, g, b, a);
            }

            if (!tilePath.EndsWith(".asset")) tilePath += ".asset";
            AssetDatabase.CreateAsset(tile, tilePath);
            AssetDatabase.SaveAssets();

            return new { success = true, path = tilePath, spriteName = sprite.name, message = $"Created tile asset at {tilePath}" };
        }

        private object SetTileColor(Dictionary<string, string> p)
        {
            var tilemap = FindTilemap(p);
            if (tilemap == null)
                return new { success = false, error = "No Tilemap found" };

            int.TryParse(p.GetValueOrDefault("x") ?? "0", out var x);
            int.TryParse(p.GetValueOrDefault("y") ?? "0", out var y);

            float.TryParse(p.GetValueOrDefault("r") ?? "1", out var r);
            float.TryParse(p.GetValueOrDefault("g") ?? "1", out var g);
            float.TryParse(p.GetValueOrDefault("b") ?? "1", out var b);
            float.TryParse(p.GetValueOrDefault("a") ?? "1", out var a);

            var pos = new Vector3Int(x, y, 0);
            Undo.RecordObject(tilemap, "Set Tile Color");
            tilemap.SetTileFlags(pos, TileFlags.None);
            tilemap.SetColor(pos, new Color(r, g, b, a));
            EditorUtility.SetDirty(tilemap);

            return new { success = true, message = $"Set color at ({x},{y}) to ({r:F2},{g:F2},{b:F2},{a:F2})" };
        }

        private object CompressBounds(Dictionary<string, string> p)
        {
            var tilemap = FindTilemap(p);
            if (tilemap == null)
                return new { success = false, error = "No Tilemap found" };

            tilemap.CompressBounds();
            var bounds = tilemap.cellBounds;

            return new
            {
                success = true,
                message = "Compressed tilemap bounds",
                min = new { x = bounds.xMin, y = bounds.yMin },
                max = new { x = bounds.xMax, y = bounds.yMax }
            };
        }
    }
}
