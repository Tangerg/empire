import { viteSingleFile } from 'vite-plugin-singlefile';
import { appConfig } from '../../vite.shared';

/** One file, opened straight off disk: everything inlined, nothing split out. */
export default appConfig('experience-lab', {
  plugins: [viteSingleFile()],
  build: { cssCodeSplit: false, assetsInlineLimit: Number.MAX_SAFE_INTEGER },
});
