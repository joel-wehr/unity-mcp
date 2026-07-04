# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Structured tool output: `outputSchema` + `structuredContent` on the core
  readers `get_gameobject`, `find_gameobjects`, `get_console_logs`, and `run_tests`,
  so MCP clients can consume typed results instead of parsing text.
- Progress + cancellation for long-running tools (`run_tests`, `build_pipeline`,
  `recompile_scripts`, `asset_import`, `lighting`): time-based
  `notifications/progress` heartbeats (when the client supplies a progress token)
  and forwarding of the client's cancellation signal.
- Continuous integration (typecheck + build on Node 20/22) and `.gitattributes`
  for LF normalization.

### Changed
- Migrated every tool from the deprecated `server.tool()` API to `registerTool()`
  with centralized MCP tool annotations (`readOnly` / `destructive` / etc.).
- Bumped `@modelcontextprotocol/sdk` to `^1.29.0`.

### Fixed
- Unity 6 / 6.5 compile errors: declared the `com.unity.ugui` dependency and added
  an `InstanceID` → `EntityId` reflection shim across the C# handlers.
- Removed a dangling `Samples~/XrealDemo` entry from the UPM manifest that pointed
  at a sample scene that did not exist.
- Corrected package metadata (repository, documentation, changelog, and license
  URLs; author details).

## [1.0.0]

### Added
- Initial Unity MCP server: a Node/TypeScript MCP server bridged over WebSocket to a
  Unity C# editor plugin (`com.joelwehr.unity-mcp`).
- Core Unity Editor control (scenes, GameObjects, components, prefabs, assets,
  packages, play mode, console, tests) plus comprehensive control tools (build
  pipeline, profiler, project settings, and more).
- XREAL One Pro mixed-reality tooling (device, hand tracking, spatial mapping,
  image tracking, XR interaction, and build tools).

[Unreleased]: https://github.com/joel-wehr/unity-mcp/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/joel-wehr/unity-mcp/releases/tag/v1.0.0
