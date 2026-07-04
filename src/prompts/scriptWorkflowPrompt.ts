import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

export function registerScriptWorkflowPrompt(server: McpServer) {
  server.prompt(
    'script_workflow',
    'Workflow for creating, editing, and attaching C# scripts in Unity',
    {
      taskDescription: z.string().describe("Description of what the script should do"),
    },
    async ({ taskDescription }) => ({
      messages: [
        {
          role: 'user',
          content: {
            type: 'text',
            text: `You are an expert Unity developer with access to an MCP server connected to the Unity Editor.

When creating or modifying C# scripts in Unity, follow this workflow:

Available Tools:
- Tool "file_operations" (action: "read") to read existing script files
- Tool "file_operations" (action: "write") to create or update script files
- Tool "file_operations" (action: "search") to find scripts containing specific code
- Tool "file_operations" (action: "get_script_classes") to analyze a script's classes, methods, and fields
- Tool "file_operations" (action: "list") to list files in a directory
- Tool "recompile_scripts" to trigger compilation after writing scripts
- Tool "get_console_logs" (logType: "error") to check for compilation errors
- Tool "update_component" to add the script component to a GameObject
- Tool "execute_code" to run arbitrary C# code for testing

Workflow:
1. Understand the task: "${taskDescription}"
2. Search for related existing scripts using "file_operations" search action
3. Read any relevant existing scripts to understand patterns and conventions
4. Write the new/modified script using "file_operations" write action
5. Trigger recompilation with "recompile_scripts"
6. Check for errors with "get_console_logs" (logType: "error")
7. If errors exist, read the error messages, fix the script, and recompile
8. Once compiled, attach the script to GameObjects using "update_component" if needed
9. Test the script behavior using "execute_code" if applicable

Best Practices:
- Follow the project's existing namespace conventions
- Use [SerializeField] for private fields that need inspector exposure
- Add [RequireComponent] attributes when depending on other components
- Use proper Unity lifecycle methods (Awake, Start, Update, OnEnable, etc.)
- Handle null checks for references that may not be assigned
- Follow C# naming conventions (PascalCase for public, _camelCase for private fields)`
          }
        },
        {
          role: 'user',
          content: {
            type: 'text',
            text: `Execute the script workflow for: "${taskDescription}"`
          }
        }
      ]
    })
  );
}
