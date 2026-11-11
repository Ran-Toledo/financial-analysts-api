import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Dev: `npm run dev` serves on :5173 and forwards /api to the ASP.NET API on :5076 (same origin, so no CORS).
// Prod: `npm run build` writes into the API's wwwroot, and the API serves it.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: { '/api': 'http://localhost:5076' },
  },
  build: {
    outDir: '../src/FinancialAnalystsAPI/wwwroot',
    emptyOutDir: true,
  },
  test: {
    include: ['src/**/*.test.ts'],
  },
})
