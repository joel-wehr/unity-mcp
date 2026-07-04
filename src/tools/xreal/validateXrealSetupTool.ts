import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'validate_xreal_setup';
const toolDescription = `Validates the Unity project configuration for XREAL development. Checks NRSDK installation, Android build settings, XR Plugin Management, required permissions, and reports any issues that need to be fixed.`;

const paramsSchema = z.object({
  checkNrsdk: z.boolean().default(true).describe('Validate NRSDK is properly installed'),
  checkAndroidSettings: z.boolean().default(true).describe('Validate Android build configuration'),
  checkXrPluginManagement: z.boolean().default(true).describe('Validate XR Plugin Management settings'),
  checkPermissions: z.boolean().default(true).describe('Validate required Android permissions are set'),
  checkSceneSetup: z.boolean().default(true).describe('Validate current scene has required XREAL components'),
  autoFix: z.boolean().default(false).describe('Attempt to automatically fix any issues found'),
});

/**
 * Registers the Validate XREAL Setup tool with the MCP server.
 * This tool checks project configuration for XREAL compatibility.
 */
export function registerValidateXrealSetupTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: z.infer<typeof paramsSchema>) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(mcpUnity: McpUnity, params: z.infer<typeof paramsSchema>): Promise<CallToolResult> {
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.XREAL_CONFIGURATION_ERROR,
      response.message || 'Failed to validate XREAL setup'
    );
  }

  // Format validation results
  const results = response.validationResults || {};
  const issues = response.issues || [];
  const warnings = response.warnings || [];
  const fixedIssues = response.fixedIssues || [];

  let resultText = '# XREAL Project Validation Report\n\n';

  // Overall status
  resultText += `## Status: ${issues.length === 0 ? 'PASSED' : 'ISSUES FOUND'}\n\n`;

  // Validation results
  resultText += '## Validation Results\n';
  if (results.nrsdk !== undefined) {
    resultText += `- NRSDK Installation: ${results.nrsdk ? '✓' : '✗'}\n`;
  }
  if (results.androidSettings !== undefined) {
    resultText += `- Android Settings: ${results.androidSettings ? '✓' : '✗'}\n`;
  }
  if (results.xrPluginManagement !== undefined) {
    resultText += `- XR Plugin Management: ${results.xrPluginManagement ? '✓' : '✗'}\n`;
  }
  if (results.permissions !== undefined) {
    resultText += `- Permissions: ${results.permissions ? '✓' : '✗'}\n`;
  }
  if (results.sceneSetup !== undefined) {
    resultText += `- Scene Setup: ${results.sceneSetup ? '✓' : '✗'}\n`;
  }

  // Issues
  if (issues.length > 0) {
    resultText += '\n## Issues\n';
    issues.forEach((issue: string, index: number) => {
      resultText += `${index + 1}. ${issue}\n`;
    });
  }

  // Warnings
  if (warnings.length > 0) {
    resultText += '\n## Warnings\n';
    warnings.forEach((warning: string, index: number) => {
      resultText += `${index + 1}. ${warning}\n`;
    });
  }

  // Fixed issues
  if (fixedIssues.length > 0) {
    resultText += '\n## Auto-Fixed Issues\n';
    fixedIssues.forEach((fixed: string, index: number) => {
      resultText += `${index + 1}. ${fixed}\n`;
    });
  }

  return {
    content: [{
      type: 'text' as const,
      text: resultText
    }]
  };
}
