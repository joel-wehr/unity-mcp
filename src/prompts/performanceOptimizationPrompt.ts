import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

export function registerPerformanceOptimizationPrompt(server: McpServer) {
  server.prompt(
    'performance_optimization',
    'Profile and optimize Unity project performance',
    {
      targetPlatform: z.string().optional().describe("Target platform (e.g., 'mobile', 'desktop', 'vr')"),
      targetFps: z.string().optional().describe("Target FPS (e.g., '60', '90')"),
    },
    async ({ targetPlatform, targetFps }) => ({
      messages: [
        {
          role: 'user',
          content: {
            type: 'text',
            text: `You are a Unity performance optimization expert with access to an MCP server connected to the Unity Editor.

When optimizing Unity performance, follow this systematic workflow:

Available Tools:
- Tool "profiler" (action: "start") to begin profiling
- Tool "profiler" (action: "get_memory_snapshot") to check memory usage
- Tool "profiler" (action: "get_render_stats") to check rendering stats
- Tool "profiler" (action: "get_cpu_usage") to check CPU breakdown
- Tool "profiler" (action: "get_gc_allocs") to check GC allocations
- Tool "project_settings" (category: "quality") to adjust quality settings
- Tool "project_settings" (category: "player") to adjust player settings
- Tool "lighting" to optimize lighting settings
- Tool "file_operations" (action: "search") to find performance anti-patterns
- Tool "execute_code" to run diagnostic code
- Tool "debugger" to inspect runtime state

Optimization Workflow:
1. Capture baseline metrics:
   - Start profiler and get frame data
   - Get memory snapshot
   - Get render stats (if in play mode)
   - Get GC allocation data
2. Identify bottlenecks (CPU, GPU, memory, GC)
3. Search codebase for common performance issues:
   - FindObjectOfType in Update loops
   - Excessive Instantiate/Destroy
   - String concatenation in Update
   - LINQ in hot paths
   - Unoptimized coroutines
4. Check project settings:
   - Quality settings appropriate for ${targetPlatform || 'target platform'}
   - Texture compression settings
   - Audio compression settings
5. Check rendering:
   - Draw call count
   - Batching effectiveness
   - Shader complexity
   - Overdraw
6. Implement optimizations
7. Re-profile to verify improvements

Target: ${targetFps ? targetFps + ' FPS' : '60 FPS'} on ${targetPlatform || 'all platforms'}

Key Performance Budgets (mobile):
- Draw calls: < 100
- Triangles: < 100K
- SetPass calls: < 50
- Texture memory: < 200MB
- GC allocations per frame: 0 bytes ideal`
          }
        },
        {
          role: 'user',
          content: {
            type: 'text',
            text: `Optimize performance for ${targetPlatform || 'the current platform'}, targeting ${targetFps || '60'} FPS.`
          }
        }
      ]
    })
  );
}
