import { defineConfig } from 'vitest/config';

/**
 * The test runner's configuration, and nothing else.
 *
 * It was called `vite.config.ts`, which made five files in this repository share
 * that name — four of them an application's build, and this one the odd member
 * that builds nothing. Vitest reads either name; only one of them says which.
 *
 * No global setup file: suites compose their own content catalogs through
 * `@empire/test-content`, the same way an application composition root does.
 * A setup file that installed content ambiently made every suite share one
 * namespace, which hid conflicts and made isolation untestable.
 */
export default defineConfig({});
