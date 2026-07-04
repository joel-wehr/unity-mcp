import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { exec, spawn } from 'child_process';
import { promisify } from 'util';
import * as path from 'path';
import * as fs from 'fs';
import * as os from 'os';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

const execAsync = promisify(exec);

// Constants for the tool
const toolName = 'unity_hub';
const toolDescription = `Controls Unity Hub to create projects, manage installations, and open projects.
This tool works INDEPENDENTLY of the Unity Editor connection. Editor installs use the
Unity Hub CLI; project creation and template listing use the installed Editor and its
bundled templates directly (the Hub CLI has no create/templates subcommands).

Actions:
- create_project: Create a new Unity project
- open_project: Open an existing project in Unity Editor
- list_projects: List recent projects from Unity Hub
- list_installations: List installed Unity Editor versions
- install_editor: Install a Unity Editor version
- add_module: Add a module to an installed editor (Android, iOS, WebGL, etc.)
- list_templates: List available project templates
- get_hub_path: Get the Unity Hub installation path

Note: Unity Hub must be installed. Default paths:
- Windows: C:\\Program Files\\Unity Hub\\Unity Hub.exe
- macOS: /Applications/Unity Hub.app/Contents/MacOS/Unity Hub
- Linux: ~/Unity Hub/UnityHub.AppImage`;

const paramsSchema = z.object({
  action: z.enum([
    'create_project', 'open_project', 'list_projects', 'list_installations',
    'install_editor', 'add_module', 'list_templates', 'get_hub_path'
  ]).describe('Unity Hub action to perform'),
  projectPath: z.string().optional()
    .describe('Full path for the new/existing project'),
  projectName: z.string().optional()
    .describe('Name for the new project (used with projectPath parent directory)'),
  editorVersion: z.string().optional()
    .describe('Unity Editor version (e.g., "2022.3.20f1", "6000.0.0f1")'),
  template: z.string().optional()
    .describe('Project template (e.g., "com.unity.template.3d", "com.unity.template.urp-blank")'),
  modules: z.array(z.enum([
    'android', 'ios', 'webgl', 'windows-il2cpp', 'mac-il2cpp',
    'linux-il2cpp', 'lumin', 'appletv', 'facebook-games',
    'windows-server', 'mac-server', 'linux-server'
  ])).optional().describe('Modules to install with editor or add to existing'),
  hubPath: z.string().optional()
    .describe('Custom path to Unity Hub executable (auto-detected if not provided)'),
  waitForExit: z.boolean().optional()
    .describe('Wait for Unity Editor to exit after opening project (default: false)')
});

// Default Unity Hub paths by platform
function getDefaultHubPath(): string {
  switch (process.platform) {
    case 'win32':
      return 'C:\\Program Files\\Unity Hub\\Unity Hub.exe';
    case 'darwin':
      return '/Applications/Unity Hub.app/Contents/MacOS/Unity Hub';
    case 'linux':
      return path.join(process.env.HOME || '~', 'Unity Hub/UnityHub.AppImage');
    default:
      return '';
  }
}

// Find Unity Hub executable
async function findUnityHub(customPath?: string): Promise<string> {
  if (customPath && fs.existsSync(customPath)) {
    return customPath;
  }

  const defaultPath = getDefaultHubPath();
  if (fs.existsSync(defaultPath)) {
    return defaultPath;
  }

  // Try to find via environment or common locations
  const additionalPaths = process.platform === 'win32' ? [
    'C:\\Program Files\\Unity Hub\\Unity Hub.exe',
    path.join(process.env.LOCALAPPDATA || '', 'Programs', 'Unity Hub', 'Unity Hub.exe'),
    path.join(process.env.PROGRAMFILES || '', 'Unity Hub', 'Unity Hub.exe')
  ] : [];

  for (const p of additionalPaths) {
    if (fs.existsSync(p)) {
      return p;
    }
  }

  throw new McpUnityError(
    ErrorType.VALIDATION,
    'Unity Hub not found. Please provide the path via hubPath parameter or install Unity Hub.'
  );
}

// Execute Unity Hub CLI command
async function executeHubCommand(hubPath: string, args: string[], logger: Logger): Promise<string> {
  const quotedHubPath = `"${hubPath}"`;
  const command = `${quotedHubPath} -- --headless ${args.join(' ')}`;

  logger.info(`Executing Unity Hub command: ${command}`);

  try {
    const { stdout, stderr } = await execAsync(command, {
      timeout: 120000, // 2 minute timeout
      windowsHide: true
    });

    if (stderr && !stderr.includes('DevTools')) {
      logger.warn(`Unity Hub stderr: ${stderr}`);
    }

    return stdout;
  } catch (error: any) {
    // Unity Hub sometimes returns non-zero but still succeeds
    if (error.stdout) {
      return error.stdout;
    }
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      `Unity Hub command failed: ${error.message}`
    );
  }
}

interface HubEditor {
  version: string;
  architecture?: string;
  location: string;
}

// List installed editors via `editors -i --json`. The Hub prints some GPU/cache
// noise to stderr; the JSON array we want is on stdout. We still slice out the
// first [...] to be robust against any stray leading output.
async function getInstalledEditors(hubPath: string, logger: Logger): Promise<HubEditor[]> {
  const out = await executeHubCommand(hubPath, ['editors', '-i', '--json'], logger);
  const start = out.indexOf('[');
  const end = out.lastIndexOf(']');
  if (start === -1 || end === -1 || end < start) {
    return [];
  }
  try {
    return JSON.parse(out.slice(start, end + 1));
  } catch (e) {
    logger.warn(`Could not parse Unity Hub editors JSON: ${e instanceof Error ? e.message : String(e)}`);
    return [];
  }
}

// Resolve the Unity Editor executable path for a given version (exact match first,
// then a prefix match so "6000.5" can resolve "6000.5.2f1").
async function resolveEditorExecutable(hubPath: string, editorVersion: string, logger: Logger): Promise<string> {
  const editors = await getInstalledEditors(hubPath, logger);
  const match = editors.find(e => e.version === editorVersion)
    || editors.find(e => e.version?.startsWith(editorVersion));
  if (match?.location && fs.existsSync(match.location)) {
    return match.location;
  }
  throw new McpUnityError(
    ErrorType.VALIDATION,
    `Unity Editor version ${editorVersion} not found or not installed. Use list_installations to see available versions.`
  );
}

// Locate the bundled project-templates directory for an installed editor.
function getProjectTemplatesDir(editorExe: string): string {
  if (process.platform === 'darwin') {
    // .../Unity.app/Contents/MacOS/Unity -> .../Unity.app/Contents/Resources/...
    const contents = path.dirname(path.dirname(editorExe));
    return path.join(contents, 'Resources', 'PackageManager', 'ProjectTemplates');
  }
  // Windows/Linux: .../Editor/Unity(.exe) -> .../Editor/Data/Resources/...
  return path.join(path.dirname(editorExe), 'Data', 'Resources', 'PackageManager', 'ProjectTemplates');
}

// Parse a template tgz filename like "com.unity.template.3d-cross-platform-17.0.14.tgz"
// into { id: "com.unity.template.3d-cross-platform", version: "17.0.14" }.
function parseTemplateName(file: string): { id: string; version: string } | null {
  const m = file.match(/^(.*)-(\d[0-9A-Za-z.\-]*)\.tgz$/);
  if (!m) return null;
  return { id: m[1], version: m[2] };
}

// Resolve a `tar` that can extract .tgz files with Windows drive-letter paths.
// On Windows, GNU tar (often first on PATH via Git) rejects "C:\..." with
// "Cannot connect to C: resolve failed", so prefer the bundled bsdtar at
// System32\tar.exe (present on Windows 10 1803+ / 11). Elsewhere, use PATH `tar`.
function tarExecutable(): string {
  if (process.platform === 'win32') {
    const bsdtar = path.join(process.env.SystemRoot || 'C:\\Windows', 'System32', 'tar.exe');
    if (fs.existsSync(bsdtar)) {
      return `"${bsdtar}"`;
    }
  }
  return 'tar';
}

// Instantiate a project from a bundled template by extracting its ProjectData~
// skeleton (this is exactly what Unity Hub does; no Editor run required).
// Returns true if the template was found and extracted.
async function createFromTemplate(templatesDir: string, template: string, projectPath: string, logger: Logger): Promise<boolean> {
  if (!fs.existsSync(templatesDir)) {
    return false;
  }
  const files = fs.readdirSync(templatesDir).filter(f => f.endsWith('.tgz'));
  const tgz = files.find(f => parseTemplateName(f)?.id === template)
    || files.find(f => f.startsWith(`${template}-`));
  if (!tgz) {
    return false;
  }
  const tgzPath = path.join(templatesDir, tgz);
  fs.mkdirSync(projectPath, { recursive: true });
  // --strip-components=2 drops the "package/ProjectData~/" prefix so
  // Assets/Packages/ProjectSettings land at the project root.
  const command = `${tarExecutable()} -xzf "${tgzPath}" -C "${projectPath}" --strip-components=2 "package/ProjectData~"`;
  logger.info(`Extracting template ${tgz} into ${projectPath}`);
  await execAsync(command, { timeout: 120000, windowsHide: true });
  return fs.existsSync(path.join(projectPath, 'Assets'));
}

// Create a bare (default) project by invoking the Editor binary directly.
// Unity Hub's CLI has no `create` subcommand, so this is the reliable path.
async function runEditorCreateProject(editorExe: string, projectPath: string, logger: Logger): Promise<void> {
  const parent = path.dirname(projectPath);
  if (parent && !fs.existsSync(parent)) {
    fs.mkdirSync(parent, { recursive: true });
  }
  const logFile = path.join(os.tmpdir(), `unity-createproject-${path.basename(projectPath)}.log`);
  const command = `"${editorExe}" -batchmode -createProject "${projectPath}" -quit -logFile "${logFile}"`;
  logger.info(`Creating project via Editor: ${command}`);
  try {
    await execAsync(command, { timeout: 300000, windowsHide: true });
  } catch (error: any) {
    // Unity can exit non-zero even when the project was created; we verify by
    // checking for the Assets/ folder at the call site rather than trusting exit code.
    logger.warn(`Editor -createProject exited non-zero (will verify by directory): ${error.message}`);
  }
}

// Launch the Unity Editor with a project. Resolves the correct editor version
// (from the argument, or the project's own ProjectVersion.txt) and launches the
// editor binary directly.
async function launchUnityEditor(
  hubPath: string,
  projectPath: string,
  editorVersion: string | undefined,
  waitForExit: boolean,
  logger: Logger
): Promise<string> {
  let version = editorVersion;
  if (!version) {
    const projectVersionFile = path.join(projectPath, 'ProjectSettings', 'ProjectVersion.txt');
    if (fs.existsSync(projectVersionFile)) {
      const m = fs.readFileSync(projectVersionFile, 'utf-8').match(/m_EditorVersion:\s*(\S+)/);
      if (m) {
        version = m[1];
      }
    }
  }
  if (!version) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'Could not determine which editor version to open the project with. Pass editorVersion, or ensure the project has ProjectSettings/ProjectVersion.txt.'
    );
  }

  const editorExe = await resolveEditorExecutable(hubPath, version, logger);
  const args = ['-projectPath', `"${projectPath}"`];

  if (waitForExit) {
    await execAsync(`"${editorExe}" ${args.join(' ')}`, { windowsHide: true });
    return `Project opened with Unity ${version} and the Editor has exited: ${projectPath}`;
  }

  // Non-blocking launch
  spawn(`"${editorExe}"`, args, {
    detached: true,
    stdio: 'ignore',
    shell: true,
    windowsHide: true
  }).unref();

  return `Launching Unity ${version} with project: ${projectPath}`;
}

export function registerUnityHubTool(server: McpServer, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.registerTool(
    toolName,
    {
      description: toolDescription,
      inputSchema: paramsSchema.shape,
      annotations: getToolAnnotations(toolName),
    },
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(params, logger);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(params: any, logger: Logger): Promise<CallToolResult> {
  const { action } = params;

  // Find Unity Hub
  const hubPath = await findUnityHub(params.hubPath);
  logger.info(`Using Unity Hub at: ${hubPath}`);

  let result: string;

  switch (action) {
    case 'get_hub_path':
      result = JSON.stringify({ hubPath, platform: process.platform }, null, 2);
      break;

    case 'list_installations':
      result = await executeHubCommand(hubPath, ['editors', '-i'], logger);
      break;

    case 'list_projects':
      // Unity Hub doesn't have a direct CLI for this, but we can read the prefs
      const prefsPath = process.platform === 'win32'
        ? path.join(process.env.APPDATA || '', 'UnityHub', 'projectDir.json')
        : path.join(process.env.HOME || '', '.config', 'UnityHub', 'projectDir.json');

      try {
        if (fs.existsSync(prefsPath)) {
          const prefs = JSON.parse(fs.readFileSync(prefsPath, 'utf-8'));
          result = JSON.stringify(prefs, null, 2);
        } else {
          result = JSON.stringify({
            message: 'Project list not available via CLI. Check Unity Hub UI for recent projects.',
            prefsPath
          }, null, 2);
        }
      } catch {
        result = JSON.stringify({ message: 'Could not read Unity Hub project list' }, null, 2);
      }
      break;

    case 'list_templates': {
      if (!params.editorVersion) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "Parameter 'editorVersion' is required for list_templates"
        );
      }
      // Unity Hub CLI has no `templates` subcommand; the templates ship as .tgz
      // packages inside the installed editor, so enumerate them from disk.
      const editorExe = await resolveEditorExecutable(hubPath, params.editorVersion, logger);
      const templatesDir = getProjectTemplatesDir(editorExe);
      if (!fs.existsSync(templatesDir)) {
        result = JSON.stringify({
          success: false,
          message: `Templates directory not found: ${templatesDir}`,
          editorVersion: params.editorVersion,
          templates: []
        }, null, 2);
        break;
      }
      const templates = fs.readdirSync(templatesDir)
        .filter(f => f.endsWith('.tgz'))
        .map(f => {
          const parsed = parseTemplateName(f);
          return { id: parsed?.id ?? f.replace(/\.tgz$/, ''), version: parsed?.version ?? null, file: f };
        });
      result = JSON.stringify({
        success: true,
        editorVersion: params.editorVersion,
        templatesDir,
        templates
      }, null, 2);
      break;
    }

    case 'create_project': {
      if (!params.projectPath) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "Parameter 'projectPath' is required for create_project"
        );
      }
      if (!params.editorVersion) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "Parameter 'editorVersion' is required for create_project"
        );
      }

      // Unity Hub's CLI has no `create` subcommand. Two reliable paths instead:
      //   - with a template: extract the template's ProjectData~ skeleton (like Hub does)
      //   - otherwise: create a default project via the Editor binary's -createProject
      const editorExe = await resolveEditorExecutable(hubPath, params.editorVersion, logger);
      let usedTemplate = false;
      let templateNote: string | undefined;

      if (params.template) {
        const templatesDir = getProjectTemplatesDir(editorExe);
        try {
          usedTemplate = await createFromTemplate(templatesDir, params.template, params.projectPath, logger);
          if (!usedTemplate) {
            templateNote = `Template '${params.template}' not found in ${templatesDir}; created a default project instead. Use list_templates to see available templates.`;
          }
        } catch (error: any) {
          templateNote = `Template extraction failed (${error.message}); fell back to a default project.`;
          usedTemplate = false;
        }
      }

      if (!usedTemplate) {
        await runEditorCreateProject(editorExe, params.projectPath, logger);
      }

      const created = fs.existsSync(path.join(params.projectPath, 'Assets'));
      result = JSON.stringify({
        success: created,
        message: created
          ? `Project created successfully${usedTemplate ? ` from template '${params.template}'` : ''}`
          : 'Project creation failed - no Assets/ folder was produced at the project path',
        projectPath: params.projectPath,
        editorVersion: params.editorVersion,
        template: usedTemplate ? params.template : (params.template ? 'default (requested template not applied)' : 'default'),
        method: usedTemplate ? 'template-extract' : 'editor-createProject',
        ...(templateNote ? { note: templateNote } : {})
      }, null, 2);
      break;
    }

    case 'open_project':
      if (!params.projectPath) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "Parameter 'projectPath' is required for open_project"
        );
      }

      if (!fs.existsSync(params.projectPath)) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          `Project path does not exist: ${params.projectPath}`
        );
      }

      result = await launchUnityEditor(
        hubPath,
        params.projectPath,
        params.editorVersion,
        params.waitForExit || false,
        logger
      );
      break;

    case 'install_editor':
      if (!params.editorVersion) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "Parameter 'editorVersion' is required for install_editor"
        );
      }

      const installArgs = ['install', '-v', params.editorVersion];

      if (params.modules && params.modules.length > 0) {
        installArgs.push('-m', params.modules.join(','));
      }

      result = await executeHubCommand(hubPath, installArgs, logger);
      break;

    case 'add_module':
      if (!params.editorVersion) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "Parameter 'editorVersion' is required for add_module"
        );
      }
      if (!params.modules || params.modules.length === 0) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "Parameter 'modules' is required for add_module"
        );
      }

      const moduleArgs = ['install-modules', '-v', params.editorVersion, '-m', params.modules.join(',')];
      result = await executeHubCommand(hubPath, moduleArgs, logger);
      break;

    default:
      throw new McpUnityError(
        ErrorType.VALIDATION,
        `Unknown action: ${action}`
      );
  }

  return {
    content: [{
      type: "text" as const,
      text: result
    }]
  };
}
