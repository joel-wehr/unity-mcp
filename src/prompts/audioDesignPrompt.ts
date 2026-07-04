import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

export function registerAudioDesignPrompt(server: McpServer) {
  server.prompt(
    'audio_design',
    'Workflow for Unity audio: AudioMixer, AudioSources, spatial audio, music',
    {
      taskDescription: z.string().describe("Description of the audio system to build"),
    },
    async ({ taskDescription }) => ({
      messages: [
        {
          role: 'user' as const,
          content: {
            type: 'text' as const,
            text: `You are an expert Unity audio designer with access to an MCP server connected to the Unity Editor.

When building audio systems, use these tools:

## AudioMixer Tools:
- Tool "audio_mixer" (action: "list") — list all AudioMixer assets
- Tool "audio_mixer" (action: "get_groups") — get mixer groups/channels
- Tool "audio_mixer" (action: "get_exposed_parameters") — list exposed params with values
- Tool "audio_mixer" (action: "set_float") — set exposed parameter value (volume, pitch, etc.)
- Tool "audio_mixer" (action: "get_float") — read parameter value
- Tool "audio_mixer" (action: "get_snapshots") — list mixer snapshots
- Tool "audio_mixer" (action: "transition_snapshot") — transition between snapshots
- Tool "audio_mixer" (action: "get_audio_sources") — list all AudioSources in scene

## Asset & Component Tools:
- Tool "asset_import" (action: "get_audio_settings") — audio clip import settings
- Tool "asset_import" (action: "set_audio_settings") — configure compression, sample rate
- Tool "update_component" — configure AudioSource properties
- Tool "update_gameobject" — create GameObjects for audio
- Tool "file_operations" — read/write audio manager scripts

## Audio Design Workflow:
1. **AudioMixer Setup**:
   - Create mixer with groups: Master → Music, SFX, Ambient, UI
   - Expose volume parameters for runtime control
   - Create snapshots for different game states (gameplay, pause, menu)

2. **AudioSource Configuration**:
   - Set output to appropriate mixer group
   - 2D sounds (UI, music): spatialBlend = 0
   - 3D sounds (SFX, ambient): spatialBlend = 1, configure rolloff

3. **Import Settings**:
   - Music: Streaming, Vorbis compression, quality 50-70%
   - SFX: Decompress on Load, PCM or ADPCM
   - Ambient: Compressed in Memory, Vorbis

4. **Spatial Audio**:
   - AudioSource.spatialBlend = 1 for 3D
   - Configure min/max distance for rolloff
   - Use AudioReverbZone for environment effects

## Task:
${taskDescription}

Set up the audio system step by step, verifying mixer routing before adding sources.`
          }
        }
      ]
    })
  );
}
