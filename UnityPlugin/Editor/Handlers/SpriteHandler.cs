using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles Sprite and SpriteAtlas operations.
    /// </summary>
    public class SpriteHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "sprite" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "get_info": return GetSpriteInfo(p);
                case "list": return ListSprites(p);
                case "set_import_settings": return SetImportSettings(p);
                case "slice": return SliceSprite(p);
                case "get_sprite_renderers": return GetSpriteRenderers();
                case "set_sprite": return SetSprite(p);
                case "get_atlases": return GetAtlases(p);
                case "create_atlas": return CreateAtlas(p);
                case "add_to_atlas": return AddToAtlas(p);
                case "pack_atlas": return PackAtlas(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object GetSpriteInfo(Dictionary<string, string> p)
        {
            var assetPath = p.GetValueOrDefault("assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { success = false, error = "assetPath is required" };

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return new { success = false, error = $"Texture not found at: {assetPath}" };

            var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>().ToList();

            return new
            {
                success = true,
                path = assetPath,
                textureType = importer.textureType.ToString(),
                spriteMode = importer.spriteImportMode.ToString(),
                pixelsPerUnit = importer.spritePixelsPerUnit,
                filterMode = importer.filterMode.ToString(),
                maxTextureSize = importer.maxTextureSize,
                compression = importer.textureCompression.ToString(),
                spriteCount = sprites.Count,
                sprites = sprites.Select(s => new
                {
                    name = s.name,
                    rect = new { x = s.rect.x, y = s.rect.y, w = s.rect.width, h = s.rect.height },
                    pivot = new { x = s.pivot.x, y = s.pivot.y },
                    pixelsPerUnit = s.pixelsPerUnit
                }).ToList()
            };
        }

        private object ListSprites(Dictionary<string, string> p)
        {
            var folder = p.GetValueOrDefault("folder") ?? "Assets";
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });

            var sprites = guids.Take(100).Select(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                return new
                {
                    name = sprite?.name ?? "unknown",
                    path = path,
                    size = sprite != null ? new { w = sprite.rect.width, h = sprite.rect.height } : null
                };
            }).ToList();

            return new { success = true, count = sprites.Count, totalFound = guids.Length, sprites = sprites };
        }

        private object SetImportSettings(Dictionary<string, string> p)
        {
            var assetPath = p.GetValueOrDefault("assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { success = false, error = "assetPath is required" };

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return new { success = false, error = $"Texture not found at: {assetPath}" };

            var changed = new List<string>();

            // Always set texture type to Sprite if we're configuring sprite settings
            if (p.TryGetValue("textureType", out var tt))
            {
                switch (tt.ToLower())
                {
                    case "sprite": importer.textureType = TextureImporterType.Sprite; break;
                    case "default": importer.textureType = TextureImporterType.Default; break;
                }
                changed.Add("textureType");
            }

            if (p.TryGetValue("spriteMode", out var sm))
            {
                switch (sm.ToLower())
                {
                    case "single": importer.spriteImportMode = SpriteImportMode.Single; break;
                    case "multiple": importer.spriteImportMode = SpriteImportMode.Multiple; break;
                    case "polygon": importer.spriteImportMode = SpriteImportMode.Polygon; break;
                }
                changed.Add("spriteMode");
            }

            if (p.TryGetValue("pixelsPerUnit", out var ppu) && float.TryParse(ppu, out var ppuf))
            { importer.spritePixelsPerUnit = ppuf; changed.Add("pixelsPerUnit"); }

            if (p.TryGetValue("filterMode", out var fm))
            {
                switch (fm.ToLower())
                {
                    case "point": importer.filterMode = FilterMode.Point; break;
                    case "bilinear": importer.filterMode = FilterMode.Bilinear; break;
                    case "trilinear": importer.filterMode = FilterMode.Trilinear; break;
                }
                changed.Add("filterMode");
            }

            if (p.TryGetValue("maxTextureSize", out var mts) && int.TryParse(mts, out var mtsi))
            { importer.maxTextureSize = mtsi; changed.Add("maxTextureSize"); }

            if (p.TryGetValue("compression", out var comp))
            {
                switch (comp.ToLower())
                {
                    case "none": importer.textureCompression = TextureImporterCompression.Uncompressed; break;
                    case "low": importer.textureCompression = TextureImporterCompression.CompressedLQ; break;
                    case "normal": importer.textureCompression = TextureImporterCompression.Compressed; break;
                    case "high": importer.textureCompression = TextureImporterCompression.CompressedHQ; break;
                }
                changed.Add("compression");
            }

            if (changed.Count > 0)
            {
                importer.SaveAndReimport();
            }

            return new { success = true, changed = changed.ToArray(), message = $"Updated {changed.Count} sprite import settings" };
        }

        private object SliceSprite(Dictionary<string, string> p)
        {
            var assetPath = p.GetValueOrDefault("assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { success = false, error = "assetPath is required" };

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return new { success = false, error = $"Texture not found at: {assetPath}" };

            var sliceMode = p.GetValueOrDefault("sliceMode") ?? "grid";

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;

            if (sliceMode.ToLower() == "grid")
            {
                int.TryParse(p.GetValueOrDefault("cellWidth") ?? "32", out var cellW);
                int.TryParse(p.GetValueOrDefault("cellHeight") ?? "32", out var cellH);
                int.TryParse(p.GetValueOrDefault("offsetX") ?? "0", out var offX);
                int.TryParse(p.GetValueOrDefault("offsetY") ?? "0", out var offY);
                int.TryParse(p.GetValueOrDefault("paddingX") ?? "0", out var padX);
                int.TryParse(p.GetValueOrDefault("paddingY") ?? "0", out var padY);

                // Load the texture to get its size
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null)
                    return new { success = false, error = "Could not load texture" };

                var spritesheet = new List<SpriteMetaData>();
                int cols = (texture.width - offX) / (cellW + padX);
                int rows = (texture.height - offY) / (cellH + padY);
                int index = 0;

                for (int row = rows - 1; row >= 0; row--)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        var meta = new SpriteMetaData
                        {
                            name = $"{texture.name}_{index}",
                            rect = new Rect(
                                offX + col * (cellW + padX),
                                offY + row * (cellH + padY),
                                cellW, cellH),
                            alignment = (int)SpriteAlignment.Center,
                            pivot = new Vector2(0.5f, 0.5f)
                        };
                        spritesheet.Add(meta);
                        index++;
                    }
                }

                importer.spritesheet = spritesheet.ToArray();
                importer.SaveAndReimport();

                return new
                {
                    success = true,
                    spriteCount = spritesheet.Count,
                    cellSize = new { w = cellW, h = cellH },
                    grid = new { cols = cols, rows = rows },
                    message = $"Sliced into {spritesheet.Count} sprites ({cols}x{rows} grid)"
                };
            }

            return new { success = false, error = $"Unsupported slice mode: {sliceMode}. Use 'grid'" };
        }

        private object GetSpriteRenderers()
        {
            var renderers = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            var list = renderers.Select(r => new
            {
                name = r.gameObject.name,
                instanceId = r.gameObject.GetInstanceID(),
                spriteName = r.sprite?.name ?? "none",
                color = $"({r.color.r:F2}, {r.color.g:F2}, {r.color.b:F2}, {r.color.a:F2})",
                flipX = r.flipX,
                flipY = r.flipY,
                sortingLayer = r.sortingLayerName,
                sortingOrder = r.sortingOrder,
                drawMode = r.drawMode.ToString()
            }).ToList();

            return new { success = true, count = list.Count, renderers = list };
        }

        private object SetSprite(Dictionary<string, string> p)
        {
            var objectPath = p.GetValueOrDefault("objectPath");
            var objectId = p.GetValueOrDefault("objectId");

            GameObject go = null;
            if (!string.IsNullOrEmpty(objectPath))
                go = GameObject.Find(objectPath);
            if (go == null && !string.IsNullOrEmpty(objectId) && int.TryParse(objectId, out var id))
                go = EditorUtility.InstanceIDToObject(id) as GameObject;
            if (go == null)
                return new { success = false, error = "objectPath or objectId required" };

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                return new { success = false, error = $"'{go.name}' has no SpriteRenderer" };

            var spritePath = p.GetValueOrDefault("spritePath");
            if (string.IsNullOrEmpty(spritePath))
                return new { success = false, error = "spritePath is required" };

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
                return new { success = false, error = $"Sprite not found at: {spritePath}" };

            Undo.RecordObject(renderer, "Set Sprite");
            renderer.sprite = sprite;

            // Optional color
            if (p.TryGetValue("r", out var rs) && p.TryGetValue("g", out var gs) && p.TryGetValue("b", out var bs))
            {
                float.TryParse(rs, out var r);
                float.TryParse(gs, out var g);
                float.TryParse(bs, out var b);
                float.TryParse(p.GetValueOrDefault("a") ?? "1", out var a);
                renderer.color = new Color(r, g, b, a);
            }

            EditorUtility.SetDirty(go);
            return new { success = true, message = $"Set sprite to '{sprite.name}' on '{go.name}'" };
        }

        private object GetAtlases(Dictionary<string, string> p)
        {
            var folder = p.GetValueOrDefault("folder") ?? "Assets";
            var guids = AssetDatabase.FindAssets("t:SpriteAtlas", new[] { folder });

            var atlases = guids.Select(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(path);
                return new
                {
                    name = atlas?.name ?? "unknown",
                    path = path,
                    spriteCount = atlas?.spriteCount ?? 0
                };
            }).ToList();

            return new { success = true, count = atlases.Count, atlases = atlases };
        }

        private object CreateAtlas(Dictionary<string, string> p)
        {
            var atlasPath = p.GetValueOrDefault("atlasPath");
            if (string.IsNullOrEmpty(atlasPath))
                return new { success = false, error = "atlasPath is required" };

            if (!atlasPath.EndsWith(".spriteatlas")) atlasPath += ".spriteatlas";

            var atlas = new UnityEngine.U2D.SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, atlasPath);

            // Configure packing settings via SerializedObject (Unity 6 compatible)
            var so = new SerializedObject(atlas);
            var packProp = so.FindProperty("m_EditorData.packingSettings");
            if (packProp != null)
            {
                var rotProp = packProp.FindPropertyRelative("enableRotation");
                if (rotProp != null) rotProp.boolValue = p.GetValueOrDefault("enableRotation")?.ToLower() == "true";

                var tightProp = packProp.FindPropertyRelative("enableTightPacking");
                if (tightProp != null) tightProp.boolValue = p.GetValueOrDefault("enableTightPacking")?.ToLower() != "false";

                var padProp = packProp.FindPropertyRelative("padding");
                if (padProp != null)
                {
                    padProp.intValue = 4;
                    if (p.TryGetValue("padding", out var pad) && int.TryParse(pad, out var padI))
                        padProp.intValue = padI;
                }
                so.ApplyModifiedProperties();
            }
            AssetDatabase.SaveAssets();

            return new { success = true, path = atlasPath, message = $"Created SpriteAtlas at {atlasPath}" };
        }

        private object AddToAtlas(Dictionary<string, string> p)
        {
            var atlasPath = p.GetValueOrDefault("atlasPath");
            var spritePaths = p.GetValueOrDefault("spritePaths"); // comma-separated or folder

            if (string.IsNullOrEmpty(atlasPath))
                return new { success = false, error = "atlasPath is required" };

            var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(atlasPath);
            if (atlas == null)
                return new { success = false, error = $"SpriteAtlas not found at: {atlasPath}" };

            var folder = p.GetValueOrDefault("folder");
            var objects = new List<UnityEngine.Object>();

            if (!string.IsNullOrEmpty(folder))
            {
                // Add entire folder
                var folderObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folder);
                if (folderObj != null) objects.Add(folderObj);
            }
            else if (!string.IsNullOrEmpty(spritePaths))
            {
                foreach (var path in spritePaths.Split(','))
                {
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path.Trim());
                    if (obj != null) objects.Add(obj);
                }
            }
            else
            {
                return new { success = false, error = "spritePaths (comma-separated) or folder is required" };
            }

            // Unity 6 compatible: use SerializedObject to add packables
            var so = new SerializedObject(atlas);
            var packablesProp = so.FindProperty("m_EditorData.packables");
            if (packablesProp != null)
            {
                foreach (var obj in objects)
                {
                    packablesProp.arraySize++;
                    packablesProp.GetArrayElementAtIndex(packablesProp.arraySize - 1).objectReferenceValue = obj;
                }
                so.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();

            return new { success = true, addedCount = objects.Count, message = $"Added {objects.Count} items to atlas" };
        }

        private object PackAtlas(Dictionary<string, string> p)
        {
            var atlasPath = p.GetValueOrDefault("atlasPath");
            if (string.IsNullOrEmpty(atlasPath))
                return new { success = false, error = "atlasPath is required" };

            var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(atlasPath);
            if (atlas == null)
                return new { success = false, error = $"SpriteAtlas not found at: {atlasPath}" };

            UnityEditor.U2D.SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);

            return new { success = true, spriteCount = atlas.spriteCount, message = $"Packed atlas '{atlas.name}' ({atlas.spriteCount} sprites)" };
        }
    }
}
