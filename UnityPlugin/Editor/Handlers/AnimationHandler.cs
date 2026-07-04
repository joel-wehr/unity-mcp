using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class AnimationHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "animation" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "get_clips": return GetClips(p);
                case "get_parameters": return GetParameters(p);
                case "set_parameter": return SetParameter(p);
                case "get_state_info": return GetStateInfo(p);
                case "play": return PlayAnimation(p);
                case "stop": return StopAnimation(p);
                case "sample": return SampleAnimation(p);
                case "create_clip": return CreateClip(p);
                case "pause": return PauseAnimation(p);
                case "crossfade": return CrossfadeAnimation(p);
                case "get_timeline_state": return GetTimelineState(p);
                case "set_timeline_time": return SetTimelineTime(p);
                case "play_timeline": return PlayTimeline(p);
                case "record_animation": return RecordAnimation(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private GameObject FindGO(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("objectPath");
            var id = p.GetValueOrDefault("objectId");
            if (!string.IsNullOrEmpty(path)) return GameObject.Find(path);
            if (!string.IsNullOrEmpty(id) && int.TryParse(id, out var iid))
                return EditorUtility.InstanceIDToObject(iid) as GameObject;
            return Selection.activeGameObject;
        }

        private object GetClips(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var animator = go.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var clips = animator.runtimeAnimatorController.animationClips
                    .Select(c => new
                    {
                        name = c.name,
                        length = c.length,
                        frameRate = c.frameRate,
                        isLooping = c.isLooping,
                        wrapMode = c.wrapMode.ToString(),
                        events = c.events.Length
                    }).ToList();
                return new { success = true, source = "Animator", clips = clips };
            }

            var animation = go.GetComponent<Animation>();
            if (animation != null)
            {
                var clips = new List<object>();
                foreach (AnimationState state in animation)
                {
                    clips.Add(new
                    {
                        name = state.name,
                        length = state.length,
                        speed = state.speed,
                        wrapMode = state.wrapMode.ToString()
                    });
                }
                return new { success = true, source = "Animation", clips = clips };
            }

            return new { success = false, error = "No Animator or Animation component found" };
        }

        private object GetParameters(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var animator = go.GetComponent<Animator>();
            if (animator == null) return new { success = false, error = "No Animator component" };

            var parameters = new List<object>();
            for (int i = 0; i < animator.parameterCount; i++)
            {
                var param = animator.GetParameter(i);
                object value = param.type switch
                {
                    AnimatorControllerParameterType.Bool => (object)animator.GetBool(param.name),
                    AnimatorControllerParameterType.Int => animator.GetInteger(param.name),
                    AnimatorControllerParameterType.Float => animator.GetFloat(param.name),
                    AnimatorControllerParameterType.Trigger => "trigger",
                    _ => "unknown"
                };
                parameters.Add(new { name = param.name, type = param.type.ToString(), value = value?.ToString() });
            }
            return new { success = true, parameters = parameters };
        }

        private object SetParameter(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var animator = go.GetComponent<Animator>();
            if (animator == null) return new { success = false, error = "No Animator component" };

            var paramName = p.GetValueOrDefault("parameterName");
            var paramType = p.GetValueOrDefault("parameterType") ?? "float";
            var paramValue = p.GetValueOrDefault("parameterValue");

            if (string.IsNullOrEmpty(paramName))
                return new { success = false, error = "parameterName is required" };

            switch (paramType.ToLower())
            {
                case "bool":
                    animator.SetBool(paramName, paramValue?.ToLower() == "true");
                    break;
                case "int":
                case "integer":
                    if (int.TryParse(paramValue, out var iv)) animator.SetInteger(paramName, iv);
                    break;
                case "float":
                    if (float.TryParse(paramValue, out var fv)) animator.SetFloat(paramName, fv);
                    break;
                case "trigger":
                    animator.SetTrigger(paramName);
                    break;
            }

            return new { success = true, message = $"Set {paramName} = {paramValue}" };
        }

        private object GetStateInfo(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var animator = go.GetComponent<Animator>();
            if (animator == null) return new { success = false, error = "No Animator component" };

            var layerStr = p.GetValueOrDefault("layerIndex") ?? "0";
            var layer = int.TryParse(layerStr, out var li) ? li : 0;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            return new
            {
                success = true,
                layer = layer,
                normalizedTime = stateInfo.normalizedTime,
                length = stateInfo.length,
                speed = stateInfo.speed,
                isLooping = stateInfo.loop,
                isInTransition = animator.IsInTransition(layer)
            };
        }

        private object PlayAnimation(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var stateName = p.GetValueOrDefault("stateName") ?? p.GetValueOrDefault("clipName");
            if (string.IsNullOrEmpty(stateName))
                return new { success = false, error = "stateName or clipName is required" };

            var animator = go.GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play(stateName);
                return new { success = true, message = $"Playing state: {stateName}" };
            }

            var animation = go.GetComponent<Animation>();
            if (animation != null)
            {
                animation.Play(stateName);
                return new { success = true, message = $"Playing clip: {stateName}" };
            }

            return new { success = false, error = "No Animator or Animation component" };
        }

        private object StopAnimation(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var animation = go.GetComponent<Animation>();
            if (animation != null)
            {
                animation.Stop();
                return new { success = true, message = "Animation stopped" };
            }

            return new { success = true, message = "Stop only works with legacy Animation component. For Animator, set speed to 0." };
        }

        private object SampleAnimation(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var clipName = p.GetValueOrDefault("clipName");
            var timeStr = p.GetValueOrDefault("normalizedTime") ?? "0";
            var time = float.TryParse(timeStr, out var t) ? t : 0f;

            var animator = go.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == clipName);
                if (clip != null)
                {
                    clip.SampleAnimation(go, time * clip.length);
                    return new { success = true, message = $"Sampled {clipName} at t={time}" };
                }
            }

            return new { success = false, error = "Clip not found or no Animator" };
        }

        private object CreateClip(Dictionary<string, string> p)
        {
            var clipPath = p.GetValueOrDefault("newClipPath");
            if (string.IsNullOrEmpty(clipPath))
                return new { success = false, error = "newClipPath is required" };

            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
            AssetDatabase.SaveAssets();

            return new { success = true, path = clipPath, message = $"Created animation clip at {clipPath}" };
        }

        private object PauseAnimation(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var animator = go.GetComponent<Animator>();
            if (animator != null)
            {
                animator.speed = 0f;
                return new { success = true, message = "Animator paused (speed set to 0)" };
            }

            var animation = go.GetComponent<Animation>();
            if (animation != null)
            {
                foreach (AnimationState state in animation)
                {
                    state.speed = 0f;
                }
                return new { success = true, message = "Animation paused (all states speed set to 0)" };
            }

            return new { success = false, error = "No Animator or Animation component found" };
        }

        private object CrossfadeAnimation(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var stateName = p.GetValueOrDefault("stateName");
            if (string.IsNullOrEmpty(stateName))
                return new { success = false, error = "stateName is required for crossfade" };

            var durationStr = p.GetValueOrDefault("transitionDuration") ?? "0.25";
            var duration = float.TryParse(durationStr, out var d) ? d : 0.25f;
            var layerStr = p.GetValueOrDefault("layerIndex") ?? "0";
            var layer = int.TryParse(layerStr, out var li) ? li : 0;
            var timeStr = p.GetValueOrDefault("normalizedTime");
            var time = timeStr != null && float.TryParse(timeStr, out var t) ? t : float.NegativeInfinity;

            var animator = go.GetComponent<Animator>();
            if (animator != null)
            {
                animator.CrossFade(stateName, duration, layer, time);
                return new { success = true, message = $"Crossfading to '{stateName}' over {duration}s on layer {layer}" };
            }

            var animation = go.GetComponent<Animation>();
            if (animation != null)
            {
                animation.CrossFade(stateName, duration);
                return new { success = true, message = $"Crossfading to '{stateName}' over {duration}s" };
            }

            return new { success = false, error = "No Animator or Animation component found" };
        }

        private object GetTimelineState(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            // Use reflection to access PlayableDirector without requiring Timeline package dependency
            var director = go.GetComponent("PlayableDirector");
            if (director == null)
                return new { success = false, error = "No PlayableDirector component found. Install com.unity.timeline package." };

            var dirType = director.GetType();
            var time = dirType.GetProperty("time")?.GetValue(director);
            var duration = dirType.GetProperty("duration")?.GetValue(director);
            var state = dirType.GetProperty("state")?.GetValue(director);
            var timeUpdateMode = dirType.GetProperty("timeUpdateMode")?.GetValue(director);

            return new
            {
                success = true,
                time = time,
                duration = duration,
                state = state?.ToString(),
                timeUpdateMode = timeUpdateMode?.ToString()
            };
        }

        private object SetTimelineTime(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var director = go.GetComponent("PlayableDirector");
            if (director == null)
                return new { success = false, error = "No PlayableDirector component found" };

            var timeStr = p.GetValueOrDefault("timelineTime") ?? "0";
            if (!double.TryParse(timeStr, out var time))
                return new { success = false, error = "Invalid timelineTime value" };

            var dirType = director.GetType();
            var timeProp = dirType.GetProperty("time");
            if (timeProp != null)
            {
                timeProp.SetValue(director, time);
                dirType.GetMethod("Evaluate")?.Invoke(director, null);
                return new { success = true, message = $"Timeline time set to {time}" };
            }

            return new { success = false, error = "Could not set Timeline time" };
        }

        private object PlayTimeline(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var director = go.GetComponent("PlayableDirector");
            if (director == null)
                return new { success = false, error = "No PlayableDirector component found" };

            var timelineAction = p.GetValueOrDefault("timelineAction") ?? "play";
            var dirType = director.GetType();

            switch (timelineAction.ToLower())
            {
                case "play":
                    dirType.GetMethod("Play")?.Invoke(director, null);
                    return new { success = true, message = "Timeline playing" };
                case "pause":
                    dirType.GetMethod("Pause")?.Invoke(director, null);
                    return new { success = true, message = "Timeline paused" };
                case "stop":
                    dirType.GetMethod("Stop")?.Invoke(director, null);
                    return new { success = true, message = "Timeline stopped" };
                case "resume":
                    dirType.GetMethod("Resume")?.Invoke(director, null);
                    return new { success = true, message = "Timeline resumed" };
                default:
                    return new { success = false, error = $"Unknown timeline action: {timelineAction}" };
            }
        }

        private object RecordAnimation(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var recordAction = p.GetValueOrDefault("recordingAction") ?? "start";

            if (recordAction.ToLower() == "start")
            {
                AnimationMode.StartAnimationMode();
                AnimationMode.BeginSampling();
                return new { success = true, message = "Animation recording started" };
            }
            else if (recordAction.ToLower() == "stop")
            {
                AnimationMode.EndSampling();
                AnimationMode.StopAnimationMode();
                return new { success = true, message = "Animation recording stopped" };
            }

            return new { success = false, error = $"Unknown recording action: {recordAction}" };
        }
    }
}
