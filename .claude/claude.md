# Unity MCP Server - AI Development Tracker

This file is monitored by Claude Code for event-driven development. When changes are made to the Features or Issues sections below, the AI will review Unity and Anthropic MCP documentation to implement solutions.

**Last Reviewed:** 2025-12-21

---

## My Role (Monitoring AI Agent)

I am a senior developer AI agent responsible for maintaining and evolving this Unity MCP server. My job is:

### What I Do
1. **Monitor this file** - I watch for changes to the Features and Issues sections
2. **Research documentation** - When updates are detected, I review:
   - Unity Editor scripting documentation (UnityEditor, UnityEngine APIs)
   - Unity Package Manager documentation
   - Anthropic MCP SDK documentation and best practices
   - Existing codebase patterns
3. **Implement solutions** - I write TypeScript code for the MCP server and coordinate with Unity C# requirements
4. **Commit changes** - All work is committed to git with descriptive messages
5. **Update this file** - I mark features as completed and issues as resolved

### How to Request Work
**For new features:** Add an entry under `## Features` with:
- Feature name, status, priority, description
- Relevant Unity APIs to research
- Any additional context

**For bugs/issues:** Add an entry under `## Issues` with:
- Issue title, status, severity
- Steps to reproduce, expected vs actual behavior
- Error messages if applicable

### Event-Driven Workflow
```
Another AI adds Feature/Issue to this file
         ↓
I detect the change
         ↓
I research Unity + MCP documentation
         ↓
I implement the solution in src/
         ↓
I commit to git
         ↓
I update this file (status → Completed/Resolved)
```

---

## Features

<!--
Add feature requests here. The monitoring AI will:
1. Review Unity documentation for relevant APIs
2. Review Anthropic MCP documentation for best practices
3. Implement the feature following MCP patterns
4. Update this file when complete

Format:
### [Feature Name]
- **Status**: Pending | In Progress | Completed
- **Priority**: High | Medium | Low
- **Description**: Brief description of the feature
- **Unity APIs**: Relevant Unity APIs to research
- **Notes**: Additional context
-->


---

## Issues

<!--
Report bugs and issues here. The monitoring AI will:
1. Investigate the root cause
2. Review relevant Unity/MCP documentation
3. Implement a fix
4. Update this file when resolved

Format:
### [Issue Title]
- **Status**: Open | Investigating | Resolved
- **Severity**: Critical | Major | Minor
- **Description**: What's happening
- **Steps to Reproduce**: How to trigger the issue
- **Expected Behavior**: What should happen
- **Actual Behavior**: What actually happens
- **Error Messages**: Any relevant error output
-->


---

## Changelog

<!-- Automatically updated when features are completed or issues are resolved -->

### 2025-12-21
- Initial Unity MCP Server with 22 tools, 7 resources, 1 prompt
- Created AI development tracker

---

## Current Implementation Status

### Implemented Tools (22)
- [x] create_scene - Create new scenes
- [x] delete_scene - Delete scenes
- [x] load_scene - Load scenes
- [x] get_gameobject - Get GameObject details
- [x] update_gameobject - Create/update GameObjects
- [x] delete_gameobject - Delete GameObjects
- [x] duplicate_gameobject - Clone GameObjects
- [x] find_gameobjects - Search GameObjects
- [x] select_gameobject - Select in editor
- [x] update_component - Modify components
- [x] editor_selection - Get/set selection
- [x] create_prefab - Create prefabs
- [x] add_asset_to_scene - Instantiate assets
- [x] manage_asset - Asset operations
- [x] add_package - Package Manager integration
- [x] play_mode - Control play mode
- [x] execute_menu_item - Run menu items
- [x] recompile_scripts - Force recompilation
- [x] get_console_logs - Read console
- [x] send_console_log - Write to console
- [x] run_tests - Execute tests
- [x] search_unity_knowledge - RAG documentation search

### Implemented Resources (7)
- [x] unity://scenes_hierarchy - All GameObjects in loaded scenes
- [x] unity://gameobject/{id} - Detailed GameObject info
- [x] unity://menu-items - Available menu items
- [x] unity://logs/{type} - Console log entries
- [x] unity://packages - Package Manager data
- [x] unity://assets - Asset Database contents
- [x] unity://tests/{mode} - Test Runner info

### Implemented Prompts (1)
- [x] gameobject_handling_strategy - GameObject workflow guidance

---

## Architecture Reference

```
Claude Code <-> STDIO <-> Node.js MCP Server <-> WebSocket <-> Unity Editor (C#)
```

**Key Files:**
- `src/index.ts` - Server entry point, registers all tools/resources
- `src/unity/mcpUnity.ts` - Unity WebSocket bridge (JSON-RPC)
- `src/tools/*.ts` - MCP tool implementations
- `src/resources/*.ts` - MCP resource implementations
- `src/utils/` - Logger and error handling

**Adding New Tools:**
1. Create `src/tools/myNewTool.ts`
2. Define Zod schema for parameters
3. Register in `src/index.ts`
4. Implement Unity C# handler

**Adding New Resources:**
1. Create `src/resources/myNewResource.ts`
2. Define URI pattern
3. Register in `src/index.ts`
4. Implement Unity data provider

---

## Notes
- Issues are reported by AI agents using the MCP server in Unity projects
- This file is monitored for changes - add Features/Issues above to trigger development
- Priority: Bugs that block workflows > New features > Enhancements
