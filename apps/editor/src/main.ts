import '@empire/game-ui/styles/app.css';
import '@empire/editor/styles/editor.css';
import { createContentCatalog, ContentPackInstaller } from '@empire/battle-engine';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '@empire/content-ancient-empires';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { registerCandidate01Presentation } from '@empire/story-candidate-01/presentation';
import { EditorApp, initialLevel } from '@empire/editor';

/** Composition root: this app declares its own content, nothing ambient. */
const content = createContentCatalog();
new ContentPackInstaller(content).install(
  COMMON_CONTENT_PACK,
  ANCIENT_EMPIRES_CONTENT_PACK,
  CANDIDATE_01_CONTENT_PACK,
);
registerCandidate01Presentation();

new EditorApp(content, initialLevel(content)).mount(document.getElementById('app')!);
