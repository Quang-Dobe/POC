export interface AppConfig {

  readonly apiBaseUrl: string;
}

function required(key: keyof ImportMetaEnv): string {
  const value = import.meta.env[key];
  if (typeof value !== 'string' || value.length === 0) {
    throw new Error(`Missing required environment variable: ${key}`);
  }
  return value;
}

export const config: AppConfig = {
  apiBaseUrl: required('VITE_API_BASE_URL'),
};
