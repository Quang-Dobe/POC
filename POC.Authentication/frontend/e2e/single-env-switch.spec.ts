import { test, expect } from '@playwright/test';
import {
  SPA_URL,
  KEYCLOAK_ISSUER,
  TEST_USER,
  TEST_PASSWORD,
  SEEDED_DISPLAY_STRING,
} from './constants';

test.describe('E2E-6: single ENV=DEV switch selects Keycloak AND OpenBAO', () => {
  test('login redirect targets Keycloak realm poc AND the message equals the OpenBAO seed', async ({
    page,
  }) => {

    await page.goto(SPA_URL + '/');
    await page.getByRole('button', { name: 'Sign in' }).click();
    await page.waitForURL(`${KEYCLOAK_ISSUER}/protocol/openid-connect/auth**`);

    expect(page.url().startsWith(`${KEYCLOAK_ISSUER}/`)).toBeTruthy();

    await page.locator('#username').fill(TEST_USER);
    await page.locator('#password').fill(TEST_PASSWORD);
    await page.locator('#kc-login').click();

    await page.waitForURL(`${SPA_URL}/message`);
    await expect(page.getByTestId('message')).toHaveText(SEEDED_DISPLAY_STRING);
  });
});
