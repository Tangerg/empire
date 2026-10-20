import { JSDOM } from 'jsdom';
import { describe, expect, it } from 'vitest';
import { Battlefield, createBattleEngine, mapFromLevel, type Coord } from '@empire/battle-engine';
import { createTestCatalog } from '@empire/test-content';
import { GROUND_TAGS, taggedGround, type BoardPiece } from '@empire/game-ui';
import { CANDIDATE_01_CONTENT_PACK, candidate01Level } from '../index';
import { candidate01ClipGround, candidate01PlayableShape } from './candidate-01-playable-shape';
import { candidate01MapSceneryLayers, candidate01SceneFrameMarkup } from './candidate-01-map-scene';
import { candidateReviewViewport } from '../../../../tools/art/candidate_review_viewport';

const content = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
const engine = createBattleEngine({ content });
const level = candidate01Level('c01-02');
const map = mapFromLevel(content, level);
const outside = (index: number) => taggedGround(content.terrains.get(map.tiles[index]), GROUND_TAGS.outside);
const coordinate = (index: number): Coord => ({ x: index % map.width, y: Math.floor(index / map.width) });
const outsideCells = map.tiles.flatMap((_, index) => outside(index) ? [coordinate(index)] : []);
const activeCells = map.tiles.flatMap((_, index) => outside(index) ? [] : [coordinate(index)]);
const parser = new (new JSDOM('').window.DOMParser)();

function document(markup: string): Document {
  return parser.parseFromString(`<svg xmlns="http://www.w3.org/2000/svg">${markup}</svg>`, 'image/svg+xml');
}

function localClip(piece: BoardPiece): { id: string; path: Element } {
  const svg = document(piece.markup).documentElement;
  const clip = svg.querySelector(':scope > defs > clipPath');
  const id = clip?.getAttribute('id');
  const path = clip?.querySelector('path');
  if (!id || !path) throw new Error('piece is missing its own playable clip definition');
  expect(svg.querySelector(':scope > g')?.getAttribute('clip-path')).toBe(`url(#${id})`);
  expect(path.getAttribute('transform')).toBe(`translate(${-piece.x} ${-piece.y})`);
  return { id, path };
}

describe('terrain-owned irregular playable boundaries', () => {
  it('makes Three Bridges genuinely irregular inside its active bounding box', () => {
    expect(outsideCells.length).toBeGreaterThan(0);
    expect(activeCells.length).toBeGreaterThan(0);
    const minX = Math.min(...activeCells.map(cell => cell.x));
    const maxX = Math.max(...activeCells.map(cell => cell.x));
    const minY = Math.min(...activeCells.map(cell => cell.y));
    const maxY = Math.max(...activeCells.map(cell => cell.y));
    expect(outsideCells.some(cell => cell.x >= minX && cell.x <= maxX && cell.y >= minY && cell.y <= maxY)).toBe(true);
    const rowWidths = new Set(Array.from({ length: map.height }, (_, y) => activeCells.filter(cell => cell.y === y).length));
    expect(rowWidths.size).toBeGreaterThan(1);
    expect(candidate01PlayableShape(content, map)?.path).toBeTruthy();
  });

  it('derives the shape from outside tags, not terrain identity or decoration', () => {
    const catalog = createTestCatalog(CANDIDATE_01_CONTENT_PACK);
    catalog.terrains.define({ ...catalog.terrains.get('c01.outside'), id: 'probe.outside' });
    const renamed = { ...map, tiles: map.tiles.map(id => id === 'c01.outside' ? 'probe.outside' : id) };
    expect(candidate01PlayableShape(catalog, renamed)).toEqual(candidate01PlayableShape(content, map));

    const repainted = { ...map, tiles: map.tiles.map((id, index) => outside(index) ? id : 'plain') };
    expect(candidate01PlayableShape(content, repainted)).toEqual(candidate01PlayableShape(content, map));
    const expanded = { ...map, tiles: [...map.tiles] };
    const opening = outsideCells[0];
    expanded.tiles[opening.y * map.width + opening.x] = 'plain';
    expect(candidate01PlayableShape(content, expanded)?.signature).not.toBe(candidate01PlayableShape(content, map)?.signature);
    expect(candidate01PlayableShape(content, expanded)?.path).not.toBe(candidate01PlayableShape(content, map)?.path);
  });

  it('leaves rectangular maps and their piece markup unchanged', () => {
    const rectangular = { ...map, tiles: map.tiles.map(id => id === 'c01.outside' ? 'plain' : id) };
    expect(candidate01PlayableShape(content, rectangular)).toBeNull();
    const pieces = [{ x: 7, y: 19, markup: '<rect width="12" height="8"/>' }];
    expect(candidate01ClipGround(pieces, null, 'probe')).toEqual(pieces);
  });

  it('keeps each translated piece self-contained and every clip id distinct', () => {
    const shape = candidate01PlayableShape(content, map)!;
    const pieces: BoardPiece[] = [
      { x: 0, y: 0, markup: '<rect width="64" height="64"/>' },
      { x: 512, y: 320, markup: '<rect width="64" height="64"/>', foot: { x: 3, y: 7 } },
      { x: -17, y: 13, markup: '<circle r="12"/>' },
    ];
    const before = JSON.stringify(pieces);
    const clipped = candidate01ClipGround(pieces, shape, 'ground');
    const lights = candidate01ClipGround(pieces, shape, 'light');
    const ids = [...clipped, ...lights].map(piece => {
      const clip = localClip(piece);
      expect(clip.path.getAttribute('d')).toBe(shape.path);
      return clip.id;
    });
    expect(new Set(ids).size).toBe(ids.length);
    for (const [index, piece] of clipped.entries()) {
      expect(piece).toMatchObject({ x: pieces[index].x, y: pieces[index].y });
      expect(piece.foot).toEqual(pieces[index].foot);
      expect(piece.markup).toContain(pieces[index].markup);
    }
    expect(JSON.stringify(pieces)).toBe(before);
  });

  it.each([false, true])('clips every scene ground and sunlight piece in editing=%s', editing => {
    const before = JSON.stringify(map);
    const context = { content, levelId: level.id, map, viewport: candidateReviewViewport(map), editing };
    const layers = candidate01MapSceneryLayers(context);
    const sunlight = layers.underUnits.filter(piece => piece.markup.includes('id="c01-field-sun"'));
    expect(layers.ground.length).toBeGreaterThan(0);
    expect(sunlight).toHaveLength(1);
    const pieces = [...layers.ground, ...sunlight];
    const ids = pieces.map(piece => localClip(piece).id);
    expect(new Set(ids).size).toBe(ids.length);
    const backdrop = document(candidate01SceneFrameMarkup(context).backdrop);
    expect(backdrop.querySelector('image')).toBeNull();
    expect(backdrop.querySelector('[fill="url(#c01-scene-ground)"]')).toBeNull();
    expect(JSON.stringify(map)).toBe(before);
  });

  it('rejects outside cells for every installed movement class, including flying', () => {
    const state = engine.createState(level);
    const battlefield = new Battlefield(state, content);
    const grid = engine.rules.grids.get('square4');
    const movementClasses = content.movementProfiles.keys();
    expect(movementClasses).toContain('flying');
    let boundaryEdges = 0;
    for (const at of outsideCells) {
      const cell = battlefield.cell(at);
      for (const movement of movementClasses) {
        expect(cell.movementCost(movement), `${movement} at ${at.x},${at.y}`).toBeNull();
        expect(cell.admits(movement), `${movement} at ${at.x},${at.y}`).toBe(false);
        for (const from of grid.adjacent(at)) {
          if (!battlefield.contains(from) || taggedGround(battlefield.cell(from).terrain, GROUND_TAGS.outside)) continue;
          expect(battlefield.traversalCost(from, at, movement), `${movement} entering ${at.x},${at.y}`).toBeNull();
          boundaryEdges++;
        }
      }
    }
    expect(boundaryEdges).toBeGreaterThan(0);
  });

  it('keeps all actors, rule buildings and objective zones on actual active terrain', () => {
    const state = engine.createState(level);
    const battlefield = new Battlefield(state, content);
    const placements = [
      ...state.units.map(unit => ({ at: unit, name: `unit ${unit.key}` })),
      ...state.structures.map(structure => ({ at: structure, name: `structure ${structure.id}` })),
      ...(level.owners ?? []).map(at => ({ at, name: 'building owner' })),
      ...(level.scenario?.zones ?? []).flatMap(zone => zone.cells.map(at => ({ at, name: `zone ${zone.id}` }))),
      ...map.tiles.flatMap((id, index) => content.terrains.get(id).tags.includes('building')
        ? [{ at: coordinate(index), name: `building ${id}` }] : []),
    ];
    expect(placements.length).toBeGreaterThan(state.units.length);
    for (const { at, name } of placements) {
      expect(battlefield.contains(at), name).toBe(true);
      expect(taggedGround(battlefield.cell(at).terrain, GROUND_TAGS.outside), `${name} at ${at.x},${at.y}`).toBe(false);
    }
  });
});
