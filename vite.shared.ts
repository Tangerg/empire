import { defineConfig, type UserConfig } from 'vite';

/**
 * What every application in this repository builds like.
 *
 * Three of the four configs were the same five lines with one word changed, and
 * the word was the convention: an app named `x` builds into `dist/x`. Written out
 * four times, that convention was a thing you could get wrong in one file and
 * nobody would notice until `build:apps` overwrote another app's output.
 *
 * `base: './'` because the built pages are opened over `file://` as often as they
 * are served, and an absolute base breaks that.
 */
export const appConfig = (name: string, extra: UserConfig = {}): UserConfig =>
  defineConfig({
    ...extra,
    base: './',
    build: {
      ...extra.build,
      outDir: `../../dist/${name}`,
      emptyOutDir: true,
    },
  });
