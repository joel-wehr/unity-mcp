using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

#if TEST_FRAMEWORK_ENABLED
using UnityEditor.TestTools.TestRunner.Api;
#endif

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles Test Runner MCP tool requests.
    /// </summary>
    public class TestHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "run_tests"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "run_tests":
                    return RunTests(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object RunTests(Dictionary<string, string> @params)
        {
#if TEST_FRAMEWORK_ENABLED
            var testMode = @params.GetValueOrDefault("testMode") ?? "EditMode";
            var testFilter = @params.GetValueOrDefault("testFilter") ?? "";
            var returnOnlyFailures = @params.GetValueOrDefault("returnOnlyFailures")?.ToLower() != "false";
            var returnWithLogs = @params.GetValueOrDefault("returnWithLogs")?.ToLower() == "true";

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var results = new List<TestResult>();
            var completed = false;

            // Create callbacks
            var callbacks = new TestRunCallbacks(
                result =>
                {
                    results.Add(new TestResult
                    {
                        name = result.Name,
                        fullName = result.FullName,
                        passed = result.ResultState == "Passed",
                        duration = result.Duration,
                        message = result.Message,
                        stackTrace = returnWithLogs ? result.StackTrace : null
                    });
                },
                () => completed = true
            );

            api.RegisterCallbacks(callbacks);

            // Build filter
            var filter = new Filter
            {
                testMode = testMode.ToLower() == "playmode" ? TestMode.PlayMode : TestMode.EditMode
            };

            if (!string.IsNullOrEmpty(testFilter))
            {
                filter.testNames = new[] { testFilter };
            }

            // Execute tests
            api.Execute(new ExecutionSettings(filter));

            // Wait for completion (simplified)
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMinutes(5);
            while (!completed && (DateTime.Now - startTime) < timeout)
            {
                System.Threading.Thread.Sleep(100);
            }

            api.UnregisterCallbacks(callbacks);

            // Filter results
            var filteredResults = returnOnlyFailures
                ? results.Where(r => !r.passed).ToList()
                : results;

            var passedCount = results.Count(r => r.passed);
            var failedCount = results.Count(r => !r.passed);

            return new
            {
                success = failedCount == 0,
                testMode = testMode,
                totalTests = results.Count,
                passed = passedCount,
                failed = failedCount,
                results = filteredResults
            };
#else
            return new
            {
                success = false,
                error = "Test Framework not installed. Add com.unity.test-framework package."
            };
#endif
        }

#if TEST_FRAMEWORK_ENABLED
        private class TestRunCallbacks : ICallbacks
        {
            private readonly Action<ITestResultAdaptor> _onTestFinished;
            private readonly Action _onRunFinished;

            public TestRunCallbacks(Action<ITestResultAdaptor> onTestFinished, Action onRunFinished)
            {
                _onTestFinished = onTestFinished;
                _onRunFinished = onRunFinished;
            }

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor result) => _onRunFinished?.Invoke();
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) => _onTestFinished?.Invoke(result);
        }
#endif

        [Serializable]
        private class TestResult
        {
            public string name;
            public string fullName;
            public bool passed;
            public double duration;
            public string message;
            public string stackTrace;
        }
    }
}
