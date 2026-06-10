import { test, expect, request, type APIResponse } from '@playwright/test';
import { API_URL, SEEDED_DISPLAY_STRING } from './constants';

const MESSAGE_PATH = '/api/message';

async function expectRejectedNoLeak(response: APIResponse): Promise<void> {
  expect(response.status()).toBe(401);

  const body = await response.text();

  expect(body).not.toContain('"message"');

  expect(body).not.toContain(SEEDED_DISPLAY_STRING);

  const headerBlob = JSON.stringify(response.headers());
  expect(headerBlob).not.toContain(SEEDED_DISPLAY_STRING);
}

test.describe('E2E-1: anonymous /api/message is rejected', () => {
  test('rejects a request with no Authorization header (401, no leak)', async () => {
    const ctx = await request.newContext({ baseURL: API_URL });
    try {
      const response = await ctx.get(MESSAGE_PATH);
      await expectRejectedNoLeak(response);
    } finally {
      await ctx.dispose();
    }
  });

  test('rejects a request with a malformed Bearer token (401, no leak)', async () => {
    const ctx = await request.newContext({ baseURL: API_URL });
    try {
      const response = await ctx.get(MESSAGE_PATH, {
        headers: { Authorization: 'Bearer not-a-real-token' },
      });
      await expectRejectedNoLeak(response);
    } finally {
      await ctx.dispose();
    }
  });

  test('rejects a request with an empty Bearer token (401, no leak)', async () => {
    const ctx = await request.newContext({ baseURL: API_URL });
    try {
      const response = await ctx.get(MESSAGE_PATH, {
        headers: { Authorization: 'Bearer ' },
      });
      await expectRejectedNoLeak(response);
    } finally {
      await ctx.dispose();
    }
  });
});
