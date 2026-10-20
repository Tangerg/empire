import { defineConfig } from 'vitest/config';
export default defineConfig({ root: "/workspace/shared/empire-art-rebuild/approved-v5/fx/ambient-contract/harness", cacheDir: "/workspace/shared/empire-art-rebuild/approved-v5/fx/ambient-contract/harness/.vite", test: { include: ['candidate-ambient-fx-playback.test.ts'], maxWorkers: 1, minWorkers: 1 } });
