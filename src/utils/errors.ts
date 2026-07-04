export enum ErrorType {
  CONNECTION = 'connection_error',
  TOOL_EXECUTION = 'tool_execution_error',
  RESOURCE_FETCH = 'resource_fetch_error',
  VALIDATION = 'validation_error',
  INTERNAL = 'internal_error',
  TIMEOUT = 'timeout_error',
  RAG_ERROR = 'rag_error',
  // XREAL-specific error types
  XREAL_SDK_NOT_FOUND = 'xreal_sdk_not_found',
  XREAL_DEVICE_NOT_CONNECTED = 'xreal_device_not_connected',
  XREAL_TRACKING_LOST = 'xreal_tracking_lost',
  XREAL_PERMISSION_DENIED = 'xreal_permission_denied',
  XREAL_FEATURE_NOT_SUPPORTED = 'xreal_feature_not_supported',
  XREAL_CONFIGURATION_ERROR = 'xreal_configuration_error',
  BUILD_ERROR = 'build_error',
  ADB_ERROR = 'adb_error'
}

export class McpUnityError extends Error {
  type: ErrorType;
  details?: any;

  constructor(type: ErrorType, message: string, details?: any) {
    super(message);
    this.type = type;
    this.details = details;
    this.name = 'McpUnityError';
  }

  toJSON() {
    return {
      type: this.type,
      message: this.message,
      details: this.details
    };
  }
}

export function handleError(error: any, context: string): McpUnityError {
  if (error instanceof McpUnityError) {
    return error;
  }

  // Handle standard errors
  return new McpUnityError(
    ErrorType.INTERNAL,
    `${context} error: ${error.message || 'Unknown error'}`,
    error
  );
}
