import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

/**
 * Registers the XREAL Optimization Guide prompt with the MCP server.
 * This prompt provides performance optimization guidance for mobile XR.
 */
export function registerXrealOptimizationPrompt(server: McpServer) {
  server.prompt(
    'xreal_optimization_guide',
    'Performance optimization guide for XREAL mobile mixed reality applications',
    {
      targetFrameRate: z.number().default(72).describe("Target frame rate (60, 72, or 90 FPS)"),
      issueArea: z.enum(['rendering', 'physics', 'scripts', 'memory', 'tracking', 'all']).default('all').describe("Specific area to optimize"),
    },
    async ({ targetFrameRate, issueArea }) => ({
      messages: [
        {
          role: 'user',
          content: {
            type: 'text',
            text: `You are an expert AI assistant for XREAL performance optimization.

# XREAL Performance Optimization Guide

## Target: ${targetFrameRate} FPS
## Focus Area: ${issueArea}

## Frame Budget
At ${targetFrameRate} FPS, you have ${(1000 / targetFrameRate).toFixed(2)}ms per frame.
- CPU budget: ~${(1000 / targetFrameRate * 0.6).toFixed(2)}ms
- GPU budget: ~${(1000 / targetFrameRate * 0.8).toFixed(2)}ms
- Reserve: ~${(1000 / targetFrameRate * 0.1).toFixed(2)}ms for overhead

## Step 1: Analyze Current Performance

Use \`profile_xr_scene\`:
\`\`\`
analyzeRendering: true
analyzePhysics: true
analyzeScripts: true
analyzeMemory: true
targetFrameRate: ${targetFrameRate}
generateReport: true
\`\`\`

Use \`get_xr_performance_metrics\` for real-time data:
\`\`\`
includeFrameMetrics: true
includeGpuMetrics: true
includeCpuMetrics: true
includeMemoryMetrics: true
includeThermalState: true
\`\`\`

${issueArea === 'rendering' || issueArea === 'all' ? `
## Rendering Optimization

### Draw Call Reduction
Target: <100 draw calls for mobile XR

1. **Static Batching**
   Use \`configure_android_build\` with \`staticBatching: true\`
   - Mark non-moving objects as static
   - Combine materials where possible

2. **GPU Instancing**
   - Use instanced materials for repeated objects
   - Combine meshes with same material

3. **Occlusion Culling**
   - Enable occlusion culling in scene
   - Use proper occlusion volumes

### Triangle Count
Target: <100K triangles per eye

1. **LOD Groups**
   - Implement LOD for complex meshes
   - Aggressive LOD at distance

2. **Mesh Optimization**
   - Use \`optimizeMeshData: true\` in build settings
   - Remove unused UV channels
   - Simplify distant geometry

### Shader Optimization

1. **Mobile Shaders**
   - Use URP/Mobile shaders
   - Avoid complex fragment operations
   - Minimize texture samples per pixel

2. **Overdraw**
   - Reduce transparent/alpha objects
   - Sort transparent objects properly
   - Use cutout for simple alpha

### Texture Optimization

1. **Compression**
   - Use ASTC compression for Android
   - Appropriate mip levels
   - Power-of-two dimensions

2. **Resolution**
   - UI: 1024x1024 max
   - Environment: 512x512 typical
   - Use atlases where possible

### Stereo Rendering
Use \`set_render_mode\` with:
\`\`\`
stereoRenderingMode: "SinglePassInstanced"
\`\`\`
` : ''}

${issueArea === 'physics' || issueArea === 'all' ? `
## Physics Optimization

### Collision Reduction

1. **Simplified Colliders**
   - Use primitive colliders (box, sphere, capsule)
   - Avoid mesh colliders when possible
   - Combine small colliders

2. **Layer Optimization**
   - Configure physics layer matrix
   - Disable unnecessary collision pairs

3. **Rigidbody Settings**
   - Use interpolation only when needed
   - Reduce solver iterations
   - Mark kinematic when possible

### Spatial Meshing
Use \`enable_meshing\` with:
\`\`\`
meshDensity: "Low" or "Medium"
updateRate: 1.0  // seconds between updates
generateColliders: true  // only if needed for physics
\`\`\`
` : ''}

${issueArea === 'scripts' || issueArea === 'all' ? `
## Script Optimization

### Update Loop

1. **Reduce Update Calls**
   - Cache component references
   - Use events instead of polling
   - Batch operations

2. **Coroutines and Jobs**
   - Move heavy work off main thread
   - Use Job System for parallel work
   - Spread work across frames

### Allocations

1. **Avoid GC Pressure**
   - Object pooling
   - Pre-allocate collections
   - Avoid string concatenation

2. **IL2CPP Benefits**
   Ensure \`scriptingBackend: "IL2CPP"\` for:
   - Better runtime performance
   - Smaller executable
   - Code stripping
` : ''}

${issueArea === 'memory' || issueArea === 'all' ? `
## Memory Optimization

### Asset Memory

1. **Texture Memory**
   - Compress textures (ASTC)
   - Use texture streaming
   - Unload unused textures

2. **Mesh Memory**
   - Strip unused mesh data
   - Use mesh compression
   - Share materials

3. **Audio**
   - Compress audio clips
   - Stream large audio files
   - Unload when not in use

### Runtime Memory

1. **Object Pooling**
   - Pool frequently spawned objects
   - Pre-warm pools
   - Clean up unused pools

2. **Resource Management**
   - Unload unused scenes
   - Use Addressables for large assets
   - Monitor memory with profiler
` : ''}

${issueArea === 'tracking' || issueArea === 'all' ? `
## Tracking Optimization

### Hand Tracking
Use \`enable_hand_tracking\` with:
\`\`\`
trackingMode: "Basic"  // if full joint tracking not needed
trackedHands: "Right"  // if only one hand needed
gestureRecognition: false  // if gestures not used
\`\`\`

### Plane Detection
Use \`enable_plane_detection\` with:
\`\`\`
updateMode: "OnDemand"  // instead of Continuous
maxPlanes: 10  // limit tracked planes
minPlaneArea: 0.5  // filter small planes
\`\`\`

### Image Tracking
Use \`configure_image_tracking\` with:
\`\`\`
maxSimultaneousImages: 2  // reduce from default
trackingMode: "Static"  // if images don't move
\`\`\`
` : ''}

## Thermal Management

Mobile XR devices thermal throttle. Monitor with \`get_xr_performance_metrics\`:

1. **Reduce Thermal Load**
   - Lower frame rate during low activity
   - Reduce feature complexity when warm
   - Implement thermal-aware quality settings

2. **Feature Toggles**
   - Disable meshing when not needed
   - Reduce hand tracking complexity
   - Lower render resolution

## Build Optimization

Use \`configure_android_build\`:
\`\`\`
scriptingBackend: "IL2CPP"
optimizeMeshData: true
staticBatching: true
dynamicBatching: false  // usually not helpful for XR
multithreadedRendering: true
gpuSkinning: true
\`\`\`

## Verification

After optimizations, verify with:
1. \`get_xr_performance_metrics\` - Check frame rate
2. \`profile_xr_scene\` - Verify draw calls and triangles
3. Test on device for thermal behavior

Target metrics for ${targetFrameRate} FPS:
- Frame time: <${(1000 / targetFrameRate).toFixed(1)}ms
- Draw calls: <100
- Triangles: <100K per eye
- Texture memory: <256MB
- No thermal throttling during normal use

Now analyze and optimize the current scene for ${targetFrameRate} FPS, focusing on ${issueArea}.`
          }
        }
      ]
    })
  );
}
