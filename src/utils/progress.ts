import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from './logger.js';

/**
 * The subset of the MCP SDK's `RequestHandlerExtra` that our long-running tools
 * need. Tool callbacks receive this as their second argument. We type it loosely
 * so this helper stays decoupled from a specific SDK version.
 */
export interface ProgressCapableExtra {
  signal?: AbortSignal;
  _meta?: { progressToken?: string | number } & Record<string, unknown>;
  sendNotification?: (notification: {
    method: 'notifications/progress';
    params: {
      progressToken: string | number;
      progress: number;
      total?: number;
      message?: string;
    };
  }) => Promise<void>;
}

export interface UnityProgressRequest {
  method: string;
  params: any;
  timeout?: number;
}

export interface ProgressOptions {
  /** How often to emit a heartbeat progress notification, in ms. Default 2000. */
  intervalMs?: number;
  /**
   * A soft estimate of how long the op takes, in ms. When provided, progress is
   * reported as elapsed/estimate (a determinate-looking bar that never quite
   * reaches 100% until completion). When omitted, progress is an ever-increasing
   * elapsed-seconds counter (indeterminate).
   */
  estimatedMs?: number;
  /** Human-readable label for the operation, shown in progress messages. */
  label?: string;
}

/**
 * Sends a request to Unity while (a) forwarding the MCP client's cancellation
 * signal so an aborted call stops waiting immediately, and (b) emitting periodic
 * progress notifications for the lifetime of the call — but ONLY when the client
 * supplied a `progressToken` (per the MCP spec, progress is opt-in).
 *
 * Unity answers each JSON-RPC request with a single response (no intermediate
 * progress), so these heartbeats are time-based rather than driven by real Unity
 * progress. They keep the client informed that the op is alive and give it a
 * cancel affordance. True Unity-side progress/cancel needs plugin support (roadmap).
 *
 * The returned promise resolves/rejects exactly as `mcpUnity.sendRequest` would.
 */
export async function sendUnityRequestWithProgress(
  mcpUnity: McpUnity,
  request: UnityProgressRequest,
  extra: ProgressCapableExtra | undefined,
  logger: Logger,
  opts: ProgressOptions = {}
): Promise<any> {
  const progressToken = extra?._meta?.progressToken;
  const sendNotification = extra?.sendNotification;
  const intervalMs = opts.intervalMs ?? 2000;
  const estimatedMs = opts.estimatedMs;
  const label = opts.label ?? request.method;
  const reportProgress = progressToken !== undefined && typeof sendNotification === 'function';

  let interval: ReturnType<typeof setInterval> | undefined;
  let elapsedMs = 0;
  // A strictly-monotonic progress value (the MCP spec requires it to increase on
  // every notification). For the determinate case it is elapsed ms; for the
  // indeterminate case it is a tick counter.
  let lastProgress = 0;

  const emit = (progress: number, message: string) => {
    if (!reportProgress) return;
    lastProgress = progress;
    // Fire-and-forget: a failed progress notification must never break the tool.
    void sendNotification!({
      method: 'notifications/progress',
      params: {
        progressToken: progressToken!,
        progress,
        ...(estimatedMs ? { total: estimatedMs } : {}),
        message,
      },
    }).catch((err) => logger.debug(`progress notification failed: ${err instanceof Error ? err.message : String(err)}`));
  };

  if (reportProgress) {
    emit(0, `${label}: started`);
    interval = setInterval(() => {
      elapsedMs += intervalMs;
      // Determinate bar: cap just under the estimate so it never reads "done"
      // before Unity actually replies. Indeterminate: keep counting up.
      const progress = estimatedMs
        ? Math.min(elapsedMs, Math.floor(estimatedMs * 0.99))
        : elapsedMs;
      emit(progress, `${label}: ${Math.round(elapsedMs / 1000)}s elapsed…`);
    }, intervalMs);
  }

  try {
    return await mcpUnity.sendRequest({ ...request, signal: extra?.signal });
  } finally {
    if (interval) clearInterval(interval);
    // Final tick at 100%. Ensure it's strictly greater than the last emitted value.
    const finalProgress = estimatedMs ? estimatedMs : lastProgress + intervalMs;
    emit(finalProgress, `${label}: complete`);
  }
}
