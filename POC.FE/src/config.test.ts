import { afterEach, describe, expect, it, vi } from 'vitest';

const DEV_ENV = {
  VITE_API_BASE_URL: 'http://localhost:5000',
} as const;

const PROD_ENV = {
  VITE_API_BASE_URL: 'https://api.example.com',
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
  it('maps the DEV VITE_API_BASE_URL onto the flat config object (sole surviving member)', async () => {
    stubEnv(DEV_ENV);

    const config = await loadConfig();

    expect(config).toStrictEqual({
      apiBaseUrl: 'http://localhost:5000',
    });
  });

  it('maps the PROD VITE_API_BASE_URL onto the flat config object', async () => {
    stubEnv(PROD_ENV);

    const config = await loadConfig();

    expect(config.apiBaseUrl).toBe('https://api.example.com');
  });

  it('throws when VITE_API_BASE_URL is missing', async () => {
    stubEnv(DEV_ENV);
    vi.stubEnv('VITE_API_BASE_URL', '');

    await expect(loadConfig()).rejects.toThrow('VITE_API_BASE_URL');
  });
});
