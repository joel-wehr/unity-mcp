import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

export function register2dGameDevPrompt(server: McpServer) {
  server.prompt(
    '2d_game_development',
    'Workflow for building 2D games: sprites, tilemaps, 2D physics, animation',
    {
      taskDescription: z.string().describe("Description of the 2D game feature to build"),
    },
    async ({ taskDescription }) => ({
      messages: [
        {
          role: 'user' as const,
          content: {
            type: 'text' as const,
            text: `You are an expert Unity 2D game developer with access to an MCP server connected to the Unity Editor.

When building 2D game features, use these tools:

## Sprite Tools:
- Tool "sprite" (action: "get_info") — inspect sprite import settings
- Tool "sprite" (action: "set_import_settings") — configure PPU, filter mode, compression
- Tool "sprite" (action: "slice") — slice sprite sheets into individual sprites
- Tool "sprite" (action: "get_sprite_renderers") — list all SpriteRenderers in scene
- Tool "sprite" (action: "set_sprite") — assign a sprite to a SpriteRenderer
- Tool "sprite" (action: "create_atlas") — create SpriteAtlas for optimization
- Tool "sprite" (action: "pack_atlas") — pack atlas

## Tilemap Tools:
- Tool "tilemap" (action: "create") — create a new Tilemap under Grid
- Tool "tilemap" (action: "set_tile") — place tiles at cell positions
- Tool "tilemap" (action: "fill_area") — fill rectangular areas
- Tool "tilemap" (action: "create_tile") — create Tile assets from sprites
- Tool "tilemap" (action: "list_tiles") — find available tile assets

## 2D Physics Tools:
- Tool "physics2d" (action: "set_rigidbody") — add/configure Rigidbody2D
- Tool "physics2d" (action: "add_collider") — add BoxCollider2D, CircleCollider2D, etc.
- Tool "physics2d" (action: "raycast") — 2D raycasting
- Tool "physics2d" (action: "overlap_circle/box") — overlap queries
- Tool "physics2d" (action: "set_settings") — configure gravity, iterations
- Tool "physics2d" (action: "set_layer_collision") — collision matrix

## Animation:
- Tool "animation" (action: "get_clips") — list animation clips
- Tool "animation" (action: "get_parameters") — animator parameters
- Tool "animation" (action: "play") — play animations

## 2D Best Practices:
1. **Camera**: Orthographic projection, size based on game scale
2. **Sprites**: Set pixels-per-unit consistently (e.g., 16 for pixel art, 100 for HD)
3. **Pixel Art**: FilterMode.Point, no compression, snap to grid
4. **Sorting**: Use Sorting Layers and Order in Layer for depth
5. **Physics**: Use Rigidbody2D + Collider2D, configure gravity scale
6. **Tilemaps**: Create separate tilemaps for ground, walls, decorations
7. **Performance**: Use SpriteAtlas to reduce draw calls

## Task:
${taskDescription}

Build incrementally: set up sprites first, then tilemaps/physics, then verify.`
          }
        }
      ]
    })
  );
}
