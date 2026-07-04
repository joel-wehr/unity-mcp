using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles Console-related MCP tool requests.
    /// </summary>
    public class ConsoleHandler : IToolHandler
    {
        private static readonly List<LogEntry> _logCache = new List<LogEntry>();
        private static bool _isCapturing;

        public string[] SupportedMethods => new[]
        {
            "send_console_log",
            "get_console_logs"
        };

        static ConsoleHandler()
        {
            Application.logMessageReceived += OnLogReceived;
            _isCapturing = true;
        }

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "send_console_log":
                    return SendConsoleLog(paramsDict);
                case "get_console_logs":
                    return GetConsoleLogs(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private static void OnLogReceived(string message, string stackTrace, LogType type)
        {
            if (!_isCapturing) return;

            lock (_logCache)
            {
                _logCache.Add(new LogEntry
                {
                    message = message,
                    stackTrace = stackTrace,
                    type = type.ToString().ToLower(),
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                });

                // Keep cache size reasonable
                while (_logCache.Count > 1000)
                {
                    _logCache.RemoveAt(0);
                }
            }
        }

        private object SendConsoleLog(Dictionary<string, string> @params)
        {
            var message = @params.GetValueOrDefault("message");
            var type = @params.GetValueOrDefault("type") ?? "info";

            if (string.IsNullOrEmpty(message))
            {
                return new { success = false, error = "message is required" };
            }

            switch (type.ToLower())
            {
                case "warning":
                    Debug.LogWarning($"[MCP] {message}");
                    break;
                case "error":
                    Debug.LogError($"[MCP] {message}");
                    break;
                default:
                    Debug.Log($"[MCP] {message}");
                    break;
            }

            return new { success = true, message = message, type = type };
        }

        private object GetConsoleLogs(Dictionary<string, string> @params)
        {
            var logType = @params.GetValueOrDefault("logType");
            var limitStr = @params.GetValueOrDefault("limit") ?? "50";
            var offsetStr = @params.GetValueOrDefault("offset") ?? "0";
            var includeStackTrace = @params.GetValueOrDefault("includeStackTrace")?.ToLower() != "false";

            var limit = int.TryParse(limitStr, out var l) ? Math.Min(l, 500) : 50;
            var offset = int.TryParse(offsetStr, out var o) ? o : 0;

            List<LogEntry> logs;

            lock (_logCache)
            {
                logs = _logCache.ToList();
            }

            // Filter by type
            if (!string.IsNullOrEmpty(logType))
            {
                logs = logs.Where(log => log.type.Equals(logType, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Apply pagination
            var totalCount = logs.Count;
            logs = logs.Skip(offset).Take(limit).ToList();

            // Optionally strip stack traces
            if (!includeStackTrace)
            {
                logs = logs.Select(log => new LogEntry
                {
                    message = log.message,
                    type = log.type,
                    timestamp = log.timestamp,
                    stackTrace = null
                }).ToList();
            }

            return new
            {
                success = true,
                totalCount = totalCount,
                offset = offset,
                limit = limit,
                logs = logs
            };
        }

        [Serializable]
        private class LogEntry
        {
            public string message;
            public string stackTrace;
            public string type;
            public string timestamp;
        }
    }
}
