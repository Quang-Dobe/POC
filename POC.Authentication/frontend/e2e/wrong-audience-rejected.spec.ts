import { test, expect, request, type APIRequestContext } from '@playwright/test';
import {
  API_URL,
  KEYCLOAK_TOKEN_ENDPOINT,
  TEST_USER,
  TEST_PASSWORD,
} from './constants';

const AUDIENCE = 'poc-api';

interface TokenResponse {
  readonly access_token: string;
}

function decodeJwtPayload(jwt: string): Record<string, unknown> | null {
  const parts = jwt.split('.');
  if (parts.length !== 3) {
    return null;
  }

  const b64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
  const json = Buffer.from(b64, 'base64').toString('utf8');
  const parsed: unknown = JSON.parse(json);
  return typeof parsed === 'object' && parsed !== null
    ? (parsed as Record<string, unknown>)
    : null;
}

function audienceIncludes(payload: Record<string, unknown>, audience: string): boolean {
  const aud = payload['aud'];
  if (typeof aud === 'string') {
    return aud === audience;
  }
  if (Array.isArray(aud)) {
    return aud.includes(audience);
  }
  return false;
}

async function mintAdminCliToken(ctx: APIRequestContext): Promise<string> {
  const response = await ctx.post(KEYCLOAK_TOKEN_ENDPOINT, {
    form: {
      grant_type: 'password',
      client_id: 'admin-cli',
      username: TEST_USER,
      password: TEST_PASSWORD,
    },
  });

  expect(
    response.ok(),
    `Expected admin-cli password grant to succeed (HTTP ${response.status()}). If direct ` +
      `access grants are disabled for admin-cli in realm poc, fall back to the account client.`,
  ).toBeTruthy();

  const body = (await response.json()) as TokenResponse;
  expect(typeof body.access_token).toBe('string');
  return body.access_token;
}

test.describe('E2E-4: wrong-audience token is rejected', () => {
  test('a Keycloak-signed token lacking aud=poc-api is rejected with 401', async () => {

    const kcCtx = await request.newContext({ ignoreHTTPSErrors: true });
    const apiCtx = await request.newContext({ baseURL: API_URL });
    try {
      const token = await mintAdminCliToken(kcCtx);

      const payload = decodeJwtPayload(token);
      expect(payload, 'minted token payload should be decodable').not.toBeNull();
      expect(
        audienceIncludes(payload as Record<string, unknown>, AUDIENCE),
        `the admin-cli token must NOT carry aud=${AUDIENCE} for this test to be meaningful`,
      ).toBeFalsy();

      const response = await apiCtx.get('/api/message', {
        headers: { Authorization: `Bearer ${token}` },
      });

      expect(response.status()).toBe(401);
      const responseBody = await response.text();
      expect(responseBody).not.toContain('"message"');
    } finally {
      await kcCtx.dispose();
      await apiCtx.dispose();
    }
  });
});
