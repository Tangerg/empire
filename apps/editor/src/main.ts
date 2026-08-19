import '@empire/game-ui/styles/app.css';
import '@empire/editor/styles/editor.css';
import {
  createContentCatalog,
  ContentPackInstaller,
  createBattleEngine,
} from '@empire/battle-engine';
import { COMMON_CONTENT_PACK } from '@empire/content-common';
import {
  ANCIENT_EMPIRES_CONTENT_PACK,
  ANCIENT_EMPIRES_LEVELS,
} from '@empire/content-ancient-empires';
import { CANDIDATE_01_CONTENT_PACK, CANDIDATE_01_LEVELS } from '@empire/story-candidate-01';
import { CANDIDATE_01_ART } from '@empire/story-candidate-01/presentation';
import { EditorApp, initialLevel, type EditorSetup } from '@empire/editor';

/** Composition root: this app declares its own content, nothing ambient. */
const content = createContentCatalog();
new ContentPackInstaller(content).install(
  COMMON_CONTENT_PACK,
  ANCIENT_EMPIRES_CONTENT_PACK,
  CANDIDATE_01_CONTENT_PACK,
);

/**
 * The editor package no longer knows any of this.
 *
 * It used to import one campaign's levels itself, which meant the preset list
 * and the installed catalog could disagree — as they already did: this app
 * installs the candidate-01 pack, and its chapters were not offered. Whoever
 * composes the catalog says which levels were composed for it.
 */
const setup: EditorSetup = {
  rules: createBattleEngine({ content }).rules,
  // Art is composed here beside the catalog, exactly as the game shell does it.
  // The editor drew with the generic fallback whatever it was opened for, so a
  // chapter of the campaign it lists could not be seen as it actually plays.
  art: CANDIDATE_01_ART,
  presets: [...ANCIENT_EMPIRES_LEVELS, ...CANDIDATE_01_LEVELS],
};

new EditorApp(setup, initialLevel(setup)).mount(document.getElementById('app')!);
