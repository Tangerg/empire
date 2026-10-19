/** Actual controller pointer-input audit; writes offline evidence and changes no level or source asset. */
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { relative, resolve } from 'node:path';
import { engine, CANDIDATE_01_ART, shippedLevel } from '../board-harness';
import { GameController, SvgBoardSurface, type BoardPointer } from '@empire/game-ui';

const root = resolve(import.meta.dirname, '../..');
const output = resolve(process.env.INTERACTION_REVIEW_OUTPUT ?? '/tmp/empire-silverwood-interaction');
mkdirSync(output, { recursive: true });
// The board harness is normally static. A real controller opening announcement
// needs RAF timestamps on the same clock as the timeline's performance.now().
const previousFrameRequest = globalThis.requestAnimationFrame;
globalThis.requestAnimationFrame = callback => Number(setTimeout(() => callback(performance.now()), 16));
let pointer: BoardPointer | undefined;
const game = new GameController(shippedLevel('c01-15'), () => {}, {
  engine, art: CANDIDATE_01_ART,
  renderer: scene => {
    const surface = new SvgBoardSurface(scene);
    const listen = surface.listen.bind(surface);
    surface.listen = input => { pointer = input; listen(input); };
    return surface;
  },
});
try {
  const svg = game.root.querySelector<SVGSVGElement>('svg.candidate-map') ?? game.root.querySelector<SVGSVGElement>('svg');
  if (!svg?.querySelector('.board-world')) throw new Error('missing actual board');
  const controls = (): BoardPointer => {
    if (!pointer) throw new Error('controller has not connected pointer input');
    return pointer;
  };
  const sources = new Map<string, string>();
  const codeSources = [
    'packages/game-ui/src/ui/game.ts', 'packages/game-ui/src/ui/board.ts',
    'packages/game-ui/src/ui/hud.ts', 'packages/game-ui/src/art/board-surface.ts',
    'packages/game-ui/src/art/svg-board-surface.ts', 'packages/game-ui/src/styles/app.css',
  ];
  for (const path of codeSources) sources.set(path, createHash('sha256').update(readFileSync(resolve(root, path))).digest('hex'));
  const report: Array<Record<string, unknown>> = [];
  const cell = (x: number, y: number) => ({ x: 74 + (x + .5) * 32, y: 62 + (y + .5) * 32 });
  const record = (label: string, save = false) => {
    const cue = svg.querySelector('.selection-cue');
    report.push({ label,
      unitName: game.root.querySelector('.unit-name')?.textContent?.trim(),
      unitHp: game.root.querySelector('.unit-hp')?.textContent?.trim(),
      cueCount: svg.querySelectorAll('.selection-cue').length,
      cueTransform: cue?.getAttribute('transform'), cueOpacity: cue?.getAttribute('opacity'),
      cueImage: cue?.querySelector('image')?.getAttribute('href'),
      selectedOrigins: [...svg.querySelectorAll('.unit.is-selected')].map(unit => unit.getAttribute('transform')),
    });
    if (!save) return;
    const copy = svg.cloneNode(true) as SVGSVGElement;
    const style = document.createElementNS('http://www.w3.org/2000/svg', 'style');
    style.textContent = readFileSync(resolve(root, 'packages/game-ui/src/styles/app.css'), 'utf8');
    copy.prepend(style);
    copy.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    copy.setAttribute('width', '916'); copy.setAttribute('height', '648'); copy.removeAttribute('style');
    const markup = copy.outerHTML.replace(/href="(\/packages\/[^"?]+)(?:\?[^"]*)?"/g, (_, path: string) => {
      const file = resolve(root, `.${path}`);
      sources.set(relative(root, file), createHash('sha256').update(readFileSync(file)).digest('hex'));
      return `href="${relative(output, file)}"`;
    });
    writeFileSync(resolve(output, `${label}.svg`), markup);
  };
  const deadline = performance.now() + 10000;
  while (svg.querySelector('.fx-banner')) {
    if (performance.now() >= deadline) throw new Error('opening announcement did not complete');
    await new Promise(resolve => setTimeout(resolve, 50));
  }
  record('idle');
  controls().move(cell(14, 5)); record('hover-longbow-1', true);
  controls().move(cell(14, 9)); record('hover-longbow-2');
  controls().move(cell(4, 8)); record('hover-tasha');
  controls().press(cell(4, 8), 0); record('selected-tasha', true);
  controls().move(cell(14, 5)); record('selected-tasha-hover-enemy', true);
  const expectedOrigins = [undefined, 'translate(448.00,160.00)', 'translate(448.00,288.00)',
    undefined, 'translate(128.00,256.00)', 'translate(128.00,256.00)'];
  report.forEach((row, index) => {
    if (row.cueCount !== (expectedOrigins[index] ? 1 : 0)
      || (row.cueTransform ?? undefined) !== expectedOrigins[index]
      || (row.cueCount === 1 && row.cueOpacity !== '0.12')) throw new Error(`unexpected cue contract: ${row.label}`);
  });
  for (const index of [1, 2, 5]) if (!String(report[index].unitName).includes('银林长弓') || report[index].unitHp !== '76 / 76') {
    throw new Error('enemy inspection was lost');
  }
  for (const index of [3, 4]) if (!String(report[index].unitName).includes('塔莎') || report[index].unitHp !== '92 / 92') {
    throw new Error('friendly inspection was lost');
  }
  writeFileSync(resolve(output, 'interaction.json'), JSON.stringify({
    kind: 'actual GameController input handlers through surface pointer contract; offline JSDOM, no live browser/GPU',
    checkpoint: execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim(),
    report, sources: Object.fromEntries(sources),
  }, null, 2) + '\n');
  console.log(output);
} finally {
  game.dispose();
  globalThis.requestAnimationFrame = previousFrameRequest;
}
