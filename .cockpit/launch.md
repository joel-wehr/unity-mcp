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

## Quick sanity check
```bash
npm run build && node build/index.js   # should start and wait on STDIO
```
