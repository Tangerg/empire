import { viteSingleFile } from 'vite-plugin-singlefile';
import { appConfig } from '../../vite.shared';
import { lateInlineAssets } from '../../tools/build/late-inline-assets';

const resources = lateInlineAssets();

/** One file, opened straight off disk: everything inlined, nothing split out. */
export default appConfig('experience-lab', {
  plugins: [resources.plugin, viteSingleFile({ useRecommendedBuildConfig: false })],
  experimental: { renderBuiltUrl: resources.renderBuiltUrl },
  build: {
    cssCodeSplit: false,
    assetsInlineLimit: 0,
    assetsDir: '',
    copyPublicDir: false,
    rollupOptions: { output: { inlineDynamicImports: true } },
  },
});
