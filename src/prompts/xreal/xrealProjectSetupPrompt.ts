import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

/**
 * Registers the XREAL Project Setup prompt with the MCP server.
 * This prompt guides through setting up a new XREAL Unity project.
 */
export function registerXrealProjectSetupPrompt(server: McpServer) {
  server.prompt(
    'xreal_project_setup',
    'Step-by-step guide for setting up a new XREAL mixed reality project in Unity',
    {
      projectType: z.enum(['MR', 'AR', 'VR']).describe("Type of XR experience to create: MR (mixed reality), AR (augmented reality), or VR (virtual reality)"),
      targetDevice: z.string().default('XREALOnePro').describe("Target XREAL device (e.g., XREALOnePro, XREALAir2Ultra)"),
    },
    async ({ projectType, targetDevice }) => ({
      messages: [
        {
          role: 'user',
          content: {
            type: 'text',
            text: `You are an expert AI assistant for XREAL mixed reality development in Unity.

# XREAL ${projectType} Project Setup Guide

You are setting up a Unity project for XREAL ${targetDevice} development. Follow these steps in order:

## Phase 1: Project Configuration
1. **Setup XREAL Project** - Use \`setup_xreal_project\` tool:
   - Configure for ${projectType} mode
   - Target device: ${targetDevice}
   - Enable hand tracking, image tracking, and plane detection as appropriate

2. **Configure Android Build** - Use \`configure_android_build\` tool:
   - Set minimum SDK to 29 (Android 10)
   - Use IL2CPP scripting backend
   - Enable ARM64 architecture
   - Use OpenGLES3 graphics API

3. **Import NRSDK** - Use \`import_nrsdk\` tool:
   - Import the NRSDK package
   - Configure streaming assets
   - Set up player settings

4. **Validate Setup** - Use \`validate_xreal_setup\` tool:
   - Check all configurations
   - Fix any issues found

## Phase 2: Scene Setup
5. **Create XR Rig** - Use \`create_xr_rig\` tool:
   - Set up XR Origin with XREAL configuration
   - Add hand controllers
   - Configure tracking origin

6. **Setup XR Interaction** - Use \`setup_xr_interaction\` tool:
   - Configure for hand tracking
   - Set up default interactors

${projectType === 'MR' ? `
## Phase 3: Mixed Reality Features
7. **Configure Passthrough** - Use \`configure_passthrough\` tool:
   - Enable camera passthrough
   - Set up alpha blending

8. **Enable Plane Detection** - Use \`enable_plane_detection\` tool:
   - Detect horizontal and vertical planes
   - Enable plane visualization for testing

9. **Configure Occlusion** - Use \`configure_occlusion\` tool:
   - Enable environment depth occlusion
   - Configure hand occlusion
` : ''}

## Phase 4: Verification
10. **Validate Final Setup** - Use \`validate_xreal_setup\` with all checks enabled

## Available Resources
- \`xreal://device_state\` - Check device connection status
- \`xreal://build_settings\` - View current build configuration
- \`xreal://hand_tracking/{hand}\` - Monitor hand tracking (left/right/both)
- \`xreal://detected_planes\` - View detected surfaces
- \`xreal://spatial_anchors\` - List spatial anchors

## Key Tools by Category

### Project Setup
- \`setup_xreal_project\` - Initial project configuration
- \`configure_android_build\` - Android build settings
- \`import_nrsdk\` - Import NRSDK package
- \`validate_xreal_setup\` - Validate configuration

### XR Interaction
- \`create_xr_rig\` - Create camera rig
- \`setup_xr_interaction\` - Configure interaction system
- \`create_hand_interactable\` - Make objects interactive
- \`create_xr_ui\` - Create world-space UI

### Spatial Features
- \`enable_hand_tracking\` - Enable hand tracking
- \`enable_plane_detection\` - Detect surfaces
- \`create_spatial_anchor\` - Create persistent anchors
- \`enable_meshing\` - Generate spatial mesh

### Mixed Reality
- \`configure_passthrough\` - Camera passthrough
- \`set_render_mode\` - AR/VR/MR mode
- \`configure_occlusion\` - Depth occlusion

### Build & Test
- \`build_xreal_apk\` - Build APK
- \`get_connected_devices\` - List connected devices
- \`get_xr_performance_metrics\` - Performance data
- \`profile_xr_scene\` - Analyze scene

Now proceed with setting up the XREAL ${projectType} project for ${targetDevice}.`
          }
        },
        {
          role: 'user',
          content: {
            type: 'text',
            text: `Begin setting up the ${projectType} project for ${targetDevice}. Start with Phase 1.`
          }
        }
      ]
    })
  );
}
