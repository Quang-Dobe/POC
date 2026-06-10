import { afterEach, describe, expect, it, vi } from 'vitest';

const DEV_ENV = {
  VITE_OIDC_AUTHORITY: 'https://localhost:8080/realms/poc',
  VITE_OIDC_CLIENT_ID: 'poc-spa',
  VITE_OIDC_SCOPE: 'openid profile poc-api',
  VITE_API_BASE_URL: 'http://localhost:5000',
  VITE_OIDC_REDIRECT_URI: 'http://localhost:5173/callback',
} as const;

const PROD_ENV = {
  VITE_OIDC_AUTHORITY: 'https://login.microsoftonline.com/tenant-123/v2.0',
  VITE_OIDC_CLIENT_ID: 'spa-client-id',
  VITE_OIDC_SCOPE: 'openid profile api://api-client-id/access',
  VITE_API_BASE_URL: 'https://api.example.com',
  VITE_OIDC_REDIRECT_URI: 'https://app.example.com/callback',
} as const;

function stubEnv(env: Record<string, string>): void {
  for (const [key, value] of Object.entries(env)) {
    vi.stubEnv(key, value);
  }
}

async function loadConfig(): Promise<typeof import('@/config').config> {
  vi.resetModules();
  const module = await import('@/config');
  return module.config;
}

afterEach(() => {
  vi.unstubAllEnvs();
});

describe('config', () => {
  it('maps the DEV (Keycloak) VITE_* values onto the flat config object', async () => {
    stubEnv(DEV_ENV);

    const config = await loadConfig();

    expect(config).toStrictEqual({
      authority: 'https://localhost:8080/realms/poc',
      clientId: 'poc-spa',
      scope: 'openid profile poc-api',
      apiBaseUrl: 'http://localhost:5000',
      redirectUri: 'http://localhost:5173/callback',
    });
  });

  it('maps the PROD (Entra) VITE_* values onto the flat config object', async () => {
    stubEnv(PROD_ENV);

    const config = await loadConfig();

    expect(config.authority).toBe('https://login.microsoftonline.com/tenant-123/v2.0');
    expect(config.scope).toBe('openid profile api://api-client-id/access');
    expect(config.clientId).toBe('spa-client-id');
  });

  it('throws when a required VITE_* variable is missing', async () => {
    stubEnv(DEV_ENV);
    vi.stubEnv('VITE_OIDC_AUTHORITY', '');

    await expect(loadConfig()).rejects.toThrow('VITE_OIDC_AUTHORITY');
  });
});
