import { render, renderHook, screen, waitFor } from '@testing-library/react';
import { type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { User } from 'oidc-client-ts';

vi.mock('@/auth/userManager', () => ({
  userManager: {
    getUser: vi.fn(),
    signinRedirect: vi.fn(),
    signinRedirectCallback: vi.fn(),
    signoutRedirect: vi.fn(),
  },
}));

import { userManager } from '@/auth/userManager';
import { AuthProvider, useAuth } from '@/auth/AuthContext';

const mockManager = vi.mocked(userManager);

function makeUser(overrides: Partial<User> = {}): User {
  return {
    access_token: 'access-token-abc',
    expired: false,
    ...overrides,
  } as unknown as User;
}

function wrapper({ children }: { children: ReactNode }): JSX.Element {
  return <AuthProvider>{children}</AuthProvider>;
}

beforeEach(() => {
  mockManager.getUser.mockResolvedValue(null);
  mockManager.signinRedirect.mockResolvedValue(undefined);
  mockManager.signoutRedirect.mockResolvedValue(undefined);
});

afterEach(() => {
  vi.clearAllMocks();
});

describe('useAuth', () => {
  it('throws when used outside an AuthProvider', () => {
    expect(() => renderHook(() => useAuth())).toThrow('useAuth must be used within an AuthProvider');
  });

  it('login() delegates to userManager.signinRedirect', async () => {
    const { result } = renderHook(() => useAuth(), { wrapper });

    await result.current.login();

    expect(mockManager.signinRedirect).toHaveBeenCalledTimes(1);
  });

  it('logout() delegates to userManager.signoutRedirect', async () => {
    const { result } = renderHook(() => useAuth(), { wrapper });

    await result.current.logout();

    expect(mockManager.signoutRedirect).toHaveBeenCalledTimes(1);
  });

  it('completeLogin() loads the user via signinRedirectCallback and flips isAuthenticated', async () => {
    mockManager.signinRedirectCallback.mockResolvedValue(makeUser());
    const { result } = renderHook(() => useAuth(), { wrapper });

    await result.current.completeLogin();

    expect(mockManager.signinRedirectCallback).toHaveBeenCalledTimes(1);
    await waitFor(() => {
      expect(result.current.isAuthenticated).toBe(true);
    });
  });

  it('getAccessToken() returns the stored token for a valid user', async () => {
    mockManager.getUser.mockResolvedValue(makeUser({ access_token: 'tok-123' }));
    const { result } = renderHook(() => useAuth(), { wrapper });

    await expect(result.current.getAccessToken()).resolves.toBe('tok-123');
  });

  it('getAccessToken() returns null when there is no user', async () => {
    mockManager.getUser.mockResolvedValue(null);
    const { result } = renderHook(() => useAuth(), { wrapper });

    await expect(result.current.getAccessToken()).resolves.toBeNull();
  });

  it('getAccessToken() returns null when the stored user is expired', async () => {
    mockManager.getUser.mockResolvedValue(makeUser({ expired: true }));
    const { result } = renderHook(() => useAuth(), { wrapper });

    await expect(result.current.getAccessToken()).resolves.toBeNull();
  });
});

describe('AuthProvider', () => {
  it('isAuthenticated is false when no session is persisted on mount', async () => {
    mockManager.getUser.mockResolvedValue(null);
    const { result } = renderHook(() => useAuth(), { wrapper });

    await waitFor(() => {
      expect(mockManager.getUser).toHaveBeenCalled();
    });
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('isLoading starts true and flips to false once the initial getUser resolves', async () => {
    mockManager.getUser.mockResolvedValue(null);
    const { result } = renderHook(() => useAuth(), { wrapper });

    expect(result.current.isLoading).toBe(true);

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });
  });

  it('isAuthenticated reflects a persisted, non-expired user loaded on mount', async () => {
    mockManager.getUser.mockResolvedValue(makeUser());

    function Probe(): JSX.Element {
      const { isAuthenticated } = useAuth();
      return <span>{isAuthenticated ? 'authed' : 'anon'}</span>;
    }

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );

    expect(await screen.findByText('authed')).toBeInTheDocument();
  });
});
