using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class AssetImportHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "asset_import" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "get_settings": return GetImportSettings(p);
                case "set_settings": return SetImportSettings(p);
                case "reimport": return Reimport(p);
                case "reimport_all": return ReimportAll();
                case "get_importer_type": return GetImporterType(p);
                case "apply_preset": return ApplyPreset(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object GetImportSettings(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("assetPath");
            if (string.IsNullOrEmpty(path))
                return new { success = false, error = "assetPath is required" };

            var importer = AssetImporter.GetAtPath(path);
            if (importer == null)
                return new { success = false, error = $"No importer for: {path}" };

            if (importer is TextureImporter tex)
                return GetTextureSettings(tex, path);
            if (importer is ModelImporter model)
                return GetModelSettings(model, path);
            if (importer is AudioImporter audio)
                return GetAudioSettings(audio, path);

            return new
            {
                success = true,
                assetPath = path,
                importerType = importer.GetType().Name,
                userData = importer.userData,
                assetBundleName = importer.assetBundleName
            };
        }

        private object GetTextureSettings(TextureImporter tex, string path)
        {
            return new
            {
                success = true,
                assetPath = path,
                importerType = "TextureImporter",
                textureType = tex.textureType.ToString(),
                spriteImportMode = tex.spriteImportMode.ToString(),
                maxTextureSize = tex.maxTextureSize,
                textureCompression = tex.textureCompression.ToString(),
                mipmapEnabled = tex.mipmapEnabled,
                sRGBTexture = tex.sRGBTexture,
                alphaSource = tex.alphaSource.ToString(),
                alphaIsTransparency = tex.alphaIsTransparency,
                wrapMode = tex.wrapMode.ToString(),
                filterMode = tex.filterMode.ToString(),
                anisoLevel = tex.anisoLevel,
                isReadable = tex.isReadable,
                pixelsPerUnit = tex.spritePixelsPerUnit
            };
        }

        private object GetModelSettings(ModelImporter model, string path)
        {
            return new
            {
                success = true,
                assetPath = path,
                importerType = "ModelImporter",
                globalScale = model.globalScale,
                useFileScale = model.useFileScale,
                importBlendShapes = model.importBlendShapes,
                importVisibility = model.importVisibility,
                importCameras = model.importCameras,
                importLights = model.importLights,
                meshCompression = model.meshCompression.ToString(),
                isReadable = model.isReadable,
                importAnimation = model.importAnimation,
                animationType = model.animationType.ToString(),
                materialImportMode = model.materialImportMode.ToString()
            };
        }

        private object GetAudioSettings(AudioImporter audio, string path)
        {
            var defaultSettings = audio.defaultSampleSettings;
            return new
            {
                success = true,
                assetPath = path,
                importerType = "AudioImporter",
                forceToMono = audio.forceToMono,
                loadInBackground = audio.loadInBackground,
                loadType = defaultSettings.loadType.ToString(),
                compressionFormat = defaultSettings.compressionFormat.ToString(),
                quality = defaultSettings.quality,
                sampleRateSetting = defaultSettings.sampleRateSetting.ToString()
            };
        }

        private object SetImportSettings(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("assetPath");
            if (string.IsNullOrEmpty(path))
                return new { success = false, error = "assetPath is required" };

            var importer = AssetImporter.GetAtPath(path);
            if (importer == null)
                return new { success = false, error = $"No importer for: {path}" };

            var changed = new List<string>();

            if (importer is TextureImporter tex)
            {
                var settingsJson = p.GetValueOrDefault("textureSettings");
                if (!string.IsNullOrEmpty(settingsJson))
                {
                    var s = JsonRpcParamsParser.ParseToDictionary(settingsJson);
                    if (s.TryGetValue("maxSize", out var ms) && int.TryParse(ms, out var maxSize))
                    { tex.maxTextureSize = maxSize; changed.Add("maxSize"); }
                    if (s.TryGetValue("sRGB", out var srgb))
                    { tex.sRGBTexture = srgb.ToLower() == "true"; changed.Add("sRGB"); }
                    if (s.TryGetValue("mipmapEnabled", out var mm))
                    { tex.mipmapEnabled = mm.ToLower() == "true"; changed.Add("mipmapEnabled"); }
                    if (s.TryGetValue("isReadable", out var ir))
                    { tex.isReadable = ir.ToLower() == "true"; changed.Add("isReadable"); }
                    if (s.TryGetValue("spritePixelsPerUnit", out var ppu) && float.TryParse(ppu, out var ppuf))
                    { tex.spritePixelsPerUnit = ppuf; changed.Add("spritePixelsPerUnit"); }
                }
            }

            if (changed.Count > 0)
            {
                importer.SaveAndReimport();
            }

            return new { success = true, changed = changed.ToArray(), message = $"Updated {changed.Count} import settings for {path}" };
        }

        private object Reimport(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("assetPath");
            if (string.IsNullOrEmpty(path))
                return new { success = false, error = "assetPath is required" };

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return new { success = true, message = $"Reimported: {path}" };
        }

        private object ReimportAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return new { success = true, message = "Reimporting all assets" };
        }

        private object GetImporterType(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("assetPath");
            if (string.IsNullOrEmpty(path))
                return new { success = false, error = "assetPath is required" };

            var importer = AssetImporter.GetAtPath(path);
            return new
            {
                success = true,
                assetPath = path,
                importerType = importer?.GetType().Name ?? "none",
                importerFullType = importer?.GetType().FullName ?? "none"
            };
        }

        private object ApplyPreset(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("assetPath");
            var presetPath = p.GetValueOrDefault("presetPath");
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(presetPath))
                return new { success = false, error = "assetPath and presetPath are required" };

            var preset = AssetDatabase.LoadAssetAtPath<UnityEditor.Presets.Preset>(presetPath);
            if (preset == null) return new { success = false, error = $"Preset not found: {presetPath}" };

            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) return new { success = false, error = $"No importer for: {path}" };

            var applied = preset.ApplyTo(importer);
            if (applied) importer.SaveAndReimport();

            return new { success = applied, message = applied ? $"Preset applied to {path}" : "Preset could not be applied (type mismatch?)" };
        }
    }
}
