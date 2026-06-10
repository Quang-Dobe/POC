import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/auth/AuthContext', () => ({
  getAccessToken: vi.fn(),
}));

vi.mock('@/config', () => ({
  config: { apiBaseUrl: 'https://api.test' },
}));

import { getAccessToken } from '@/auth/AuthContext';
import { ApiError, NotAuthenticatedError, askAgent, fetchMessage } from '@/api/client';

const mockGetAccessToken = vi.mocked(getAccessToken);

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
  it('returns the message string and sends the Bearer token on the happy path', async () => {
    mockGetAccessToken.mockResolvedValue('tok-123');
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(jsonResponse({ message: 'hello from the vault' }));

    await expect(fetchMessage()).resolves.toBe('hello from the vault');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('https://api.test/api/message');
    expect((init?.headers as Record<string, string>).Authorization).toBe('Bearer tok-123');
  });

  it('throws NotAuthenticatedError when there is no token (never sends Bearer null)', async () => {
    mockGetAccessToken.mockResolvedValue(null);
    const fetchMock = vi.mocked(fetch);

    await expect(fetchMessage()).rejects.toBeInstanceOf(NotAuthenticatedError);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('throws ApiError on a non-200 response', async () => {
    mockGetAccessToken.mockResolvedValue('tok-123');
    vi.mocked(fetch).mockResolvedValue(jsonResponse({}, 401));

    await expect(fetchMessage()).rejects.toBeInstanceOf(ApiError);
  });

  it('throws ApiError when the response shape is unexpected', async () => {
    mockGetAccessToken.mockResolvedValue('tok-123');
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ notMessage: 1 }));

    await expect(fetchMessage()).rejects.toBeInstanceOf(ApiError);
  });
});

describe('askAgent', () => {
  it('POSTs the question + session_id with Bearer token and returns the parsed result', async () => {
    mockGetAccessToken.mockResolvedValue('tok-123');
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(jsonResponse({ answer: '42', session_id: 'sess-9' }));

    await expect(askAgent('meaning of life?', 'sess-1')).resolves.toEqual({
      answer: '42',
      sessionId: 'sess-9',
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('https://api.test/api/ask');
    expect(init?.method).toBe('POST');
    expect((init?.headers as Record<string, string>).Authorization).toBe('Bearer tok-123');
    expect(JSON.parse(init?.body as string)).toEqual({
      question: 'meaning of life?',
      session_id: 'sess-1',
    });
  });

  it('sends session_id null on the first ask', async () => {
    mockGetAccessToken.mockResolvedValue('tok-123');
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(jsonResponse({ answer: 'hi', session_id: 'sess-1' }));

    await askAgent('first?', null);

    const [, init] = fetchMock.mock.calls[0];
    expect(JSON.parse(init?.body as string).session_id).toBeNull();
  });

  it('throws NotAuthenticatedError when there is no token (never sends Bearer null)', async () => {
    mockGetAccessToken.mockResolvedValue(null);
    const fetchMock = vi.mocked(fetch);

    await expect(askAgent('q', null)).rejects.toBeInstanceOf(NotAuthenticatedError);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('throws ApiError on a non-200 response', async () => {
    mockGetAccessToken.mockResolvedValue('tok-123');
    vi.mocked(fetch).mockResolvedValue(jsonResponse({}, 500));

    await expect(askAgent('q', null)).rejects.toBeInstanceOf(ApiError);
  });

  it('throws ApiError when the response shape is unexpected', async () => {
    mockGetAccessToken.mockResolvedValue('tok-123');
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ answer: 'no session id' }));

    await expect(askAgent('q', null)).rejects.toBeInstanceOf(ApiError);
  });
});
