import { defineConfig } from 'vitest/config';

/**
 * No global setup file: suites compose their own content catalogs through
 * `@empire/test-content`, the same way an application composition root does.
 * A setup file that installed content ambiently made every suite share one
 * namespace, which hid conflicts and made isolation untestable.
 */
export default defineConfig({});
