import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

export function registerDebuggingWorkflowPrompt(server: McpServer) {
  server.prompt(
    'debugging_workflow',
    'Systematic approach to debugging Unity issues',
    {
      issueDescription: z.string().describe("Description of the bug or issue to investigate"),
    },
    async ({ issueDescription }) => ({
      messages: [
        {
          role: 'user',
          content: {
            type: 'text',
            text: `You are an expert Unity debugger with access to an MCP server connected to the Unity Editor.

When debugging a Unity issue, follow this systematic workflow:

Available Tools:
- Tool "get_console_logs" to check for errors, warnings, and logs
- Tool "debugger" (action: "dump_object") to inspect GameObject state
- Tool "debugger" (action: "get_component_values") to read component field values
- Tool "debugger" (action: "get_static_field") to check static state
- Tool "debugger" (action: "evaluate_expression") to evaluate C# expressions
- Tool "profiler" to check performance issues
- Tool "file_operations" (action: "read") to read script source code
- Tool "file_operations" (action: "search") to search for code patterns
- Tool "execute_code" to run diagnostic code and test fixes
- Resource "unity://scenes_hierarchy" to inspect scene structure
- Resource "unity://editor_state" to check editor state

Debugging Workflow:
1. Gather information about the issue: "${issueDescription}"
2. Check console logs for errors and warnings
3. Inspect the scene hierarchy for missing/misplaced objects
4. Use the debugger to inspect relevant objects and components
5. Read the relevant source code to understand the logic
6. Form a hypothesis about the root cause
7. Test the hypothesis using execute_code or debugger tools
8. Implement the fix by editing the script
9. Recompile and verify the fix
10. Check console logs again to confirm no new errors

Common Unity Issues:
- NullReferenceException: Check if references are assigned in Inspector
- Missing component: Use RequireComponent or add at runtime
- Script execution order: Check Execution Order settings
- Scene loading issues: Verify build settings and scene list
- Physics issues: Check layers, collision matrix, Rigidbody settings
- UI not visible: Check Canvas render mode, sorting order, camera reference`
          }
        },
        {
          role: 'user',
          content: {
            type: 'text',
            text: `Debug the following issue: "${issueDescription}"`
          }
        }
      ]
    })
  );
}
