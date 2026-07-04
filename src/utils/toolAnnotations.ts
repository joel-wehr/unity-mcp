import { ToolAnnotations } from "@modelcontextprotocol/sdk/types.js";

/**
 * Centralized MCP tool annotations for every tool the server registers.
 *
 * Annotations are behavioral hints for MCP clients (per the MCP spec):
 *   - readOnlyHint:    the tool does not modify its environment (pure query/read).
 *   - destructiveHint: the tool may perform destructive updates (only meaningful
 *                      when readOnlyHint is false).
 *   - idempotentHint:  repeated calls with the same arguments have no additional effect.
 *   - openWorldHint:   the tool interacts with external entities (network, devices, disk
 *                      outside the project) whose state the server does not control.
 *
 * NOTE: Per the spec, clients treat these as UNTRUSTED hints unless the server is
 * trusted — they improve UX/safety affordances but are not a security boundary.
 * Destructive confirmations should still be enforced explicitly (see roadmap: elicitation).
 *
 * Keep this map in sync as tools are added. `getToolAnnotations` returns {} for any
 * unlisted tool, which is a safe default (no hints).
 */
export const TOOL_ANNOTATIONS: Record<string, ToolAnnotations> = {
  // ---- Pure readers / queries (read-only) ----
  get_console_logs: { readOnlyHint: true, idempotentHint: true },
  get_gameobject: { readOnlyHint: true, idempotentHint: true },
  find_gameobjects: { readOnlyHint: true, idempotentHint: true },
  watch_console: { readOnlyHint: true },
  profiler: { readOnlyHint: true },
  search_unity_knowledge: { readOnlyHint: true, idempotentHint: true, openWorldHint: true },

  // ---- Destructive: may delete/overwrite project state ----
  delete_gameobject: { readOnlyHint: false, destructiveHint: true },
  delete_scene: { readOnlyHint: false, destructiveHint: true },
  manage_asset: { readOnlyHint: false, destructiveHint: true },
  file_operations: { readOnlyHint: false, destructiveHint: true, openWorldHint: true },
  execute_code: { readOnlyHint: false, destructiveHint: true, openWorldHint: true },

  // ---- Mutating but non-destructive editor operations ----
  create_scene: { readOnlyHint: false, idempotentHint: false },
  load_scene: { readOnlyHint: false, idempotentHint: true },
  create_prefab: { readOnlyHint: false },
  prefab: { readOnlyHint: false },
  add_asset_to_scene: { readOnlyHint: false },
  update_gameobject: { readOnlyHint: false },
  update_component: { readOnlyHint: false },
  duplicate_gameobject: { readOnlyHint: false },
  select_gameobject: { readOnlyHint: false, idempotentHint: true },
  editor_selection: { readOnlyHint: false, idempotentHint: true },
  execute_menu_item: { readOnlyHint: false, openWorldHint: true },
  recompile_scripts: { readOnlyHint: false },
  send_console_log: { readOnlyHint: false },
  run_tests: { readOnlyHint: false },
  play_mode: { readOnlyHint: false },
  undo_redo: { readOnlyHint: false },
  editor_control: { readOnlyHint: false },
  project_settings: { readOnlyHint: false },
  script_management: { readOnlyHint: false },
  scriptable_object: { readOnlyHint: false },
  build_pipeline: { readOnlyHint: false, openWorldHint: true },
  asset_import: { readOnlyHint: false },
  animation: { readOnlyHint: false },
  physics: { readOnlyHint: false },
  physics2d: { readOnlyHint: false },
  material_shader: { readOnlyHint: false },
  lighting: { readOnlyHint: false },
  navmesh: { readOnlyHint: false },
  terrain: { readOnlyHint: false },
  tilemap: { readOnlyHint: false },
  sprite: { readOnlyHint: false },
  particle_system: { readOnlyHint: false },
  audio_mixer: { readOnlyHint: false },
  debugger: { readOnlyHint: false },
  playtest: { readOnlyHint: false },

  // ---- External world: network / package registries / editor installs ----
  add_package: { readOnlyHint: false, openWorldHint: true },
  add_external_dll: { readOnlyHint: false, destructiveHint: false, openWorldHint: true },
  asset_store: { readOnlyHint: false, openWorldHint: true },
  unity_hub: { readOnlyHint: false, openWorldHint: true },

  // ---- XREAL: device queries (read-only, but touch external hardware) ----
  get_xreal_device_info: { readOnlyHint: true, openWorldHint: true },
  get_connected_devices: { readOnlyHint: true, openWorldHint: true },
  get_camera_frame: { readOnlyHint: true, openWorldHint: true },
  get_hand_state: { readOnlyHint: true, openWorldHint: true },
  get_detected_planes: { readOnlyHint: true, openWorldHint: true },
  get_tracked_images: { readOnlyHint: true, openWorldHint: true },
  get_xr_performance_metrics: { readOnlyHint: true, openWorldHint: true },
  get_build_status: { readOnlyHint: true },
  profile_xr_scene: { readOnlyHint: true, openWorldHint: true },
  capture_xr_screenshot: { readOnlyHint: true },
  validate_xreal_setup: { readOnlyHint: true },

  // ---- XREAL: setup / configuration (mutating) ----
  setup_xreal_project: { readOnlyHint: false },
  configure_android_build: { readOnlyHint: false },
  import_nrsdk: { readOnlyHint: false, openWorldHint: true },
  set_tracking_mode: { readOnlyHint: false, openWorldHint: true },
  calibrate_glasses: { readOnlyHint: false, openWorldHint: true },
  enable_hand_tracking: { readOnlyHint: false },
  configure_hand_gestures: { readOnlyHint: false },
  create_hand_interactable: { readOnlyHint: false },
  enable_plane_detection: { readOnlyHint: false },
  create_spatial_anchor: { readOnlyHint: false },
  manage_spatial_anchors: { readOnlyHint: false },
  enable_meshing: { readOnlyHint: false },
  add_tracking_image: { readOnlyHint: false },
  configure_image_tracking: { readOnlyHint: false },
  configure_passthrough: { readOnlyHint: false },
  set_render_mode: { readOnlyHint: false },
  configure_occlusion: { readOnlyHint: false },
  setup_xr_interaction: { readOnlyHint: false },
  create_xr_rig: { readOnlyHint: false },
  add_xr_interactor: { readOnlyHint: false },
  create_xr_ui: { readOnlyHint: false },
  build_xreal_apk: { readOnlyHint: false, openWorldHint: true },
};

/**
 * Returns the annotations for a tool by name, or an empty object (no hints) if unlisted.
 */
export function getToolAnnotations(toolName: string): ToolAnnotations {
  return TOOL_ANNOTATIONS[toolName] ?? {};
}
