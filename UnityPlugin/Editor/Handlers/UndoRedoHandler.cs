using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class UndoRedoHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "undo_redo" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "undo": return PerformUndo();
                case "redo": return PerformRedo();
                case "get_history": return GetHistory(p);
                case "clear": return ClearHistory();
                case "begin_group": return BeginGroup(p);
                case "end_group": return EndGroup(p);
                case "set_group_name": return SetGroupName(p);
                case "record_object": return RecordObject(p);
                case "flush": return Flush();
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object PerformUndo()
        {
            Undo.PerformUndo();
            return new { success = true, message = "Undo performed", currentGroup = Undo.GetCurrentGroupName() };
        }

        private object PerformRedo()
        {
            Undo.PerformRedo();
            return new { success = true, message = "Redo performed", currentGroup = Undo.GetCurrentGroupName() };
        }

        private object GetHistory(Dictionary<string, string> p)
        {
            var limitStr = p.GetValueOrDefault("historyLimit") ?? "20";
            var limit = int.TryParse(limitStr, out var l) ? Math.Min(l, 100) : 20;

            // Unity doesn't expose full undo history directly, but we can get current state
            return new
            {
                success = true,
                currentGroupName = Undo.GetCurrentGroupName(),
                currentGroupId = Undo.GetCurrentGroup(),
                message = "Undo history details are limited by Unity API. Use undo/redo actions to navigate."
            };
        }

        private object ClearHistory()
        {
            Undo.ClearAll();
            return new { success = true, message = "Undo history cleared" };
        }

        private object BeginGroup(Dictionary<string, string> p)
        {
            var groupName = p.GetValueOrDefault("groupName") ?? "MCP Action";
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(groupName);
            return new { success = true, groupName = groupName, groupId = Undo.GetCurrentGroup() };
        }

        private object EndGroup(Dictionary<string, string> p)
        {
            Undo.IncrementCurrentGroup();
            return new { success = true, message = "Undo group ended", currentGroup = Undo.GetCurrentGroup() };
        }

        private object SetGroupName(Dictionary<string, string> p)
        {
            var name = p.GetValueOrDefault("groupName");
            if (string.IsNullOrEmpty(name))
                return new { success = false, error = "groupName is required" };

            Undo.SetCurrentGroupName(name);
            return new { success = true, groupName = name };
        }

        private object RecordObject(Dictionary<string, string> p)
        {
            var objectPath = p.GetValueOrDefault("objectPath");
            var objectId = p.GetValueOrDefault("objectId");

            GameObject go = null;
            if (!string.IsNullOrEmpty(objectPath))
                go = GameObject.Find(objectPath);
            else if (!string.IsNullOrEmpty(objectId) && int.TryParse(objectId, out var id))
                go = McpId.ToObject(id) as GameObject;

            if (go == null)
                return new { success = false, error = "Object not found" };

            Undo.RecordObject(go, "MCP Record");
            return new { success = true, message = $"Recorded {go.name} for undo" };
        }

        private object Flush()
        {
            Undo.FlushUndoRecordObjects();
            return new { success = true, message = "Undo records flushed" };
        }
    }
}
