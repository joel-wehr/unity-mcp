using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class ScriptManagementHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "script_management" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "get_defines": return GetDefines(p);
                case "set_defines": return SetDefines(p);
                case "add_define": return AddDefine(p);
                case "remove_define": return RemoveDefine(p);
                case "get_assemblies": return GetAssemblies();
                case "get_execution_order": return GetExecutionOrder();
                case "set_execution_order": return SetExecutionOrder(p);
                case "get_compilation_state": return GetCompilationState();
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object GetDefines(Dictionary<string, string> p)
        {
            var platform = p.GetValueOrDefault("platform");
            var group = string.IsNullOrEmpty(platform)
                ? EditorUserBuildSettings.selectedBuildTargetGroup
                : ParseBuildTargetGroup(platform);

            PlayerSettings.GetScriptingDefineSymbols(
                UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group),
                out var defines);

            return new
            {
                success = true,
                platform = group.ToString(),
                defines = defines
            };
        }

        private object SetDefines(Dictionary<string, string> p)
        {
            var definesJson = p.GetValueOrDefault("defines");
            if (string.IsNullOrEmpty(definesJson))
                return new { success = false, error = "defines array is required" };

            // Parse the JSON array manually
            var defines = ParseStringArray(definesJson);
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;

            PlayerSettings.SetScriptingDefineSymbols(
                UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group),
                defines);

            return new { success = true, defines = defines, platform = group.ToString(), message = $"Set {defines.Length} defines" };
        }

        private object AddDefine(Dictionary<string, string> p)
        {
            var define = p.GetValueOrDefault("define");
            if (string.IsNullOrEmpty(define))
                return new { success = false, error = "define is required" };

            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var target = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);

            PlayerSettings.GetScriptingDefineSymbols(target, out var current);
            if (current.Contains(define))
                return new { success = true, message = $"Define '{define}' already exists", defines = current };

            var newDefines = current.Append(define).ToArray();
            PlayerSettings.SetScriptingDefineSymbols(target, newDefines);

            return new { success = true, added = define, defines = newDefines, message = $"Added define: {define}" };
        }

        private object RemoveDefine(Dictionary<string, string> p)
        {
            var define = p.GetValueOrDefault("define");
            if (string.IsNullOrEmpty(define))
                return new { success = false, error = "define is required" };

            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var target = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);

            PlayerSettings.GetScriptingDefineSymbols(target, out var current);
            var newDefines = current.Where(d => d != define).ToArray();
            PlayerSettings.SetScriptingDefineSymbols(target, newDefines);

            return new { success = true, removed = define, defines = newDefines, message = $"Removed define: {define}" };
        }

        private object GetAssemblies()
        {
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player)
                .Select(a => new
                {
                    name = a.name,
                    outputPath = a.outputPath,
                    sourceFiles = a.sourceFiles.Length,
                    defines = a.defines,
                    references = a.assemblyReferences.Select(r => r.name).ToArray()
                }).ToList();

            var editorAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Select(a => new
                {
                    name = a.name,
                    outputPath = a.outputPath,
                    sourceFiles = a.sourceFiles.Length,
                    defines = a.defines
                }).ToList();

            return new
            {
                success = true,
                playerAssemblies = assemblies,
                editorAssemblies = editorAssemblies
            };
        }

        private object GetExecutionOrder()
        {
            var scripts = MonoImporter.GetAllRuntimeMonoScripts()
                .Where(s => s != null && MonoImporter.GetExecutionOrder(s) != 0)
                .Select(s => new
                {
                    name = s.name,
                    className = s.GetClass()?.FullName ?? s.name,
                    order = MonoImporter.GetExecutionOrder(s)
                })
                .OrderBy(s => s.order)
                .ToList();

            return new { success = true, scripts = scripts };
        }

        private object SetExecutionOrder(Dictionary<string, string> p)
        {
            var scriptName = p.GetValueOrDefault("scriptName");
            var orderStr = p.GetValueOrDefault("order");
            if (string.IsNullOrEmpty(scriptName) || string.IsNullOrEmpty(orderStr))
                return new { success = false, error = "scriptName and order are required" };

            if (!int.TryParse(orderStr, out var order))
                return new { success = false, error = "order must be an integer" };

            var script = MonoImporter.GetAllRuntimeMonoScripts()
                .FirstOrDefault(s => s != null && (s.name == scriptName || s.GetClass()?.FullName == scriptName));

            if (script == null)
                return new { success = false, error = $"Script not found: {scriptName}" };

            MonoImporter.SetExecutionOrder(script, order);
            return new { success = true, scriptName = scriptName, order = order, message = $"Set execution order of {scriptName} to {order}" };
        }

        private object GetCompilationState()
        {
            return new
            {
                success = true,
                isCompiling = EditorApplication.isCompiling,
                compilationFailed = EditorUtility.scriptCompilationFailed,
                assembliesCount = CompilationPipeline.GetAssemblies().Length
            };
        }

        private BuildTargetGroup ParseBuildTargetGroup(string platform)
        {
            switch (platform?.ToLower())
            {
                case "ios": return BuildTargetGroup.iOS;
                case "android": return BuildTargetGroup.Android;
                case "standalone": return BuildTargetGroup.Standalone;
                case "webgl": return BuildTargetGroup.WebGL;
                default: return EditorUserBuildSettings.selectedBuildTargetGroup;
            }
        }

        private string[] ParseStringArray(string json)
        {
            json = json.Trim();
            if (json.StartsWith("[")) json = json.Substring(1);
            if (json.EndsWith("]")) json = json.Substring(0, json.Length - 1);
            return json.Split(',')
                .Select(s => s.Trim().Trim('"'))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }
    }
}
