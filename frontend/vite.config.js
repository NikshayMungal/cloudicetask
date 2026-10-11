import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In `npm run dev`, requests to /api are proxied to the locally running backend.
// In Docker/production the same /api path is handled by nginx (see nginx.conf.template).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { '/api': { target: 'http://localhost:5000', changeOrigin: true } },
  },
});
