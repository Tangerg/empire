import { defineConfig } from 'vitest/config';
import { resolve } from 'node:path';

export default defineConfig({
  base: './',
  test: {
    setupFiles: ['./src/test/setup-content.ts'],
  },
  build: {
    target: 'es2022',
    rollupOptions: {
      input: {
        game: resolve(__dirname, 'index.html'),
        editor: resolve(__dirname, 'editor.html'),
        demo: resolve(__dirname, 'demo.html'),
      },
    },
  },
});
