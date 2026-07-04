using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class WatchConsoleHandler : IToolHandler
    {
        private static readonly List<WatchLogEntry> _watchBuffer = new List<WatchLogEntry>();
        private static int _cursor;
        private static bool _watching;

        public string[] SupportedMethods => new[] { "watch_console" };

        static WatchConsoleHandler()
        {
            Application.logMessageReceived += OnLog;
            _watching = true;
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (!_watching) return;
            lock (_watchBuffer)
            {
                _watchBuffer.Add(new WatchLogEntry
                {
                    message = message,
                    stackTrace = stackTrace,
                    type = type.ToString().ToLower(),
                    timestamp = DateTime.Now
                });
                while (_watchBuffer.Count > 5000) _watchBuffer.RemoveAt(0);
            }
        }

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "wait_for_message": return WaitForMessage(p);
                case "wait_for_error": return WaitForError(p);
                case "wait_for_silence": return WaitForSilence(p);
                case "wait_for_compilation": return WaitForCompilation(p);
                case "wait_for_play_mode": return WaitForPlayMode(p);
                case "get_new_logs": return GetNewLogs(p);
                case "reset_cursor": return ResetCursor();
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object WaitForMessage(Dictionary<string, string> p)
        {
            var pattern = p.GetValueOrDefault("pattern") ?? "";
            var logType = p.GetValueOrDefault("logType") ?? "all";
            var timeoutStr = p.GetValueOrDefault("timeout") ?? "30000";
            var timeout = int.TryParse(timeoutStr, out var t) ? t : 30000;

            var startTime = DateTime.Now;
            var deadline = startTime.AddMilliseconds(timeout);
            var startIndex = _watchBuffer.Count;

            while (DateTime.Now < deadline)
            {
                lock (_watchBuffer)
                {
                    for (int i = startIndex; i < _watchBuffer.Count; i++)
                    {
                        var entry = _watchBuffer[i];
                        if (logType != "all" && entry.type != logType) continue;
                        if (string.IsNullOrEmpty(pattern) || entry.message.Contains(pattern))
                        {
                            return new
                            {
                                success = true,
                                found = true,
                                message = entry.message,
                                type = entry.type,
                                waitTime = (DateTime.Now - startTime).TotalMilliseconds
                            };
                        }
                    }
                    startIndex = _watchBuffer.Count;
                }
                System.Threading.Thread.Sleep(50);
            }

            return new { success = true, found = false, message = "Timeout waiting for message", waitTime = timeout };
        }

        private object WaitForError(Dictionary<string, string> p)
        {
            p["logType"] = "error";
            return WaitForMessage(p);
        }

        private object WaitForSilence(Dictionary<string, string> p)
        {
            var silenceStr = p.GetValueOrDefault("silenceDuration") ?? "1000";
            var silence = int.TryParse(silenceStr, out var s) ? s : 1000;
            var timeoutStr = p.GetValueOrDefault("timeout") ?? "30000";
            var timeout = int.TryParse(timeoutStr, out var t) ? t : 30000;

            var startTime = DateTime.Now;
            var deadline = startTime.AddMilliseconds(timeout);
            var lastLogCount = _watchBuffer.Count;
            var lastActivityTime = DateTime.Now;

            while (DateTime.Now < deadline)
            {
                if (_watchBuffer.Count > lastLogCount)
                {
                    lastLogCount = _watchBuffer.Count;
                    lastActivityTime = DateTime.Now;
                }
                if ((DateTime.Now - lastActivityTime).TotalMilliseconds >= silence)
                {
                    return new { success = true, silent = true, silenceDuration = silence, waitTime = (DateTime.Now - startTime).TotalMilliseconds };
                }
                System.Threading.Thread.Sleep(50);
            }

            return new { success = true, silent = false, message = "Timeout waiting for silence" };
        }

        private object WaitForCompilation(Dictionary<string, string> p)
        {
            var timeoutStr = p.GetValueOrDefault("timeout") ?? "60000";
            var timeout = int.TryParse(timeoutStr, out var t) ? Math.Max(t, 60000) : 60000;

            var startTime = DateTime.Now;
            var deadline = startTime.AddMilliseconds(timeout);

            // Wait for compilation to start
            while (!EditorApplication.isCompiling && DateTime.Now < deadline)
                System.Threading.Thread.Sleep(100);

            if (!EditorApplication.isCompiling)
                return new { success = true, wasCompiling = false, message = "No compilation detected" };

            // Wait for compilation to finish
            while (EditorApplication.isCompiling && DateTime.Now < deadline)
                System.Threading.Thread.Sleep(100);

            return new
            {
                success = true,
                wasCompiling = true,
                compilationFailed = EditorUtility.scriptCompilationFailed,
                waitTime = (DateTime.Now - startTime).TotalMilliseconds
            };
        }

        private object WaitForPlayMode(Dictionary<string, string> p)
        {
            var targetState = p.GetValueOrDefault("targetState") ?? "Playing";
            var timeoutStr = p.GetValueOrDefault("timeout") ?? "30000";
            var timeout = int.TryParse(timeoutStr, out var t) ? t : 30000;

            var startTime = DateTime.Now;
            var deadline = startTime.AddMilliseconds(timeout);

            while (DateTime.Now < deadline)
            {
                bool match = targetState.ToLower() switch
                {
                    "playing" => EditorApplication.isPlaying && !EditorApplication.isPaused,
                    "paused" => EditorApplication.isPlaying && EditorApplication.isPaused,
                    "stopped" => !EditorApplication.isPlaying,
                    _ => false
                };
                if (match)
                    return new { success = true, state = targetState, waitTime = (DateTime.Now - startTime).TotalMilliseconds };
                System.Threading.Thread.Sleep(50);
            }

            return new { success = false, error = $"Timeout waiting for play mode state: {targetState}" };
        }

        private object GetNewLogs(Dictionary<string, string> p)
        {
            var maxStr = p.GetValueOrDefault("maxLogs") ?? "100";
            var max = int.TryParse(maxStr, out var m) ? Math.Min(m, 500) : 100;
            var includeStack = p.GetValueOrDefault("includeStackTrace")?.ToLower() != "false";

            List<object> logs;
            lock (_watchBuffer)
            {
                var newEntries = _watchBuffer.Skip(_cursor).Take(max).ToList();
                _cursor = Math.Min(_cursor + max, _watchBuffer.Count);

                logs = newEntries.Select(e => (object)new
                {
                    message = e.message,
                    type = e.type,
                    timestamp = e.timestamp.ToString("HH:mm:ss.fff"),
                    stackTrace = includeStack ? e.stackTrace : null
                }).ToList();
            }

            return new { success = true, count = logs.Count, cursor = _cursor, logs = logs };
        }

        private object ResetCursor()
        {
            _cursor = _watchBuffer.Count;
            return new { success = true, cursor = _cursor, message = "Cursor reset to current position" };
        }

        private class WatchLogEntry
        {
            public string message;
            public string stackTrace;
            public string type;
            public DateTime timestamp;
        }
    }
}
