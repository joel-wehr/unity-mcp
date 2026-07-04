using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles AudioMixer inspection and manipulation.
    /// </summary>
    public class AudioMixerHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "audio_mixer" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "list": return ListMixers();
                case "get_groups": return GetGroups(p);
                case "get_snapshots": return GetSnapshots(p);
                case "set_float": return SetFloat(p);
                case "get_float": return GetFloat(p);
                case "transition_snapshot": return TransitionSnapshot(p);
                case "get_exposed_parameters": return GetExposedParameters(p);
                case "get_audio_sources": return GetAudioSources();
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private AudioMixer FindMixer(Dictionary<string, string> p)
        {
            var mixerPath = p.GetValueOrDefault("mixerPath");
            if (!string.IsNullOrEmpty(mixerPath))
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);

            var mixerName = p.GetValueOrDefault("mixerName");
            if (!string.IsNullOrEmpty(mixerName))
            {
                var guids = AssetDatabase.FindAssets($"t:AudioMixer {mixerName}");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
                    if (mixer != null && mixer.name == mixerName) return mixer;
                }
            }

            // Return first found mixer
            var allGuids = AssetDatabase.FindAssets("t:AudioMixer");
            if (allGuids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetDatabase.GUIDToAssetPath(allGuids[0]));

            return null;
        }

        private object ListMixers()
        {
            var guids = AssetDatabase.FindAssets("t:AudioMixer");
            var mixers = guids.Select(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
                return new
                {
                    name = mixer?.name ?? "unknown",
                    path = path
                };
            }).ToList();

            return new { success = true, count = mixers.Count, mixers = mixers };
        }

        private object GetGroups(Dictionary<string, string> p)
        {
            var mixer = FindMixer(p);
            if (mixer == null)
                return new { success = false, error = "No AudioMixer found" };

            // Use reflection to access internal groups
            var outputGroup = mixer.outputAudioMixerGroup;
            var groups = mixer.FindMatchingGroups("");
            var groupList = groups.Select(g => new
            {
                name = g.name,
                isOutput = g == outputGroup
            }).ToList();

            return new { success = true, mixerName = mixer.name, count = groupList.Count, groups = groupList };
        }

        private object GetSnapshots(Dictionary<string, string> p)
        {
            var mixer = FindMixer(p);
            if (mixer == null)
                return new { success = false, error = "No AudioMixer found" };

            // Use reflection to get snapshots (not directly exposed)
            var snapshotsProp = typeof(AudioMixer).GetProperty("snapshots",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (snapshotsProp != null)
            {
                var snapshots = snapshotsProp.GetValue(mixer) as AudioMixerSnapshot[];
                if (snapshots != null)
                {
                    var list = snapshots.Select(s => new { name = s.name }).ToList();
                    return new { success = true, mixerName = mixer.name, count = list.Count, snapshots = list };
                }
            }

            return new { success = true, mixerName = mixer.name, count = 0, snapshots = new object[0], note = "Could not read snapshots via reflection" };
        }

        private object SetFloat(Dictionary<string, string> p)
        {
            var mixer = FindMixer(p);
            if (mixer == null)
                return new { success = false, error = "No AudioMixer found" };

            var paramName = p.GetValueOrDefault("parameterName");
            var valueStr = p.GetValueOrDefault("value");

            if (string.IsNullOrEmpty(paramName))
                return new { success = false, error = "parameterName is required" };
            if (!float.TryParse(valueStr, out var value))
                return new { success = false, error = "value must be a number" };

            if (mixer.SetFloat(paramName, value))
                return new { success = true, message = $"Set '{paramName}' to {value}" };

            return new { success = false, error = $"Parameter '{paramName}' not found. Make sure it's exposed in the AudioMixer." };
        }

        private object GetFloat(Dictionary<string, string> p)
        {
            var mixer = FindMixer(p);
            if (mixer == null)
                return new { success = false, error = "No AudioMixer found" };

            var paramName = p.GetValueOrDefault("parameterName");
            if (string.IsNullOrEmpty(paramName))
                return new { success = false, error = "parameterName is required" };

            if (mixer.GetFloat(paramName, out var value))
                return new { success = true, parameterName = paramName, value = value };

            return new { success = false, error = $"Parameter '{paramName}' not found or not exposed" };
        }

        private object TransitionSnapshot(Dictionary<string, string> p)
        {
            var mixer = FindMixer(p);
            if (mixer == null)
                return new { success = false, error = "No AudioMixer found" };

            var snapshotName = p.GetValueOrDefault("snapshotName");
            var durationStr = p.GetValueOrDefault("duration") ?? "1";
            var duration = float.TryParse(durationStr, out var d) ? d : 1f;

            if (string.IsNullOrEmpty(snapshotName))
                return new { success = false, error = "snapshotName is required" };

            var group = mixer.FindMatchingGroups("")[0]; // root group
            var snapshot = mixer.FindSnapshot(snapshotName);

            if (snapshot == null)
                return new { success = false, error = $"Snapshot '{snapshotName}' not found" };

            snapshot.TransitionTo(duration);
            return new { success = true, message = $"Transitioning to snapshot '{snapshotName}' over {duration}s" };
        }

        private object GetExposedParameters(Dictionary<string, string> p)
        {
            var mixer = FindMixer(p);
            if (mixer == null)
                return new { success = false, error = "No AudioMixer found" };

            // Use reflection to access exposed parameters
            var exposedParams = new List<object>();

            // AudioMixer.exposedParameters is available via reflection
            var paramsProp = typeof(AudioMixer).GetProperty("exposedParameters",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (paramsProp != null)
            {
                var exposedArr = paramsProp.GetValue(mixer) as Array;
                if (exposedArr != null)
                {
                    foreach (var item in exposedArr)
                    {
                        var nameField = item.GetType().GetField("name") ?? item.GetType().GetField("Name");
                        var guidField = item.GetType().GetField("guid") ?? item.GetType().GetField("GUID");
                        var paramName = nameField?.GetValue(item)?.ToString();
                        if (paramName != null)
                        {
                            mixer.GetFloat(paramName, out var value);
                            exposedParams.Add(new { name = paramName, value = value });
                        }
                    }
                }
            }

            return new { success = true, mixerName = mixer.name, count = exposedParams.Count, parameters = exposedParams };
        }

        private object GetAudioSources()
        {
            var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            var list = sources.Select(s => new
            {
                name = s.gameObject.name,
                instanceId = McpId.Get(s.gameObject),
                clip = s.clip?.name ?? "none",
                isPlaying = s.isPlaying,
                volume = s.volume,
                pitch = s.pitch,
                loop = s.loop,
                mute = s.mute,
                spatialBlend = s.spatialBlend,
                outputGroup = s.outputAudioMixerGroup?.name ?? "none"
            }).ToList();

            return new { success = true, count = list.Count, audioSources = list };
        }
    }
}
