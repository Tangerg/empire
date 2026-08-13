import { defineConfig } from 'vite';

export default defineConfig({
  base: './',
  build: { outDir: '../../dist/editor', emptyOutDir: true },
});
