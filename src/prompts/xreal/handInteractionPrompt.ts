import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

/**
 * Registers the Hand Interaction Strategy prompt with the MCP server.
 * This prompt guides through implementing hand-based interactions.
 */
export function registerHandInteractionPrompt(server: McpServer) {
  server.prompt(
    'hand_interaction_strategy',
    'Best practices and workflow for implementing hand tracking interactions in XREAL',
    {
      interactionType: z.enum(['grab', 'poke', 'gesture', 'all']).describe("Type of hand interaction to implement"),
      targetObject: z.string().optional().describe("Optional: specific GameObject to make interactive"),
    },
    async ({ interactionType, targetObject }) => ({
      messages: [
        {
          role: 'user',
          content: {
            type: 'text',
            text: `You are an expert AI assistant for XREAL hand tracking development.

# Hand Interaction Implementation Guide

## Overview
Implementing ${interactionType} hand interactions${targetObject ? ` for "${targetObject}"` : ''} in XREAL.

## Step 1: Enable Hand Tracking
Use \`enable_hand_tracking\` tool with:
- \`enabled: true\`
- \`trackingMode: "Advanced"\` for full joint tracking
- \`gestureRecognition: true\` for gesture detection
- \`trackedHands: "Both"\`

## Step 2: Configure Gestures
${interactionType === 'gesture' || interactionType === 'all' ? `
Use \`configure_hand_gestures\` tool with appropriate gestures:
- For UI: ["Pinch", "Point", "OpenPalm"]
- For manipulation: ["Pinch", "Grab", "Fist"]
- For navigation: ["Point", "ThumbsUp", "ThumbsDown"]

Set thresholds:
- \`pinchThreshold: 0.7\` (adjust for sensitivity)
- \`grabThreshold: 0.8\`
- \`gestureHoldTime: 0.1\` (seconds before triggering)
` : 'Gesture recognition enabled with defaults.'}

## Step 3: Create Interactable Objects
${targetObject ? `
For "${targetObject}", use \`create_hand_interactable\`:
- \`targetGameObject: "${targetObject}"\`
- \`interactionType: "${interactionType === 'all' ? 'All' : interactionType.charAt(0).toUpperCase() + interactionType.slice(1)}"\`
` : `
Use \`create_hand_interactable\` for each interactive object:`}

### Grab Interactions
\`\`\`
interactionType: "Grab"
grabType: "Kinematic" (for UI-like) or "Physics" (for realistic)
twoHandedGrab: true/false
throwOnRelease: true
highlightOnHover: true
\`\`\`

### Poke Interactions
\`\`\`
interactionType: "Poke"
pokeDepth: 0.02 (meters)
hapticFeedback: true
\`\`\`

## Step 4: Monitor Hand State
Use resource \`xreal://hand_tracking/both\` to monitor:
- Joint positions
- Current gesture
- Pinch strength
- Tracking confidence

Or use \`get_hand_state\` tool for programmatic access.

## Best Practices

### Visual Feedback
1. **Hover State**: Change color/outline when hand approaches
2. **Selection State**: Clear visual when pinching/grabbing
3. **Manipulation State**: Show grabbed object follows hand

### Ergonomics
1. Place interactables within comfortable reach (0.3m - 1.5m)
2. Avoid interactions that require sustained arm extension
3. Use larger hit targets for distant objects
4. Provide haptic feedback when available

### Performance
1. Limit simultaneous tracked hands if performance is an issue
2. Use Basic tracking mode for simpler apps
3. Reduce gesture recognition set to only needed gestures

### Gesture Design
1. Use natural, intuitive gestures
2. Pinch for selection (most reliable)
3. Grab for manipulation
4. Point for distant interaction
5. Open palm for menu/reset

## Debugging
- Use \`get_hand_state\` to check tracking status
- Enable \`jointVisualization: true\` for debugging
- Check \`xreal://hand_tracking/{hand}\` resource for real-time data

## Common Issues

### Tracking Lost
- Ensure hands are in camera view
- Check lighting conditions
- Verify hand tracking is enabled

### Gestures Not Detected
- Adjust thresholds lower
- Check enabled gestures list
- Verify gesture hold time

### Poor Precision
- Use Advanced tracking mode
- Increase smoothingFactor
- Add visual feedback for user guidance

Now implement the ${interactionType} hand interactions${targetObject ? ` for "${targetObject}"` : ''}.`
          }
        }
      ]
    })
  );
}
