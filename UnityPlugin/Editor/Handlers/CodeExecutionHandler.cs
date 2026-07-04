using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Executes arbitrary C# code in the Unity Editor context.
    /// This is the most powerful tool — it can do anything the Unity Editor API allows.
    /// </summary>
    public class CodeExecutionHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "execute_code" };

        // Capture console output during execution
        private static readonly StringBuilder _outputCapture = new StringBuilder();
        private static bool _capturing;

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var code = paramsDict.GetValueOrDefault("code");

            if (string.IsNullOrEmpty(code))
                return new { success = false, error = "code parameter is required" };

            return ExecuteCode(code);
        }

        private object ExecuteCode(string userCode)
        {
            try
            {
                // Try runtime compilation via CSharpCodeProvider (multiple assembly name attempts)
                Type providerType = null;
                foreach (var asmName in new[]
                {
                    "Microsoft.CSharp.CSharpCodeProvider, System",
                    "Microsoft.CSharp.CSharpCodeProvider, Microsoft.CSharp",
                    "Microsoft.CSharp.CSharpCodeProvider, System.CodeDom.Compiler"
                })
                {
                    providerType = Type.GetType(asmName);
                    if (providerType != null) break;
                }
                // Also search loaded assemblies
                if (providerType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            providerType = asm.GetType("Microsoft.CSharp.CSharpCodeProvider");
                            if (providerType != null) break;
                        }
                        catch { }
                    }
                }
                if (providerType != null)
                {
                    return ExecuteWithCodeProvider(providerType, userCode);
                }

                // Fallback: file-based compilation
                return ExecuteWithFileCompilation(userCode);
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    innerError = ex.InnerException?.Message
                };
            }
        }

        private object ExecuteWithCodeProvider(Type providerType, string userCode)
        {
            // Build the full source code
            string fullSource = WrapUserCode(userCode);

            // Create CSharpCodeProvider
            var provider = Activator.CreateInstance(providerType);

            // Create CompilerParameters — search multiple assembly names
            Type compParamsType = null;
            foreach (var asmName in new[]
            {
                "System.CodeDom.Compiler.CompilerParameters, System",
                "System.CodeDom.Compiler.CompilerParameters, System.CodeDom"
            })
            {
                compParamsType = Type.GetType(asmName);
                if (compParamsType != null) break;
            }
            if (compParamsType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { compParamsType = asm.GetType("System.CodeDom.Compiler.CompilerParameters"); if (compParamsType != null) break; } catch { }
                }
            }
            if (compParamsType == null)
                return new { success = false, error = "CompilerParameters type not found — runtime compilation not available", method = "CSharpCodeProvider" };

            var compParams = Activator.CreateInstance(compParamsType);

            // Set GenerateInMemory = true
            var genInMemProp = compParamsType.GetProperty("GenerateInMemory");
            var genExeProp = compParamsType.GetProperty("GenerateExecutable");
            if (genInMemProp != null) genInMemProp.SetValue(compParams, true);
            if (genExeProp != null) genExeProp.SetValue(compParams, false);

            // Add all loaded assemblies as references
            var refAsmProp = compParamsType.GetProperty("ReferencedAssemblies");
            if (refAsmProp == null)
                return new { success = false, error = "ReferencedAssemblies property not found", method = "CSharpCodeProvider" };
            var referencedAssemblies = refAsmProp.GetValue(compParams);
            var addMethod = referencedAssemblies?.GetType().GetMethod("Add", new[] { typeof(string) });
            if (addMethod == null)
                return new { success = false, error = "Could not find Add method on ReferencedAssemblies", method = "CSharpCodeProvider" };

            // Dedupe assembly references by simple name to avoid "type defined multiple times" errors.
            //
            // The previous loop added EVERY loaded assembly's Location, which on Unity's .NET Standard
            // 2.1 / netstandard2.1 profile includes overlapping facades — e.g. both mscorlib.dll and
            // netstandard.dll/System.Runtime.dll get pulled in, all type-forwarding the same core types
            // (System.Type, System.IO.File, System.Text.StringBuilder, System.Collections.Generic.List`1,
            // etc.). CSharpCodeProvider then refuses to compile user code that touches those types with
            // CS0433 "type exists in multiple assemblies" / "defined multiple times".
            //
            // Strategy:
            //   1. Drop the type-forwarding "reference assemblies" — they exist only to forward types
            //      to a real implementation assembly (recognized by ReferenceAssemblyAttribute).
            //   2. Skip the netstandard facade entirely when mscorlib is in the set: Unity's
            //      CSharpCodeProvider (Mono) targets the full BCL and mscorlib provides every type
            //      netstandard forwards.
            //   3. For each remaining assembly, keep only the FIRST instance per simple name
            //      (case-insensitive). This handles the rare case where the same assembly is loaded
            //      from two paths.
            //
            // Smoke-test snippets that previously failed with CS0433 and should now compile:
            //
            //   // Snippet A — Type
            //   var t = typeof(System.Type);
            //   return t.FullName;
            //
            //   // Snippet B — System.IO.File
            //   return System.IO.File.Exists(UnityEngine.Application.dataPath + "/manifest.json");
            //
            //   // Snippet C — StringBuilder + List<T>
            //   var sb = new System.Text.StringBuilder();
            //   var list = new System.Collections.Generic.List<string> { "a", "b" };
            //   foreach (var x in list) sb.Append(x);
            //   return sb.ToString();
            var addedSimpleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<Assembly>();
            bool hasNetstandard = false;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location)) continue;

                    // Skip pure reference assemblies (type-forwarders); they cause duplicate-type errors.
                    var isRefAsm = false;
                    try
                    {
                        foreach (var attr in asm.GetCustomAttributesData())
                        {
                            if (attr.AttributeType?.FullName == "System.Runtime.CompilerServices.ReferenceAssemblyAttribute")
                            {
                                isRefAsm = true;
                                break;
                            }
                        }
                    }
                    catch { /* attribute introspection can fail on some assemblies; treat as non-ref */ }
                    if (isRefAsm) continue;

                    var simpleName = asm.GetName().Name;
                    if (string.Equals(simpleName, "netstandard", StringComparison.OrdinalIgnoreCase))
                        hasNetstandard = true;

                    candidates.Add(asm);
                }
                catch { /* Skip assemblies that can't be inspected */ }
            }

            foreach (var asm in candidates)
            {
                try
                {
                    var simpleName = asm.GetName().Name ?? string.Empty;

                    // On Unity 2022.3 with the .NET Standard 2.1 profile, BOTH mscorlib.dll and
                    // netstandard.dll are real implementation assemblies (not type-forwarders, so they
                    // survive the ReferenceAssemblyAttribute filter above). They overlap on core BCL
                    // types — System.Object, System.Type, System.IO.File, System.Text.StringBuilder,
                    // System.Collections.Generic.List`1, etc. — causing CSharpCodeProvider to emit
                    // CS0433 "defined multiple times".
                    //
                    // We prefer netstandard because, on the .NET Standard 2.1 profile, netstandard.dll
                    // is the canonical API surface (System.Object lives there). Dropping netstandard
                    // and keeping only mscorlib breaks "type defined in unreferenced assembly netstandard"
                    // errors for the same code. So: if netstandard is present, skip mscorlib.
                    if (hasNetstandard && string.Equals(simpleName, "mscorlib", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!addedSimpleNames.Add(simpleName)) continue;
                    addMethod.Invoke(referencedAssemblies, new object[] { asm.Location });
                }
                catch { /* Skip assemblies that can't be referenced */ }
            }

            // Compile
            var compileMethod = providerType.GetMethod("CompileAssemblyFromSource",
                new[] { compParamsType, typeof(string[]) });
            if (compileMethod == null)
                return new { success = false, error = "CompileAssemblyFromSource method not found on provider", method = "CSharpCodeProvider" };
            var results = compileMethod.Invoke(provider, new object[] { compParams, new[] { fullSource } });
            if (results == null)
                return new { success = false, error = "Compilation returned null", method = "CSharpCodeProvider" };

            // Check for errors
            var errorsProperty = results.GetType().GetProperty("Errors");
            var errors = errorsProperty.GetValue(results);
            var hasErrorsProperty = errors.GetType().GetProperty("HasErrors");
            bool hasErrors = (bool)hasErrorsProperty.GetValue(errors);

            if (hasErrors)
            {
                var errorList = new List<string>();
                var enumerator = ((System.Collections.IEnumerable)errors).GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var err = enumerator.Current;
                    var isWarning = (bool)err.GetType().GetProperty("IsWarning").GetValue(err);
                    if (!isWarning)
                    {
                        var line = (int)err.GetType().GetProperty("Line").GetValue(err);
                        var text = (string)err.GetType().GetProperty("ErrorText").GetValue(err);
                        errorList.Add($"Line {line}: {text}");
                    }
                }
                return new
                {
                    success = false,
                    error = "Compilation failed",
                    compilationErrors = errorList.ToArray(),
                    method = "CSharpCodeProvider"
                };
            }

            // Execute
            var compiledAsmProp = results.GetType().GetProperty("CompiledAssembly");
            if (compiledAsmProp == null)
                return new { success = false, error = "CompiledAssembly property not found on results", method = "CSharpCodeProvider" };
            var compiledAssembly = compiledAsmProp.GetValue(results) as Assembly;
            if (compiledAssembly == null)
                return new { success = false, error = "CompiledAssembly is null — compilation may have failed silently", method = "CSharpCodeProvider" };
            return InvokeCompiled(compiledAssembly);
        }

        private object ExecuteWithFileCompilation(string userCode)
        {
            string fullSource = WrapUserCode(userCode);
            var tempDir = System.IO.Path.Combine(Application.dataPath, "Editor", "McpTemp");
            var tempFile = System.IO.Path.Combine(tempDir, "McpDynamicExecution.cs");

            try
            {
                // Write the temp file
                if (!System.IO.Directory.Exists(tempDir))
                    System.IO.Directory.CreateDirectory(tempDir);

                System.IO.File.WriteAllText(tempFile, fullSource);

                // Import the asset so Unity compiles it
                var relativePath = "Assets/Editor/McpTemp/McpDynamicExecution.cs";
                AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);

                // Wait for compilation
                var startTime = DateTime.Now;
                while (EditorApplication.isCompiling && (DateTime.Now - startTime).TotalSeconds < 30)
                {
                    System.Threading.Thread.Sleep(100);
                }

                // Find and invoke the compiled type
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .FirstOrDefault(t => t.FullName == "McpDynamicExecution");

                if (type == null)
                {
                    return new
                    {
                        success = false,
                        error = "File compilation triggered but type not found yet. Compilation may still be in progress.",
                        method = "FileCompilation"
                    };
                }

                var method = type.GetMethod("Execute", BindingFlags.Static | BindingFlags.Public);
                if (method == null)
                    return new { success = false, error = "Execute method not found in compiled type" };

                var result = method.Invoke(null, null);
                return FormatResult(result, "FileCompilation");
            }
            finally
            {
                // Clean up
                try
                {
                    if (System.IO.File.Exists(tempFile))
                    {
                        System.IO.File.Delete(tempFile);
                        var metaFile = tempFile + ".meta";
                        if (System.IO.File.Exists(metaFile))
                            System.IO.File.Delete(metaFile);
                        AssetDatabase.Refresh();
                    }
                }
                catch { /* Best effort cleanup */ }
            }
        }

        private object InvokeCompiled(Assembly compiledAssembly)
        {
            var type = compiledAssembly.GetType("McpDynamicExecution");
            if (type == null)
                return new { success = false, error = "Compiled type 'McpDynamicExecution' not found" };

            var method = type.GetMethod("Execute", BindingFlags.Static | BindingFlags.Public);
            if (method == null)
                return new { success = false, error = "Execute method not found" };

            // Capture Debug.Log output during execution
            _outputCapture.Clear();
            _capturing = true;
            Application.logMessageReceived += CaptureLog;

            object result = null;
            Exception execError = null;

            try
            {
                result = method.Invoke(null, null);
            }
            catch (TargetInvocationException tie)
            {
                execError = tie.InnerException ?? tie;
            }
            catch (Exception ex)
            {
                execError = ex;
            }
            finally
            {
                Application.logMessageReceived -= CaptureLog;
                _capturing = false;
            }

            if (execError != null)
            {
                return new
                {
                    success = false,
                    error = execError.Message,
                    stackTrace = execError.StackTrace,
                    consoleOutput = _outputCapture.ToString(),
                    method = "CSharpCodeProvider"
                };
            }

            return FormatResult(result, "CSharpCodeProvider");
        }

        private object FormatResult(object result, string compilationMethod)
        {
            var output = _outputCapture.ToString();

            if (result == null)
            {
                return new
                {
                    success = true,
                    result = (string)null,
                    resultType = "void",
                    consoleOutput = string.IsNullOrEmpty(output) ? null : output,
                    method = compilationMethod
                };
            }

            // Try to serialize complex objects
            string resultStr;
            string resultType = result.GetType().Name;

            if (result is string s)
                resultStr = s;
            else if (result is System.Collections.IEnumerable enumerable && !(result is string))
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                    items.Add(item?.ToString() ?? "null");
                resultStr = "[" + string.Join(", ", items) + "]";
            }
            else
                resultStr = result.ToString();

            return new
            {
                success = true,
                result = resultStr,
                resultType = resultType,
                consoleOutput = string.IsNullOrEmpty(output) ? null : output,
                method = compilationMethod
            };
        }

        private static void CaptureLog(string message, string stackTrace, LogType type)
        {
            if (!_capturing) return;
            _outputCapture.AppendLine($"[{type}] {message}");
        }

        private string WrapUserCode(string userCode)
        {
            // Check if the user already provided a full class
            if (userCode.Contains("class ") && userCode.Contains("Execute"))
                return userCode;

            // Check if the code is a single expression (no semicolons except at the very end)
            var trimmed = userCode.Trim().TrimEnd(';');
            bool isExpression = !trimmed.Contains(';') && !trimmed.Contains('\n')
                && !trimmed.StartsWith("var ") && !trimmed.StartsWith("if ")
                && !trimmed.StartsWith("for ") && !trimmed.StartsWith("foreach ");

            string body;
            if (isExpression)
            {
                body = $"return {trimmed};";
            }
            else
            {
                // Multi-statement code — check if it has a return statement
                body = userCode.Contains("return ") ? userCode : userCode + "\nreturn null;";
            }

            return $@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class McpDynamicExecution
{{
    public static object Execute()
    {{
        {body}
    }}
}}";
        }
    }
}
