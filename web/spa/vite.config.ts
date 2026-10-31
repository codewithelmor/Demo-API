import babel from '@rolldown/plugin-babel'
import tailwindcss from '@tailwindcss/vite'
import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vitest/config'

// In development the BFF (dotnet run, http://localhost:5080) is the only back end the browser sees.
const bff = 'http://localhost:5080'

export default defineConfig({
  // React Compiler memoizes components automatically (React Compiler 1.0, through Babel).
  plugins: [react(), babel({ presets: [reactCompilerPreset()] }), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5173,
    strictPort: true,
    // Same origin in development: Vite forwards /api and /bff to the BFF. No CSP on the dev server.
    proxy: { '/api': { target: bff }, '/bff': { target: bff } },
  },
  build: {
    // Plain SPA output; the Dockerfile copies dist/ into the BFF wwwroot.
    outDir: 'dist',
    emptyOutDir: true,
    // No public source maps.
    sourcemap: false,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    restoreMocks: true,
  },
})
