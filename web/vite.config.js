import vue from '@vitejs/plugin-vue';
import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';
import svgLoader from 'vite-svg-loader';

export default defineConfig({
  base: process.env.WEB_BASE_HREF || '/probate/',
  plugins: [vue(), svgLoader()],
  test: {
    environment: 'happy-dom',
    setupFiles: ['./src/test/setup.ts'],
    pool: 'forks',
    testTimeout: 30000,
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@assets': fileURLToPath(new URL('./src/assets', import.meta.url)),
      '@components': fileURLToPath(
        new URL('./src/components', import.meta.url)
      ),
      '@router': fileURLToPath(new URL('./src/router', import.meta.url)),
      '@services': fileURLToPath(new URL('./src/services', import.meta.url)),
      '@store': fileURLToPath(new URL('./src/store', import.meta.url)),
      '@styles': fileURLToPath(new URL('./src/styles', import.meta.url)),
      '~bootstrap': fileURLToPath(
        new URL('./node_modules/bootstrap', import.meta.url)
      ),
    },
    extensions: ['.ts', '.vue', '.json', '.scss', '.svg', '.png', '.jpg'],
  },
  server: {
    host: '0.0.0.0',
    port: 8080,
    proxy: {
      '^/probate/api': {
        target: process.env.API_URL || 'http://localhost:5000',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/probate/, ''),
        headers: {
          Connection: 'keep-alive',
          'X-Forwarded-Host': 'localhost:8080',
          'X-Forwarded-Proto': 'http',
          'X-Base-Href': process.env.WEB_BASE_HREF || '/probate/',
        },
      },
      '^/api': {
        target: process.env.API_URL || 'http://localhost:5000',
        changeOrigin: true,
        headers: {
          Connection: 'keep-alive',
          'X-Forwarded-Host': 'localhost:8080',
          'X-Forwarded-Proto': 'http',
          'X-Base-Href': process.env.WEB_BASE_HREF || '/probate/',
        },
      },
      '^/swagger': {
        target: process.env.API_URL || 'http://localhost:5000',
        changeOrigin: true,
        headers: {
          Connection: 'keep-alive',
          'X-Forwarded-Host': 'localhost:8080',
          'X-Forwarded-Proto': 'http',
        },
      },
    },
  },
});
