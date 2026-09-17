/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Pinned build contract (docs/testing/frontend-and-sql-cases.md §2.2):
// production assets emit into the dedicated backend/wwwroot/app subfolder with
// public base /app/ and a Vite manifest, so the Razor shell can resolve the
// hashed entry/CSS without hard-coding names and without a dev server.
export default defineConfig({
  plugins: [react()],
  base: '/app/',
  build: {
    outDir: '../backend/wwwroot/app',
    emptyOutDir: true,
    manifest: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
  },
})
