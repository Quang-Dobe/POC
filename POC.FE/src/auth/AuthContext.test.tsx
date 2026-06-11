import { render, renderHook, screen, waitFor } from '@testing-library/react';
import { type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/config', () => ({
  config: { apiBaseUrl: 'https://api.test' },
}));

import { AuthProvider, useAuth } from '@/auth/AuthContext';

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as unknown as Response;
}

function wrapper({ children }: { children: ReactNode }): JSX.Element {
  return <AuthProvider>{children}</AuthProvider>;
}

let assignedHref = '';

beforeEach(() => {
  assignedHref = '';
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(null, 401)));
  vi.stubGlobal('location', {
    assign: vi.fn((url: string) => {
      assignedHref = url;
    }),
  });
});

afterEach(() => {
  vi.clearAllMocks();
  vi.unstubAllGlobals();
});

describe('useAuth', () => {
  it('throws when used outside an AuthProvider', () => {
    expect(() => renderHook(() => useAuth())).toThrow('useAuth must be used within an AuthProvider');
  });

  it('login() redirects the whole browser to BE /auth/login (no client-side OIDC)', () => {
    const { result } = renderHook(() => useAuth(), { wrapper });

    result.current.login();

    expect(assignedHref).toBe('https://api.test/auth/login');
  });

  it('logout() POSTs /auth/logout with credentials and flips to anonymous', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValueOnce(jsonResponse({ displayName: 'Ada', roles: ['reader'] }));
    const { result } = renderHook(() => useAuth(), { wrapper });

    await waitFor(() => {
      expect(result.current.isAuthenticated).toBe(true);
    });

    await result.current.logout();

    const logoutCall = fetchMock.mock.calls.find(([url]) => url === 'https://api.test/auth/logout');
    expect(logoutCall).toBeDefined();
    expect((logoutCall?.[1] as RequestInit).method).toBe('POST');
    expect((logoutCall?.[1] as RequestInit).credentials).toBe('include');
    await waitFor(() => {
      expect(result.current.isAuthenticated).toBe(false);
    });
  });
});

describe('AuthProvider', () => {
  it('isAuthenticated reflects /auth/me: 200 -> true, no token exposed', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ displayName: 'Ada', roles: ['manager'] }));

    function Probe(): JSX.Element {
      const auth = useAuth();
      return (
        <span>
          {auth.isAuthenticated ? 'authed' : 'anon'}:{auth.me?.displayName ?? '-'}
        </span>
      );
    }

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );

    expect(await screen.findByText('authed:Ada')).toBeInTheDocument();
  });

  it('isAuthenticated is false when /auth/me returns 401 (no live session)', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, 401));
    const { result } = renderHook(() => useAuth(), { wrapper });

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });
    expect(result.current.isAuthenticated).toBe(false);
    expect(result.current.me).toBeNull();
  });

  it('the /auth/me request uses credentials:include and no Authorization header', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(jsonResponse({ displayName: 'Ada', roles: [] }));
    renderHook(() => useAuth(), { wrapper });

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalled();
    });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('https://api.test/auth/me');
    expect((init as RequestInit).credentials).toBe('include');
    expect((init as RequestInit).headers).toBeUndefined();
  });

  it('isLoading starts true and flips to false once /auth/me resolves', async () => {
    vi.mocked(fetch).mockResolvedValue(jsonResponse(null, 401));
    const { result } = renderHook(() => useAuth(), { wrapper });

    expect(result.current.isLoading).toBe(true);

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });
  });

  it('recheck() re-fetches /auth/me and flips isAuthenticated to false on a now-dead session', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValueOnce(jsonResponse({ displayName: 'Ada', roles: ['reader'] }));
    const { result } = renderHook(() => useAuth(), { wrapper });

    await waitFor(() => {
      expect(result.current.isAuthenticated).toBe(true);
    });

    fetchMock.mockResolvedValueOnce(jsonResponse(null, 401));
    await result.current.recheck();

    await waitFor(() => {
      expect(result.current.isAuthenticated).toBe(false);
    });
  });
});
