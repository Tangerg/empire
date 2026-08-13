import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    setupFiles: ['./tooling/test/setup-content.ts'],
  },
});
