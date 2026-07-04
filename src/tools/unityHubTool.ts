import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { exec, spawn } from 'child_process';
import { promisify } from 'util';
import * as path from 'path';
import * as fs from 'fs';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

const execAsync = promisify(exec);

// Constants for the tool
const toolName = 'unity_hub';
const toolDescription = `Controls Unity Hub to create projects, manage installations, and open projects.
This tool works INDEPENDENTLY of the Unity Editor connection - it uses Unity Hub CLI.

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

// Launch Unity Editor with a project (non-blocking)
async function launchUnityEditor(
  hubPath: string,
  projectPath: string,
  editorVersion: string | undefined,
  waitForExit: boolean,
  logger: Logger
): Promise<string> {
  // First, we need to find the editor path
  let editorPath: string;

  if (editorVersion) {
    // Get editor path for specific version
    const installsOutput = await executeHubCommand(hubPath, ['editors', '-i'], logger);
    const lines = installsOutput.split('\n');

    for (const line of lines) {
      if (line.includes(editorVersion)) {
        // Parse the path from the output
        const match = line.match(/installed at (.+)/i);
        if (match) {
          editorPath = match[1].trim();
          break;
        }
      }
    }

    if (!editorPath!) {
      throw new McpUnityError(
        ErrorType.VALIDATION,
        `Unity Editor version ${editorVersion} not found. Use list_installations to see available versions.`
      );
    }
  } else {
    // Use Unity Hub to open with default/project version
    const args = ['--projectPath', `"${projectPath}"`];

    if (waitForExit) {
      await executeHubCommand(hubPath, args, logger);
      return `Project opened and Unity Editor has exited: ${projectPath}`;
    } else {
      // Non-blocking launch
      const quotedHubPath = `"${hubPath}"`;
      spawn(quotedHubPath, ['--', '--headless', ...args], {
        detached: true,
        stdio: 'ignore',
        shell: true,
        windowsHide: true
      }).unref();

      return `Launching Unity Editor with project: ${projectPath}`;
    }
  }

  return `Unity Editor launch initiated for: ${projectPath}`;
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

    case 'list_templates':
      if (!params.editorVersion) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "Parameter 'editorVersion' is required for list_templates"
        );
      }
      result = await executeHubCommand(hubPath, ['templates', '-v', params.editorVersion], logger);
      break;

    case 'create_project':
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

      const createArgs = [
        'create',
        '-p', `"${params.projectPath}"`,
        '-v', params.editorVersion
      ];

      if (params.template) {
        createArgs.push('-t', params.template);
      }

      result = await executeHubCommand(hubPath, createArgs, logger);

      // Check if project was created
      if (fs.existsSync(params.projectPath)) {
        result = JSON.stringify({
          success: true,
          message: `Project created successfully`,
          projectPath: params.projectPath,
          editorVersion: params.editorVersion,
          template: params.template || 'default'
        }, null, 2);
      } else {
        result = JSON.stringify({
          success: false,
          message: 'Project creation may have failed - directory not found',
          output: result
        }, null, 2);
      }
      break;

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
