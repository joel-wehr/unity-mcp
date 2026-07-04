import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import * as z from "zod";
import { McpUnity } from "../unity/mcpUnity.js";
import { Logger } from "../utils/logger.js";
import { getToolAnnotations } from "../utils/toolAnnotations.js";

export function registerPlaytestTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  server.registerTool(
    "playtest",
    {
      description: "Autonomous playtesting: observe game state, simulate taps, click UI elements, capture screenshots, and interact with game objects during Unity play mode",
      inputSchema: {
      action: z.enum([
        "observe",
        "tap",
        "click_ui",
        "screenshot",
        "get_ui",
        "wait_for",
        "interact",
        "get_grid",
        "swap_tiles",
        "swipe"
      ]).describe(
        "observe: full game state snapshot (scene, UI text, buttons, camera) | " +
        "tap: simulate tap at screen coordinates (tries UI, Physics2D, Physics3D) | " +
        "click_ui: click a UI element by name (Button, Toggle, Slider, InputField) | " +
        "screenshot: capture camera view to PNG file | " +
        "get_ui: list all active UI elements with types and state | " +
        "wait_for: check if a condition is met (scene, text, object, playing, paused) | " +
        "interact: call a method on a game object component via reflection | " +
        "get_grid: read tile grid state (frequencies, positions, static status) for match-3 games | " +
        "swap_tiles: swap two tiles by grid coordinates (col1,row1 <-> col2,row2) | " +
        "swipe: simulate swipe gesture via screen coordinates (start_x,start_y -> end_x,end_y)"
      ),

      // tap parameters
      x: z.number().optional().describe("Screen X coordinate for tap action"),
      y: z.number().optional().describe("Screen Y coordinate for tap action"),

      // click_ui parameters
      name: z.string().optional().describe("UI element name for click_ui action (deep search across all canvases)"),
      value: z.string().optional().describe("Value to set for Slider (float) or InputField (text) in click_ui"),

      // screenshot parameters
      width: z.number().optional().describe("Screenshot width in pixels (default 540)"),
      height: z.number().optional().describe("Screenshot height in pixels (default 960)"),

      // wait_for parameters
      condition: z.string().optional().describe("Condition type for wait_for: scene, text, object, playing, paused"),
      sceneName: z.string().optional().describe("Expected scene name for wait_for 'scene' condition"),
      text: z.string().optional().describe("Text to search for in wait_for 'text' condition"),
      objectName: z.string().optional().describe("GameObject name for wait_for 'object' condition"),

      // interact parameters
      objectPath: z.string().optional().describe("GameObject path for interact action (e.g. 'GameManagers')"),
      componentType: z.string().optional().describe("Component type name for interact (e.g. 'GameFlowManager')"),
      methodName: z.string().optional().describe("Method to invoke for interact action"),
      args: z.string().optional().describe("Comma-separated method arguments for interact action"),

      // swap_tiles parameters
      col1: z.number().optional().describe("Source column for swap_tiles action (0-based)"),
      row1: z.number().optional().describe("Source row for swap_tiles action (0-based)"),
      col2: z.number().optional().describe("Target column for swap_tiles action (0-based)"),
      row2: z.number().optional().describe("Target row for swap_tiles action (0-based)"),

      // swipe parameters
      start_x: z.number().optional().describe("Start screen X for swipe action"),
      start_y: z.number().optional().describe("Start screen Y for swipe action"),
      end_x: z.number().optional().describe("End screen X for swipe action"),
      end_y: z.number().optional().describe("End screen Y for swipe action"),
    },
      annotations: getToolAnnotations("playtest"),
    },
    async ({ action, x, y, name, value, width, height, condition, sceneName, text, objectName, objectPath, componentType, methodName, args, col1, row1, col2, row2, start_x, start_y, end_x, end_y }) => {
      const params: Record<string, string> = { action };

      if (x !== undefined) params.x = x.toString();
      if (y !== undefined) params.y = y.toString();
      if (name) params.name = name;
      if (value) params.value = value;
      if (width !== undefined) params.width = width.toString();
      if (height !== undefined) params.height = height.toString();
      if (condition) params.condition = condition;
      if (sceneName) params.sceneName = sceneName;
      if (text) params.text = text;
      if (objectName) params.objectName = objectName;
      if (objectPath) params.objectPath = objectPath;
      if (componentType) params.componentType = componentType;
      if (methodName) params.methodName = methodName;
      if (args) params.args = args;
      if (col1 !== undefined) params.col1 = col1.toString();
      if (row1 !== undefined) params.row1 = row1.toString();
      if (col2 !== undefined) params.col2 = col2.toString();
      if (row2 !== undefined) params.row2 = row2.toString();
      if (start_x !== undefined) params.start_x = start_x.toString();
      if (start_y !== undefined) params.start_y = start_y.toString();
      if (end_x !== undefined) params.end_x = end_x.toString();
      if (end_y !== undefined) params.end_y = end_y.toString();

      const response = await mcpUnity.sendRequest({
        method: "playtest",
        params: params,
      });

      return {
        content: [
          {
            type: "text" as const,
            text: JSON.stringify(response, null, 2),
          },
        ],
      };
    }
  );
}
