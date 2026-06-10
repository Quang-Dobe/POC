export const SPA_URL = 'http://localhost:5173';
export const API_URL = 'http://localhost:5000';
export const KEYCLOAK_URL = 'https://localhost:8080';

export const REALM = 'poc';
export const KEYCLOAK_ISSUER = `${KEYCLOAK_URL}/realms/${REALM}`;
export const KEYCLOAK_TOKEN_ENDPOINT = `${KEYCLOAK_ISSUER}/protocol/openid-connect/token`;

export const TEST_USER = 'testuser';
export const TEST_PASSWORD = 'Test1234!';

export const SEEDED_DISPLAY_STRING = 'Hello from OpenBAO (DEV)';
