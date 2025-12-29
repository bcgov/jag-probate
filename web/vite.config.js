import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';
import svgLoader from 'vite-svg-loader';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  base: process.env.NODE_ENV === 'production' ? '/' : '/',
  plugins: [
    vue(),
    svgLoader(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@assets': fileURLToPath(new URL('./src/assets', import.meta.url)),
      '@components': fileURLToPath(new URL('./src/components', import.meta.url)),
      '@router': fileURLToPath(new URL('./src/router', import.meta.url)),
      '@services': fileURLToPath(new URL('./src/services', import.meta.url)),
      '@store': fileURLToPath(new URL('./src/store', import.meta.url)),
      '@styles': fileURLToPath(new URL('./src/styles', import.meta.url)),
      '~bootstrap': fileURLToPath(new URL('./node_modules/bootstrap', import.meta.url)),
    },
    extensions: ['.ts', '.vue', '.json', '.scss', '.svg', '.png', '.jpg'],
  },
  server: {
    host: '0.0.0.0',
    port: 8080,
    proxy: {
      '^/api': {
        target: 'http://api:5000',
        changeOrigin: true,
        headers: {
          Connection: 'keep-alive',
          'X-Forwarded-Host': 'localhost',
          'X-Forwarded-Port': '8080',
        },
      },
      '^/swagger': {
        target: 'http://api:5000',
        changeOrigin: true,
        headers: {
          Connection: 'keep-alive',
          'X-Forwarded-Host': 'localhost',
          'X-Forwarded-Port': '8080',
        },
      },
    },
  },
});
