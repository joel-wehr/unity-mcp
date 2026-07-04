# Unity MCP — Launch / Build / Run

This is an **MCP server** (talks STDIO to an MCP client), **not** a web app.
The cockpit managed dev server is **disabled** for this project (`dev_status` →
`enabled: false`) — there is no port/URL to serve. Ignore the Dev tab here.

## Prerequisites
- Node.js >= 18
- For the Unity side: Unity 2021.3 LTS+ (handlers guard for both 2022.3 and Unity 6),
  Android SDK API 29+, and for XREAL work a Samsung S24 + XREAL One Pro glasses.

## Build (TypeScript → build/)
```bash
npm install        # first time
npm run build      # tsc -> build/index.js
```
Other scripts: `npm run watch` (tsc --watch), `npm run clean` (rimraf build),
`npm start` (node build/index.js), `npm run inspector` (MCP Inspector UI).

## Typecheck without emitting
```bash
npx tsc --noEmit
```

## Run as an MCP server
Registered via `.mcp.json` at repo root. The client launches:
```
node C:\Users\joelw\joelwehr.com\GitHub\unity-mcp\build\index.js
```
with env: `UNITY_PORT=8090`, `UNITY_HOST=localhost`, `LOGGING=true`.
So: run `npm run build` first, then the client (Claude Code / Cursor / etc.) spawns
it over STDIO. It connects to the Unity plugin's WebSocket on **port 8090**.

## Unity plugin side
Copy `UnityPlugin/` into your Unity project (see `SETUP_GUIDE.md` for the full XREAL
setup). The plugin hosts the WebSocket server the Node process connects to. If the
Node server can't bind/connect, confirm the Unity Editor is open with the plugin
loaded and listening on 8090.

## Local test project (for validating tools live)
A dedicated Unity 6 testbed lives at `C:\Users\joelw\joelwehr.com\Unity\McpTestbed`
(Unity **6000.5.2f1**), with the plugin embedded at
`McpTestbed/Packages/com.joelwehr.unity-mcp/`.

Installed editors: `2022.3.62f3` and `6000.5.2f1` under
`C:\Program Files\Unity\Hub\Editor\<ver>\Editor\Unity.exe`.

Re-sync the plugin into the testbed after editing `UnityPlugin/`:
```bash
cp -r UnityPlugin/. "C:/Users/joelw/joelwehr.com/Unity/McpTestbed/Packages/com.joelwehr.unity-mcp/"
```
Compile-check (batchmode, quits after):
```bash
"C:/Program Files/Unity/Hub/Editor/6000.5.2f1/Editor/Unity.exe" \
  -projectPath "C:/Users/joelw/joelwehr.com/Unity/McpTestbed" -batchmode -quit -logFile <log>
# grep the log for "error CS"; 0 = clean.
```
Resident run (bridge stays up on 8090 for round-trip tests) — launch WITHOUT `-quit`
in the background, poll TCP 8090, run a client, then stop the `Unity.exe` whose command
line contains `McpTestbed`. A reusable round-trip harness that reuses the built
`McpUnity` client lives in the scratchpad (`roundtrip.mjs`); it calls
get_console_logs / send_console_log / create_scene. Env: `UNITY_PORT=8090`.

NOTE: batchmode without `-quit` only stays resident if scripts compile cleanly;
a compile error makes it exit 1 immediately.

## Quick sanity check
```bash
npm run build && node build/index.js   # should start and wait on STDIO
```
