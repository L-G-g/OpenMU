import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In development the API runs in the OpenMU process (Portal__Urls, e.g. http://localhost:8090).
export default defineConfig({
  plugins: [react()],
  server: { proxy: { '/api': process.env.PORTAL_API ?? 'http://localhost:8090' } },
  build: { outDir: 'dist', assetsDir: 'assets', sourcemap: false },
});
