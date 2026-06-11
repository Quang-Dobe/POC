import { config } from '@/config';
import { ApiError, UnauthorizedError } from '@/api/client';

export interface StreamHandlers {
  readonly onMessage: (chunk: string) => void;
  readonly onControl: (controlName: string) => void;
}

interface StreamRequestBody {
  readonly question: string;
  readonly session_id: string | null;
}

export async function openAskStream(
  question: string,
  handlers: StreamHandlers,
  signal?: AbortSignal,
): Promise<void> {
  const body: StreamRequestBody = { question, session_id: null };

  const response = await fetch(`${config.apiBaseUrl}/api/ask/stream`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
    signal,
  });

  if (response.status === 401) {
    throw new UnauthorizedError();
  }
  if (!response.ok || response.body === null) {
    throw new ApiError(`POST /api/ask/stream failed with status ${response.status}`);
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  for (;;) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }
    buffer += decoder.decode(value, { stream: true });
    buffer = drainFrames(buffer, handlers);
  }

  buffer += decoder.decode();
  drainFrames(buffer, handlers, true);
}

function drainFrames(buffer: string, handlers: StreamHandlers, flushLast = false): string {
  const normalized = buffer.replace(/\r\n/g, '\n');
  const parts = normalized.split('\n\n');

  const tail = flushLast ? '' : (parts.pop() ?? '');

  for (const part of parts) {
    dispatchFrame(part, handlers);
  }
  if (flushLast && tail.length > 0) {
    dispatchFrame(tail, handlers);
  }

  return tail;
}

function dispatchFrame(frame: string, handlers: StreamHandlers): void {
  const lines = frame.split('\n');
  let eventName = 'message';
  const dataLines: string[] = [];

  for (const line of lines) {
    if (line.startsWith('event:')) {
      eventName = line.slice('event:'.length).trim();
    } else if (line.startsWith('data:')) {
      const raw = line.slice('data:'.length);
      dataLines.push(raw.startsWith(' ') ? raw.slice(1) : raw);
    }
  }

  if (dataLines.length === 0 && eventName === 'message') {
    return;
  }

  const data = dataLines.join('\n');

  if (eventName === 'control') {
    handlers.onControl(data);
    return;
  }

  handlers.onMessage(data);
}

export async function postExtendSession(signal?: AbortSignal): Promise<boolean> {
  try {
    const response = await fetch(`${config.apiBaseUrl}/auth/extend-session`, {
      method: 'POST',
      credentials: 'include',
      signal,
    });
    return response.ok;
  } catch {
    return false;
  }
}
