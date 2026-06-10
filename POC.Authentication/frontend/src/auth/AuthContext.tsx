import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import type { User } from 'oidc-client-ts';
import { userManager } from '@/auth/userManager';

export interface AuthContextValue {

  readonly isAuthenticated: boolean;

  readonly isLoading: boolean;

  readonly login: () => Promise<void>;

  readonly completeLogin: () => Promise<void>;

  readonly logout: () => Promise<void>;

  readonly getAccessToken: () => Promise<string | null>;

  readonly getUser: () => Promise<User | null>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export async function getAccessToken(): Promise<string | null> {
  const current = await userManager.getUser();
  if (current === null || current.expired === true) {
    return null;
  }
  return current.access_token;
}

export function AuthProvider({ children }: { children: ReactNode }): JSX.Element {
  const [user, setUser] = useState<User | null>(null);

  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    let active = true;
    void userManager
      .getUser()
      .then((loaded) => {
        if (active) {
          setUser(loaded);
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

  const login = useCallback(async (): Promise<void> => {
    await userManager.signinRedirect();
  }, []);

  const completeLogin = useCallback(async (): Promise<void> => {
    const loaded = await userManager.signinRedirectCallback();
    setUser(loaded);
  }, []);

  const logout = useCallback(async (): Promise<void> => {
    await userManager.signoutRedirect();
    setUser(null);
  }, []);

  const getUser = useCallback((): Promise<User | null> => {
    return userManager.getUser();
  }, []);

  const getAccessTokenCb = useCallback((): Promise<string | null> => {
    return getAccessToken();
  }, []);

  const isAuthenticated = user !== null && user.expired !== true;

  const value = useMemo<AuthContextValue>(
    () => ({
      isAuthenticated,
      isLoading,
      login,
      completeLogin,
      logout,
      getAccessToken: getAccessTokenCb,
      getUser,
    }),
    [isAuthenticated, isLoading, login, completeLogin, logout, getAccessTokenCb, getUser],
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
