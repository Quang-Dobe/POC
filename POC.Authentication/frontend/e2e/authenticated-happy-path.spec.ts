import { test, expect } from '@playwright/test';
import {
  SPA_URL,
  KEYCLOAK_ISSUER,
  TEST_USER,
  TEST_PASSWORD,
  SEEDED_DISPLAY_STRING,
} from './constants';

test.describe('E2E-2: authenticated happy path renders the seeded store string', () => {
  test('login via Keycloak -> /message shows the exact OpenBAO seed value', async ({ page }) => {

    await page.goto(SPA_URL + '/');
    await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
    await page.getByRole('button', { name: 'Sign in' }).click();

    await page.waitForURL(`${KEYCLOAK_ISSUER}/**`);
    await page.locator('#username').fill(TEST_USER);
    await page.locator('#password').fill(TEST_PASSWORD);
    await page.locator('#kc-login').click();

    await page.waitForURL(`${SPA_URL}/message`);

    const message = page.getByTestId('message');
    await expect(message).toBeVisible();
    await expect(message).toHaveText(SEEDED_DISPLAY_STRING);
  });
});
