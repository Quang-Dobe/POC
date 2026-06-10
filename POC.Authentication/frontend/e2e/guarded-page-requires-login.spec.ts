import { test, expect } from '@playwright/test';
import { SPA_URL } from './constants';

test.describe('E2E-3: guarded /message requires login', () => {
  test('direct navigation with no session redirects to landing, shows no message', async ({
    page,
  }) => {

    const messageResponses: number[] = [];
    page.on('response', (response) => {
      if (response.url().includes('/api/message')) {
        messageResponses.push(response.status());
      }
    });

    await page.goto(`${SPA_URL}/message`);

    await page.waitForURL(`${SPA_URL}/`);

    await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
    await expect(page.getByTestId('message')).toHaveCount(0);

    expect(messageResponses).not.toContain(200);
  });
});
