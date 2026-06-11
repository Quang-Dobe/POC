import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/config', () => ({
  config: { apiBaseUrl: 'https://api.test' },
}));

import { ApiError, UnauthorizedError } from '@/api/client';
import { openAskStream, postExtendSession, type StreamHandlers } from '@/api/stream';

const encoder = new TextEncoder();

function streamResponse(frames: readonly string[], status = 200): Response {
  let index = 0;
  const reader = {
    read: (): Promise<{ done: boolean; value?: Uint8Array }> => {
      if (index < frames.length) {
        const value = encoder.encode(frames[index]);
        index += 1;
        return Promise.resolve({ done: false, value });
      }
      return Promise.resolve({ done: true, value: undefined });
    },
  };
  return {
    ok: status >= 200 && status < 300,
    status,
    body: { getReader: () => reader },
  } as unknown as Response;
}

function nonBodyResponse(status: number): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    body: null,
  } as unknown as Response;
}

function collectHandlers(): { handlers: StreamHandlers; messages: string[]; controls: string[] } {
  const messages: string[] = [];
  const controls: string[] = [];
  return {
    messages,
    controls,
    handlers: {
      onMessage: (chunk) => messages.push(chunk),
      onControl: (name) => controls.push(name),
    },
  };
}

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn());
});

afterEach(() => {
  vi.clearAllMocks();
  vi.unstubAllGlobals();
});

describe('openAskStream', () => {
  it('opens the stream with credentials:include, POST, session_id null, no Authorization', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(streamResponse(['data: hi\n\n']));
    const { handlers } = collectHandlers();

    await openAskStream('a question', handlers);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('https://api.test/api/ask/stream');
    expect((init as RequestInit).method).toBe('POST');
    expect((init as RequestInit).credentials).toBe('include');
    const headers = (init as RequestInit).headers as Record<string, string>;
    expect(headers.Authorization).toBeUndefined();
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      question: 'a question',
      session_id: null,
    });
  });

  it('parses message frames into chunks and resolves on EOF (completion, not error)', async () => {
    vi.mocked(fetch).mockResolvedValue(
      streamResponse(['data: Hello\n\n', 'data: world\n\n']),
    );
    const { handlers, messages } = collectHandlers();

    await expect(openAskStream('q', handlers)).resolves.toBeUndefined();
    expect(messages).toEqual(['Hello', 'world']);
  });

  it('buffers across network reads that split a frame mid-way', async () => {
    vi.mocked(fetch).mockResolvedValue(
      streamResponse(['data: Hel', 'lo\n\ndata: wor', 'ld\n\n']),
    );
    const { handlers, messages } = collectHandlers();

    await openAskStream('q', handlers);
    expect(messages).toEqual(['Hello', 'world']);
  });

  it('rejoins a multi-line data frame on \\n (a chunk that contained a newline)', async () => {
    vi.mocked(fetch).mockResolvedValue(
      streamResponse(['data: line one\ndata: line two\n\n']),
    );
    const { handlers, messages } = collectHandlers();

    await openAskStream('q', handlers);
    expect(messages).toEqual(['line one\nline two']);
  });

  it('routes a control:extend-session frame to onControl, not onMessage', async () => {
    vi.mocked(fetch).mockResolvedValue(
      streamResponse(['data: chunk\n\n', 'event: control\ndata: extend-session\n\n']),
    );
    const { handlers, messages, controls } = collectHandlers();

    await openAskStream('q', handlers);
    expect(messages).toEqual(['chunk']);
    expect(controls).toEqual(['extend-session']);
  });

  it('finalizes a chunk that arrived without a trailing blank line at EOF', async () => {
    vi.mocked(fetch).mockResolvedValue(streamResponse(['data: partial']));
    const { handlers, messages } = collectHandlers();

    await openAskStream('q', handlers);
    expect(messages).toEqual(['partial']);
  });

  it('resolves on an immediate EOF with zero chunks (a clean empty completion)', async () => {
    vi.mocked(fetch).mockResolvedValue(streamResponse([]));
    const { handlers, messages } = collectHandlers();

    await expect(openAskStream('q', handlers)).resolves.toBeUndefined();
    expect(messages).toEqual([]);
  });

  it('throws UnauthorizedError on a pre-commit 401 (re-login signal)', async () => {
    vi.mocked(fetch).mockResolvedValue(nonBodyResponse(401));
    const { handlers } = collectHandlers();

    await expect(openAskStream('q', handlers)).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('throws ApiError on a pre-commit non-200 (e.g. 502 -> retry message)', async () => {
    vi.mocked(fetch).mockResolvedValue(nonBodyResponse(502));
    const { handlers } = collectHandlers();

    await expect(openAskStream('q', handlers)).rejects.toBeInstanceOf(ApiError);
  });
});

describe('postExtendSession', () => {
  it('returns true when /auth/extend-session is extended (204/2xx)', async () => {
    vi.mocked(fetch).mockResolvedValue({ ok: true, status: 204 } as unknown as Response);

    await expect(postExtendSession()).resolves.toBe(true);
    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe('https://api.test/auth/extend-session');
    expect((init as RequestInit).method).toBe('POST');
    expect((init as RequestInit).credentials).toBe('include');
  });

  it('returns false (silent) on a dead-session 401 - no throw, no retry', async () => {
    vi.mocked(fetch).mockResolvedValue({ ok: false, status: 401 } as unknown as Response);

    await expect(postExtendSession()).resolves.toBe(false);
  });
});
