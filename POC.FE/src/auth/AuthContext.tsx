import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { config } from '@/config';

export interface MeResponse {
  readonly displayName: string;
  readonly roles: readonly string[];
}

export interface AuthContextValue {

  readonly isAuthenticated: boolean;

  readonly isLoading: boolean;

  readonly me: MeResponse | null;

  readonly login: () => void;

  readonly logout: () => Promise<void>;

  readonly recheck: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

async function fetchMe(): Promise<MeResponse | null> {
  let response: Response;
  try {
    response = await fetch(`${config.apiBaseUrl}/auth/me`, {
      method: 'GET',
      credentials: 'include',
    });
  } catch {
    return null;
  }

  if (!response.ok) {
    return null;
  }

  const body: unknown = await response.json().catch(() => null);
  if (
    typeof body === 'object' &&
    body !== null &&
    'displayName' in body &&
    'roles' in body &&
    typeof (body as { displayName: unknown }).displayName === 'string' &&
    Array.isArray((body as { roles: unknown }).roles)
  ) {
    return {
      displayName: (body as { displayName: string }).displayName,
      roles: ((body as { roles: unknown[] }).roles).filter(
        (role): role is string => typeof role === 'string',
      ),
    };
  }
  return null;
}

export function AuthProvider({ children }: { children: ReactNode }): JSX.Element {
  const [me, setMe] = useState<MeResponse | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const recheck = useCallback(async (): Promise<void> => {
    const loaded = await fetchMe();
    setMe(loaded);
  }, []);

  useEffect(() => {
    let active = true;
    void fetchMe()
      .then((loaded) => {
        if (active) {
          setMe(loaded);
        }
      })
      .finally(() => {
        if (active) {
          setIsLoading(false);
        }
      });
    return () => {
      active = false;
    };
  }, []);

  const login = useCallback((): void => {
    window.location.assign(`${config.apiBaseUrl}/auth/login`);
  }, []);

  const logout = useCallback(async (): Promise<void> => {
    try {
      await fetch(`${config.apiBaseUrl}/auth/logout`, {
        method: 'POST',
        credentials: 'include',
      });
    } finally {
      setMe(null);
    }
  }, []);

  const isAuthenticated = me !== null;

  const value = useMemo<AuthContextValue>(
    () => ({
      isAuthenticated,
      isLoading,
      me,
      login,
      logout,
      recheck,
    }),
    [isAuthenticated, isLoading, me, login, logout, recheck],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (context === null) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
