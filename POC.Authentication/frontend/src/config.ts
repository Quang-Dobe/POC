export interface AppConfig {

  readonly authority: string;

  readonly clientId: string;

  readonly scope: string;

  readonly apiBaseUrl: string;

  readonly redirectUri: string;
}

function required(key: keyof ImportMetaEnv): string {
  const value = import.meta.env[key];
  if (typeof value !== 'string' || value.length === 0) {
    throw new Error(`Missing required environment variable: ${key}`);
  }
  return value;
}

export const config: AppConfig = {
  authority: required('VITE_OIDC_AUTHORITY'),
  clientId: required('VITE_OIDC_CLIENT_ID'),
  scope: required('VITE_OIDC_SCOPE'),
  apiBaseUrl: required('VITE_API_BASE_URL'),
  redirectUri: required('VITE_OIDC_REDIRECT_URI'),
};
