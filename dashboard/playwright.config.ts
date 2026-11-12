import { defineConfig } from '@playwright/test'

const API_PORT = 5077
const WEB_PORT = 5179

/**
 * Smoke tests: every page against the fake-data API. Both servers start automatically (or are reused if already
 * running). On Windows the tests drive the installed Edge, so no browser download is needed; elsewhere run
 * `npx playwright install chromium` once, or set PW_CHANNEL (e.g. "chrome").
 */
export default defineConfig({
  testDir: 'e2e',
  timeout: 30_000,
  fullyParallel: true,
  reporter: 'list',
  use: {
    baseURL: `http://localhost:${WEB_PORT}`,
    channel: process.env.PW_CHANNEL ?? (process.platform === 'win32' ? 'msedge' : undefined),
    trace: 'retain-on-failure',
  },
  webServer: [
    {
      command: 'node e2e/fake-api.mjs',
      url: `http://localhost:${API_PORT}/api/stocks/sectors`,
      env: { E2E_API_PORT: String(API_PORT) },
      timeout: 600_000, // the first run builds the database
      reuseExistingServer: !process.env.CI,
    },
    {
      command: `npx vite --port ${WEB_PORT} --strictPort`,
      url: `http://localhost:${WEB_PORT}`,
      env: { API_URL: `http://localhost:${API_PORT}` },
      // Always our own Vite: a server already on this port could be another app.
      reuseExistingServer: false,
    },
  ],
})
