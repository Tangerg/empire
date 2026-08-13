import '@empire/game-ui/styles/app.css';
import '@empire/editor/styles/editor.css';
import { installContentPacks } from '@empire/battle-engine';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import { ANCIENT_EMPIRES_CONTENT_PACK } from '@empire/content-ancient-empires';
import { CANDIDATE_01_CONTENT_PACK } from '@empire/story-candidate-01';
import { registerCandidate01Presentation } from '@empire/story-candidate-01/presentation';
import { EditorApp, initialLevel } from '@empire/editor';

installContentPacks(COMMON_CONTENT_PACK, ANCIENT_EMPIRES_CONTENT_PACK, CANDIDATE_01_CONTENT_PACK);
registerCandidate01Presentation();

new EditorApp(initialLevel()).mount(document.getElementById('app')!);
