import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/config', () => ({
  config: { apiBaseUrl: 'https://api.test' },
}));

import { ApiError, UnauthorizedError, askAgent, createInvite, fetchMessage } from '@/api/client';

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as unknown as Response;
}

beforeEach(() => {
  vi.stubGlobal('fetch', vi.fn());
});

afterEach(() => {
  vi.clearAllMocks();
  vi.unstubAllGlobals();
});

describe('fetchMessage', () => {
  it('returns the message string with credentials:include and NO Authorization header', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(jsonResponse({ message: 'hello from the vault' }));

    await expect(fetchMessage()).resolves.toBe('hello from the vault');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('https://api.test/api/message');
    expect((init as RequestInit).credentials).toBe('include');
    expect((init as RequestInit).headers).toBeUndefined();
  });

  it('throws UnauthorizedError on a 401 (re-login signal)', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({}, 401));

    await expect(fetchMessage()).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('throws ApiError on a non-401 error response', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({}, 500));

    await expect(fetchMessage()).rejects.toBeInstanceOf(ApiError);
  });

  it('throws ApiError when the response shape is unexpected', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ notMessage: 1 }));

    await expect(fetchMessage()).rejects.toBeInstanceOf(ApiError);
  });
});

describe('createInvite', () => {
  it('POSTs the email as username with credentials:include and parses the result', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(
      jsonResponse({
        subject: 'erin@example.com',
        alreadyExisted: false,
        passwordSet: true,
      }),
    );

    await expect(createInvite('erin@example.com')).resolves.toEqual({
      subject: 'erin@example.com',
      alreadyExisted: false,
      passwordSet: true,
    });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('https://api.test/auth/invite');
    expect((init as RequestInit).method).toBe('POST');
    expect((init as RequestInit).credentials).toBe('include');
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      username: 'erin@example.com',
    });
  });

  it('throws UnauthorizedError on a 401', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({}, 401));

    await expect(createInvite('erin@example.com')).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('throws ApiError on a 403 (non-manager)', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({}, 403));

    await expect(createInvite('erin@example.com')).rejects.toBeInstanceOf(ApiError);
  });

  it('throws ApiError when the response shape is unexpected', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ subject: 'only' }));

    await expect(createInvite('erin@example.com')).rejects.toBeInstanceOf(ApiError);
  });
});

describe('askAgent', () => {
  it('POSTs question + session_id with credentials:include, NO Authorization header', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(jsonResponse({ answer: '42', session_id: 'sess-9' }));

    await expect(askAgent('meaning of life?', 'sess-1')).resolves.toEqual({
      answer: '42',
      sessionId: 'sess-9',
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('https://api.test/api/ask');
    expect((init as RequestInit).method).toBe('POST');
    expect((init as RequestInit).credentials).toBe('include');
    const headers = (init as RequestInit).headers as Record<string, string>;
    expect(headers.Authorization).toBeUndefined();
    expect(headers['Content-Type']).toBe('application/json');
    expect(JSON.parse((init as RequestInit).body as string)).toEqual({
      question: 'meaning of life?',
      session_id: 'sess-1',
    });
  });

  it('sends session_id null on the first ask', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(jsonResponse({ answer: 'hi', session_id: 'sess-1' }));

    await askAgent('first?', null);

    const [, init] = fetchMock.mock.calls[0];
    expect(JSON.parse((init as RequestInit).body as string).session_id).toBeNull();
  });

  it('throws UnauthorizedError on a 401 (re-login signal)', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({}, 401));

    await expect(askAgent('q', null)).rejects.toBeInstanceOf(UnauthorizedError);
  });

  it('throws ApiError on a non-401 error response', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({}, 500));

    await expect(askAgent('q', null)).rejects.toBeInstanceOf(ApiError);
  });

  it('throws ApiError when the response shape is unexpected', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ answer: 'no session id' }));

    await expect(askAgent('q', null)).rejects.toBeInstanceOf(ApiError);
  });
});
