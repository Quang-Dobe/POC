import { defineConfig, devices } from '@playwright/test';

export default defineConfig({

  testDir: './e2e',

  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  workers: 1,

  reporter: 'line',

  timeout: 60_000,
  expect: { timeout: 10_000 },

  use: {

    baseURL: 'http://localhost:5173',

    ignoreHTTPSErrors: true,
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
    trace: 'off',
    video: 'off',
    screenshot: 'off',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
