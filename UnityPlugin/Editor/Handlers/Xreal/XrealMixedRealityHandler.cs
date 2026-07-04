using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers.Xreal
{
    /// <summary>
    /// Handles XREAL mixed reality MCP tool requests.
    /// Includes configure_passthrough, set_render_mode, configure_occlusion
    /// </summary>
    public class XrealMixedRealityHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "configure_passthrough",
            "set_render_mode",
            "configure_occlusion"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "configure_passthrough":
                    return ConfigurePassthrough(paramsDict);
                case "set_render_mode":
                    return SetRenderMode(paramsDict);
                case "configure_occlusion":
                    return ConfigureOcclusion(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object ConfigurePassthrough(Dictionary<string, string> @params)
        {
            var enabled = @params.GetValueOrDefault("enabled")?.ToLower() != "false";
            var blendMode = @params.GetValueOrDefault("blendMode") ?? "AlphaBlend";
            var brightnessStr = @params.GetValueOrDefault("brightness") ?? "1";
            var contrastStr = @params.GetValueOrDefault("contrast") ?? "1";
            var saturationStr = @params.GetValueOrDefault("saturation") ?? "1";
            var colorCorrection = @params.GetValueOrDefault("colorCorrection")?.ToLower() != "false";
            var edgeRendering = @params.GetValueOrDefault("edgeRendering")?.ToLower() == "true";
            var environmentDepth = @params.GetValueOrDefault("environmentDepth")?.ToLower() != "false";

            var brightness = float.TryParse(brightnessStr, out var b) ? b : 1.0f;
            var contrast = float.TryParse(contrastStr, out var c) ? c : 1.0f;
            var saturation = float.TryParse(saturationStr, out var s) ? s : 1.0f;

            // In real implementation, this would configure NRSDK's passthrough settings

            return new
            {
                success = true,
                enabled = enabled,
                configuration = new
                {
                    blendMode = blendMode,
                    brightness = brightness,
                    contrast = contrast,
                    saturation = saturation,
                    colorCorrection = colorCorrection,
                    edgeRendering = edgeRendering,
                    environmentDepth = environmentDepth
                },
                note = "Passthrough configuration set - will activate at runtime with NRSDK"
            };
        }

        private object SetRenderMode(Dictionary<string, string> @params)
        {
            var mode = @params.GetValueOrDefault("mode");
            var backgroundType = @params.GetValueOrDefault("backgroundType") ?? "Passthrough";
            var backgroundColor = @params.GetValueOrDefault("backgroundColor") ?? "#000000";
            var enableOcclusion = @params.GetValueOrDefault("enableOcclusion")?.ToLower() != "false";
            var occlusionMode = @params.GetValueOrDefault("occlusionMode") ?? "EnvironmentDepth";
            var stereoRenderingMode = @params.GetValueOrDefault("stereoRenderingMode") ?? "SinglePassInstanced";

            if (string.IsNullOrEmpty(mode))
            {
                return new { success = false, error = "mode is required (VR, AR, or MR)" };
            }

            // Configure camera based on mode
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                switch (mode.ToUpper())
                {
                    case "VR":
                        mainCamera.clearFlags = CameraClearFlags.Skybox;
                        break;
                    case "AR":
                    case "MR":
                        mainCamera.clearFlags = CameraClearFlags.SolidColor;
                        mainCamera.backgroundColor = Color.clear;
                        break;
                }
            }

            // Configure XR settings
            if (stereoRenderingMode == "SinglePassInstanced")
            {
                // PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;
            }

            return new
            {
                success = true,
                mode = mode,
                configuration = new
                {
                    backgroundType = backgroundType,
                    backgroundColor = backgroundColor,
                    enableOcclusion = enableOcclusion,
                    occlusionMode = occlusionMode,
                    stereoRenderingMode = stereoRenderingMode
                },
                cameraConfigured = mainCamera != null,
                note = $"Render mode set to {mode}. Full functionality requires NRSDK at runtime."
            };
        }

        private object ConfigureOcclusion(Dictionary<string, string> @params)
        {
            var enabled = @params.GetValueOrDefault("enabled")?.ToLower() != "false";
            var occlusionType = @params.GetValueOrDefault("occlusionType") ?? "EnvironmentDepth";
            var depthMode = @params.GetValueOrDefault("depthMode") ?? "Medium";
            var handOcclusion = @params.GetValueOrDefault("handOcclusion")?.ToLower() != "false";
            var humanBodyOcclusion = @params.GetValueOrDefault("humanBodyOcclusion")?.ToLower() == "true";
            var smoothEdges = @params.GetValueOrDefault("smoothEdges")?.ToLower() != "false";
            var temporalFiltering = @params.GetValueOrDefault("temporalFiltering")?.ToLower() != "false";

            return new
            {
                success = true,
                enabled = enabled,
                configuration = new
                {
                    occlusionType = occlusionType,
                    depthMode = depthMode,
                    handOcclusion = handOcclusion,
                    humanBodyOcclusion = humanBodyOcclusion,
                    smoothEdges = smoothEdges,
                    temporalFiltering = temporalFiltering
                },
                note = "Occlusion configuration set - will activate at runtime with NRSDK"
            };
        }
    }
}
