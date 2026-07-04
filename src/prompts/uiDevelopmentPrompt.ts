import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

export function registerUiDevelopmentPrompt(server: McpServer) {
  server.prompt(
    'ui_development',
    'Workflow for building Unity UI: Canvas setup, layout, text, buttons, panels',
    {
      taskDescription: z.string().describe("Description of the UI to build"),
    },
    async ({ taskDescription }) => ({
      messages: [
        {
          role: 'user' as const,
          content: {
            type: 'text' as const,
            text: `You are an expert Unity UI developer with access to an MCP server connected to the Unity Editor.

When building UI in Unity, follow this workflow:

Available Tools:
- Tool "update_gameobject" to create/configure Canvas, Panel, Image, Text GameObjects
- Tool "update_component" to configure Canvas (renderMode, CanvasScaler), RectTransform, Image, Text, Button components
- Tool "find_gameobjects" to locate existing UI elements
- Tool "get_gameobject" to inspect UI hierarchy and component details
- Tool "material_shader" to create/assign UI materials
- Tool "execute_code" for complex UI setup that requires runtime API calls
- Tool "file_operations" to read/write UI scripts (MonoBehaviours for UI logic)
- Tool "recompile_scripts" after writing UI scripts

## UI Setup Checklist:
1. **Canvas Setup**: Always start with a Canvas configured for the target:
   - Screen Space - Overlay for HUD/menus
   - Screen Space - Camera for world-relative UI
   - World Space for diegetic/in-world UI
   - Add CanvasScaler with Scale With Screen Size, reference 1080x1920 (mobile) or 1920x1080 (desktop)

2. **Layout**: Use RectTransform anchors and LayoutGroups:
   - Anchor presets: stretch-stretch for backgrounds, center for popups
   - VerticalLayoutGroup / HorizontalLayoutGroup for lists
   - GridLayoutGroup for grids
   - ContentSizeFitter for dynamic content

3. **Text (TextMeshPro)**: Always use TMP_Text, not legacy Text
   - Set font size, alignment, color, overflow mode
   - Use rich text tags for formatting

4. **Buttons**: Button + Image + TMP child text
   - Configure onClick via scripts
   - Set transition type (Color Tint, Sprite Swap, Animation)

5. **Scrollable Content**: ScrollRect + Viewport (mask) + Content (VerticalLayoutGroup)

6. **Navigation**: Configure Selectable.navigation for gamepad/keyboard

## Task:
${taskDescription}

Build the UI step by step, verifying each element exists before adding the next.`
          }
        }
      ]
    })
  );
}
