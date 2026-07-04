import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

/**
 * Registers the Spatial Anchor Workflow prompt with the MCP server.
 * This prompt guides through creating persistent MR experiences with spatial anchors.
 */
export function registerSpatialAnchorWorkflowPrompt(server: McpServer) {
  server.prompt(
    'spatial_anchor_workflow',
    'Workflow for creating persistent mixed reality experiences using spatial anchors',
    {
      useCase: z.enum(['persistent_objects', 'room_mapping', 'multiplayer', 'ar_content']).describe("Primary use case for spatial anchors"),
      persistenceType: z.enum(['local', 'cloud', 'both']).default('local').describe("Where anchors should be persisted"),
    },
    async ({ useCase, persistenceType }) => ({
      messages: [
        {
          role: 'user',
          content: {
            type: 'text',
            text: `You are an expert AI assistant for XREAL spatial anchor development.

# Spatial Anchor Workflow

## Use Case: ${useCase}
## Persistence: ${persistenceType}

## Understanding Spatial Anchors

Spatial anchors are fixed points in the real world that persist across app sessions. They enable:
- Virtual objects that stay in place
- Persistent room configurations
- Shared AR experiences
- Location-based content

## Step 1: Enable Spatial Features

### Enable Plane Detection
Use \`enable_plane_detection\`:
\`\`\`
enabled: true
planeTypes: ["Both"]
minPlaneArea: 0.25
classifyPlanes: true
\`\`\`

This helps identify surfaces where anchors can be placed.

### Optional: Enable Meshing
Use \`enable_meshing\` for detailed environment understanding:
\`\`\`
enabled: true
meshDensity: "Medium"
enableOcclusion: true
\`\`\`

## Step 2: Create Spatial Anchors

### Method A: Position-Based Anchor
Use \`create_spatial_anchor\`:
\`\`\`
anchorName: "my_anchor"
position: { x: 0, y: 1.0, z: 2.0 }
rotation: { x: 0, y: 0, z: 0, w: 1 }
persistent: true
${persistenceType === 'cloud' || persistenceType === 'both' ? 'cloudEnabled: true' : 'cloudEnabled: false'}
metadata: { "type": "content_marker" }
\`\`\`

### Method B: Plane-Attached Anchor
Use \`create_spatial_anchor\`:
\`\`\`
anchorName: "table_anchor"
attachToPlane: "<plane_id_from_detection>"
planeOffset: { x: 0.2, y: 0, z: 0.1 }
persistent: true
\`\`\`

## Step 3: Manage Anchors

### List All Anchors
Read resource: \`xreal://spatial_anchors\`

### Query Specific Anchor
Use \`manage_spatial_anchors\`:
\`\`\`
action: "query"
anchorName: "my_anchor"
includeMetadata: true
includeTransform: true
\`\`\`

### Save Anchors
Use \`manage_spatial_anchors\`:
\`\`\`
action: "save"
\`\`\`

### Load Persisted Anchors
Use \`manage_spatial_anchors\`:
\`\`\`
action: "load"
\`\`\`

### Delete Anchor
Use \`manage_spatial_anchors\`:
\`\`\`
action: "delete"
anchorName: "old_anchor"
\`\`\`

## Use Case Implementations

${useCase === 'persistent_objects' ? `
### Persistent Objects
Place virtual objects that stay in the real world:

1. User places object in scene
2. Create anchor at object position
3. Parent object to anchor
4. Save anchor on scene exit
5. Load anchor on scene enter
6. Instantiate object at anchor

\`\`\`
// Workflow
1. create_spatial_anchor (position: object.position)
2. update_gameobject (set parent to anchor)
3. manage_spatial_anchors (action: "save")
\`\`\`
` : ''}

${useCase === 'room_mapping' ? `
### Room Mapping
Create a map of the room with named anchors:

1. Detect room planes (floor, walls, ceiling)
2. Create anchors at key points
3. Name anchors by function (door, window, desk)
4. Save room configuration
5. Load on re-entry

\`\`\`
// Example anchor structure
{
  "room_floor": { position: {...}, type: "floor" },
  "room_wall_north": { position: {...}, type: "wall" },
  "desk_location": { position: {...}, type: "furniture" }
}
\`\`\`
` : ''}

${useCase === 'multiplayer' ? `
### Multiplayer/Shared Experiences
Share anchor positions between users:

1. Host creates anchors with cloudEnabled: true
2. Host shares anchor IDs with guests
3. Guests load cloud anchors by ID
4. All users see content at same positions

Requirements:
- Cloud persistence enabled
- Network connectivity
- Same physical space (for relocalization)
` : ''}

${useCase === 'ar_content' ? `
### AR Content Placement
Place AR content at real-world locations:

1. Detect surfaces with plane detection
2. User taps to place content
3. Create anchor at hit point
4. Attach content to anchor
5. Optional: persist for future sessions

Best practices:
- Use plane classification for appropriate placement
- Floor anchors for standing content
- Table anchors for tabletop experiences
- Wall anchors for posters/displays
` : ''}

## Best Practices

### Naming Convention
Use descriptive, unique names:
- \`room_{id}_floor\`
- \`furniture_{type}_{index}\`
- \`content_{description}_{timestamp}\`

### Error Handling
- Always check anchor load success
- Handle relocalization failures
- Provide user feedback for anchor status

### Performance
- Limit active anchors (20-50 typical max)
- Clean up unused anchors
- Don't create anchors every frame

### User Experience
- Visualize anchor placement
- Confirm anchor positions with user
- Provide anchor management UI

## Monitoring
Use \`xreal://spatial_anchors\` resource to:
- List all current anchors
- Check anchor persistence status
- Monitor anchor pose updates

Now implement spatial anchors for ${useCase} with ${persistenceType} persistence.`
          }
        }
      ]
    })
  );
}
