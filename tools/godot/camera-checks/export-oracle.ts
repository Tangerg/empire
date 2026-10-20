import { writeFileSync } from 'node:fs';
import { JSDOM } from 'jsdom';
import type { BoardPointer } from '../../../packages/game-ui/src/art/board-surface';
import { listenForPointer } from '../../../packages/game-ui/src/art/letterbox';
import { BattlefieldCamera } from '../../../packages/game-ui/src/ui/battlefield-camera';

interface Point {
  x: number;
  y: number;
}

interface Insets {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

interface Box {
  left: number;
  top: number;
  width: number;
  height: number;
}

type CameraOperation =
  | { kind: 'resize'; viewport: Point; padding: Insets }
  | { kind: 'center'; point: Point }
  | { kind: 'pan'; delta: Point }
  | { kind: 'reveal'; point: Point; margin: number }
  | { kind: 'queueZoom'; delta: number }
  | { kind: 'applyZoom' };

interface CameraCase {
  name: string;
  fieldSize: Point;
  sceneInsets: Insets;
  initialFocus: Point;
  operations: CameraOperation[];
}

interface CameraState {
  zoom: number;
  scroll: Point;
  fieldOrigin: Point;
  position: Point;
  hasViewport: boolean;
}

interface PointerInput {
  kind: 'down' | 'up' | 'move' | 'leave' | 'cancel' | 'lostCapture';
  point: Point;
  button?: number;
  pointerId?: number;
  isPrimary?: boolean;
  pointerType?: string;
}

type PointerOperation =
  | PointerInput
  | { kind: 'wheel'; deltaY: number; ctrlKey?: boolean; metaKey?: boolean }
  | { kind: 'setBox'; box: Box }
  | { kind: 'dispose' };

type PointerCallback =
  | { kind: 'press'; point: Point; button: number }
  | { kind: 'move'; point: Point | null }
  | { kind: 'pan'; delta: Point }
  | { kind: 'leave' }
  | { kind: 'scale'; notches: number };

interface PointerCase {
  name: string;
  scene: { width: number; height: number };
  box: Box;
  operations: PointerOperation[];
}

interface PointerState {
  callbacks: PointerCallback[];
  panning: boolean;
  defaultPrevented: boolean;
}

const noInsets: Insets = { left: 0, top: 0, right: 0, bottom: 0 };
const hud: Insets = { left: 100, top: 60, right: 120, bottom: 80 };
const dom = new JSDOM('<!doctype html><html><body></body></html>');

function rect(box: Box): DOMRect {
  return new dom.window.DOMRect(box.left, box.top, box.width, box.height);
}

function cameraOracle(input: CameraCase): CameraState[] {
  const viewport = dom.window.document.createElement('div');
  const board = dom.window.document.createElement('div');
  viewport.append(board);
  dom.window.document.body.append(viewport);
  let size: Point = { x: 0, y: 0 };
  let padding = noInsets;
  let zoom = 1.25;
  let pendingZoom = 0;
  let hasViewport = false;
  const sceneSize = () => ({
    x: (input.fieldSize.x + input.sceneInsets.left + input.sceneInsets.right) * zoom,
    y: (input.fieldSize.y + input.sceneInsets.top + input.sceneInsets.bottom) * zoom,
  });
  const boardOrigin = () => ({
    x: padding.left + Math.max(0, (size.x - padding.left - padding.right - sceneSize().x) / 2),
    y: padding.top + Math.max(0, (size.y - padding.top - padding.bottom - sceneSize().y) / 2),
  });
  Object.defineProperties(viewport, {
    clientWidth: { get: () => size.x },
    clientHeight: { get: () => size.y },
    scrollWidth: { get: () => Math.max(size.x, sceneSize().x + padding.left + padding.right) },
    scrollHeight: { get: () => Math.max(size.y, sceneSize().y + padding.top + padding.bottom) },
  });
  viewport.getBoundingClientRect = () => rect({ left: 0, top: 0, width: size.x, height: size.y });
  board.getBoundingClientRect = () => rect({
    left: boardOrigin().x - viewport.scrollLeft,
    top: boardOrigin().y - viewport.scrollTop,
    width: sceneSize().x,
    height: sceneSize().y,
  });
  viewport.scrollTo = (options?: ScrollToOptions | number, _y?: number) => {
    if (typeof options !== 'object') throw new Error('The source camera must use scroll options.');
    viewport.scrollLeft = options.left ?? viewport.scrollLeft;
    viewport.scrollTop = options.top ?? viewport.scrollTop;
  };
  const camera = new BattlefieldCamera(viewport, board);
  const scenePoint = (point: Point): Point => ({
    x: (point.x + input.sceneInsets.left) * zoom,
    y: (point.y + input.sceneInsets.top) * zoom,
  });
  const expected = input.operations.map((operation) => {
    switch (operation.kind) {
      case 'resize': {
        // The native adapter discards no-layout notifications before updating its geometry.
        if (operation.viewport.x <= 0 || operation.viewport.y <= 0) break;
        size = operation.viewport;
        padding = operation.padding;
        viewport.style.padding = `${padding.top}px ${padding.right}px ${padding.bottom}px ${padding.left}px`;
        // ResizeObserver reports the content box, while the camera reads client dimensions.
        if (size.x - padding.left - padding.right <= 0 || size.y - padding.top - padding.bottom <= 0) break;
        if (hasViewport) camera.resize();
        else {
          hasViewport = true;
          camera.centerOn(scenePoint(input.initialFocus), 'instant');
        }
        break;
      }
      case 'center':
        camera.centerOn(scenePoint(operation.point), 'instant');
        break;
      case 'pan':
        camera.pan(operation.delta);
        break;
      case 'reveal':
        camera.reveal(scenePoint(operation.point), operation.margin);
        break;
      case 'queueZoom':
        pendingZoom += operation.delta;
        break;
      case 'applyZoom': {
        // This is GameController's per-frame composition, not a second camera implementation.
        const center = camera.position;
        const previous = zoom;
        zoom = Math.max(0.5, Math.min(2.2, previous + pendingZoom));
        pendingZoom = 0;
        const ratio = zoom / previous;
        camera.centerOn({ x: center.x * ratio, y: center.y * ratio }, 'instant');
        break;
      }
    }
    const position = camera.position;
    const origin = boardOrigin();
    return {
      zoom,
      scroll: { x: viewport.scrollLeft, y: viewport.scrollTop },
      fieldOrigin: {
        x: origin.x + input.sceneInsets.left * zoom - viewport.scrollLeft,
        y: origin.y + input.sceneInsets.top * zoom - viewport.scrollTop,
      },
      position: { x: position.x / zoom - input.sceneInsets.left, y: position.y / zoom - input.sceneInsets.top },
      hasViewport,
    };
  });
  viewport.remove();
  return expected;
}

function pointerOracle(input: PointerCase): PointerState[] {
  const element = dom.window.document.createElement('div');
  let box = input.box;
  element.getBoundingClientRect = () => rect(box);
  let callbacks: PointerCallback[] = [];
  const pointer: BoardPointer = {
    press: (point, button) => callbacks.push({ kind: 'press', point, button }),
    move: (point) => callbacks.push({ kind: 'move', point }),
    pan: (delta) => callbacks.push({ kind: 'pan', delta }),
    leave: () => callbacks.push({ kind: 'leave' }),
    scale: (notches) => callbacks.push({ kind: 'scale', notches }),
  };
  const dispose = listenForPointer(element, input.scene, pointer);
  const types: Record<PointerInput['kind'], string> = {
    down: 'pointerdown', up: 'pointerup', move: 'pointermove', leave: 'pointerleave',
    cancel: 'pointercancel', lostCapture: 'lostpointercapture',
  };
  try {
    return input.operations.map((operation) => {
      callbacks = [];
      let event: Event | null = null;
      if (operation.kind === 'setBox') box = operation.box;
      else if (operation.kind === 'dispose') dispose();
      else if (operation.kind === 'wheel') {
        event = new dom.window.WheelEvent('wheel', {
          deltaY: operation.deltaY,
          ctrlKey: operation.ctrlKey ?? false,
          metaKey: operation.metaKey ?? false,
          cancelable: true,
          bubbles: true,
        });
      } else {
        event = new dom.window.MouseEvent(types[operation.kind], {
          clientX: operation.point.x, clientY: operation.point.y,
          button: operation.button ?? 0, cancelable: true, bubbles: true,
        });
        Object.defineProperties(event, {
          pointerId: { value: operation.pointerId ?? 1 },
          isPrimary: { value: operation.isPrimary ?? true },
          pointerType: { value: operation.pointerType ?? 'mouse' },
        });
      }
      if (event) element.dispatchEvent(event);
      return {
        callbacks,
        panning: element.classList.contains('is-panning'),
        defaultPrevented: event?.defaultPrevented ?? false,
      };
    });
  } finally {
    dispose();
  }
}

const resize = (x: number, y: number, padding: Insets = hud): CameraOperation => ({
  kind: 'resize', viewport: { x, y }, padding,
});
const center = (x: number, y: number): CameraOperation => ({ kind: 'center', point: { x, y } });
const pan = (x: number, y: number): CameraOperation => ({ kind: 'pan', delta: { x, y } });
const reveal = (x: number, y: number, margin: number): CameraOperation => ({ kind: 'reveal', point: { x, y }, margin });
const queueZoom = (delta: number): CameraOperation => ({ kind: 'queueZoom', delta });
const applyZoom: CameraOperation = { kind: 'applyZoom' };
const cameraCase = (
  name: string,
  operations: CameraOperation[],
  fieldSize: Point = { x: 1152, y: 768 },
  sceneInsets: Insets = noInsets,
  initialFocus: Point = { x: 400, y: 320 },
): CameraCase => ({ name, fieldSize, sceneInsets, initialFocus, operations });

const cameraCases: CameraCase[] = [
  cameraCase('source-fixture-pan-and-four-edge-clamps', [
    resize(800, 600), center(400, 320), pan(-100, -50), pan(-10000, -10000),
    pan(10000, 10000), center(-1000, 1000), center(10000, -1000),
  ]),
  cameraCase('source-fixture-reveal-visible-and-hidden', [
    resize(800, 600), reveal(416, 336, 20), reveal(800, 640, 20), reveal(80, 80, 20),
    reveal(900, 680, 10000), reveal(100, 100, 0),
  ]),
  cameraCase('source-fixture-resize-keeps-scroll-not-world-center', [
    resize(800, 600), pan(-100, -50), resize(900, 600), resize(600, 400),
    resize(1800, 1200), resize(800, 600),
  ]),
  cameraCase('first-usable-layout-and-reopening', [
    resize(0, 0, noInsets), resize(800, 0, noInsets), resize(800, 600),
    pan(-140, -65), resize(0, 0, noInsets), resize(800, 600),
  ]),
  cameraCase('small-scene-auto-centers-with-unequal-hud', [
    resize(800, 600), pan(-500, 500), center(0, 0), reveal(320, 240, 30),
    queueZoom(0.5), applyZoom, resize(1200, 1000),
  ], { x: 320, y: 240 }, noInsets, { x: 16, y: 16 }),
  cameraCase('authored-scene-insets-surround-the-field', [
    resize(800, 600), center(16, 16), center(752, 560), reveal(384, 288, 16),
    pan(2000, 2000), pan(-2000, -2000), queueZoom(-0.25), applyZoom,
    resize(1600, 1200), center(384, 288),
  ], { x: 768, y: 576 }, { left: 112, top: 48, right: 76, bottom: 120 }, { x: 336, y: 272 }),
  cameraCase('zoom-coalescing-and-minimum-maximum-clamps', [
    resize(800, 600), queueZoom(0.1), queueZoom(0.2), queueZoom(-0.05), applyZoom,
    queueZoom(100), applyZoom, queueZoom(1), applyZoom,
    queueZoom(-100), applyZoom, queueZoom(-1), applyZoom,
    queueZoom(0.25), queueZoom(-0.25), applyZoom, applyZoom,
  ]),
  cameraCase('coalesced-zoom-clamps-after-summing-not-per-input', [
    resize(800, 600), queueZoom(10), queueZoom(-10), applyZoom,
    queueZoom(-10), queueZoom(10), applyZoom, queueZoom(0.1), applyZoom,
  ]),
  cameraCase('zoom-preserves-latest-center-after-intervening-pan', [
    resize(1000, 700), queueZoom(0.25), pan(-160, -90), applyZoom,
    queueZoom(-0.5), center(800, 580), applyZoom,
    queueZoom(0.4), resize(920, 660), applyZoom,
  ], { x: 1920, y: 1280 }, { left: 32, top: 64, right: 96, bottom: 48 }, { x: 640, y: 480 }),
  cameraCase('fractional-geometry-and-asymmetric-insets', [
    resize(811.5, 603.25, { left: 93.5, top: 62.25, right: 117.75, bottom: 81.5 }),
    center(422.125, 313.75), pan(-17.375, 8.625), reveal(990.5, 610.25, 21.125),
    queueZoom(0.125), applyZoom,
  ], { x: 1024.5, y: 704.25 }, { left: 17.5, top: 33.25, right: 61.75, bottom: 8.5 }),
  cameraCase('one-axis-fits-and-padding-changes', [
    resize(800, 600), pan(-600, -600), resize(800, 600, noInsets),
    resize(800, 600, { left: 200, top: 20, right: 20, bottom: 200 }),
    queueZoom(0.75), applyZoom, reveal(160, 900, 60),
  ], { x: 320, y: 960 }, { left: 20, top: 40, right: 80, bottom: 0 }, { x: 160, y: 480 }),
];

const pointerInput = (
  kind: PointerInput['kind'], x = 140, y = 90,
  options: Omit<PointerInput, 'kind' | 'point'> = {},
): PointerInput => ({ kind, point: { x, y }, ...options });
const pointerCase = (
  name: string, operations: PointerOperation[],
  box: Box = { left: 100, top: 50, width: 500, height: 500 },
): PointerCase => ({ name, scene: { width: 1000, height: 1000 }, box, operations });

const pointerCases: PointerCase[] = [
  pointerCase('stationary-click-waits-for-release', [pointerInput('down'), pointerInput('up')]),
  pointerCase('release-without-down-does-not-click', [pointerInput('up')]),
  pointerCase('release-only-small-movement-clicks-current-point', [pointerInput('down'), pointerInput('up', 143, 92)]),
  pointerCase('exactly-six-pixels-starts-pan', [pointerInput('down'), pointerInput('move', 146, 90), pointerInput('up', 146, 90)]),
  pointerCase('five-point-999-pixels-stays-click', [pointerInput('down', 0, 0), pointerInput('move', 5.999, 0), pointerInput('up', 5.999, 0)]),
  pointerCase('diagonal-six-pixels-starts-pan', [pointerInput('down', 0, 0), pointerInput('move', 3.6, 4.8), pointerInput('up', 3.6, 4.8)]),
  pointerCase('first-pan-includes-earlier-subthreshold-movement', [
    pointerInput('down'), pointerInput('move', 143, 92), pointerInput('move', 144, 93),
    pointerInput('move', 147, 94), pointerInput('up', 150, 96),
  ]),
  pointerCase('dragging-back-never-becomes-a-click', [
    pointerInput('down'), pointerInput('move', 180, 110), pointerInput('leave'),
    pointerInput('move'), pointerInput('up'), pointerInput('move', 150, 95), pointerInput('leave'),
  ]),
  pointerCase('release-only-drag', [pointerInput('down'), pointerInput('up', 180, 90)]),
  pointerCase('middle-button-drag-and-stationary-release', [
    pointerInput('down', 140, 90, { button: 1 }), pointerInput('move', 160, 100, { button: 1 }),
    pointerInput('up', 160, 100, { button: 1 }),
    pointerInput('down', 140, 90, { button: 1 }), pointerInput('up', 140, 90, { button: 1 }),
  ]),
  pointerCase('right-button-press-is-immediate', [
    pointerInput('down', 140, 90, { button: 2 }), pointerInput('move', 160, 100, { button: 2 }),
    pointerInput('up', 160, 100, { button: 2 }),
  ]),
  pointerCase('foreign-pointer-cannot-steal-or-release-gesture', [
    pointerInput('down'), pointerInput('down', 180, 90, { pointerId: 2 }),
    pointerInput('move', 240, 90, { pointerId: 2 }), pointerInput('up', 240, 90, { pointerId: 2 }),
    pointerInput('cancel', 140, 90, { pointerId: 2 }), pointerInput('lostCapture', 140, 90, { pointerId: 2 }),
    pointerInput('up'),
  ]),
  pointerCase('foreign-button-cannot-release-primary-gesture', [
    pointerInput('down'), pointerInput('down', 140, 90, { button: 2 }),
    pointerInput('up', 140, 90, { button: 1 }), pointerInput('up', 140, 90, { button: 2 }), pointerInput('up'),
  ]),
  pointerCase('move-button-does-not-change-owning-button', [
    pointerInput('down'), pointerInput('move', 150, 100, { button: -1 }), pointerInput('up', 150, 100),
  ]),
  pointerCase('nonprimary-touch-does-not-start-gesture', [
    pointerInput('down', 140, 90, { pointerId: 2, isPrimary: false, pointerType: 'touch' }),
    pointerInput('up', 140, 90, { pointerId: 2, isPrimary: false, pointerType: 'touch' }),
    pointerInput('down', 140, 90, { pointerType: 'touch' }), pointerInput('up', 140, 90, { pointerType: 'touch' }),
  ]),
  ...(['cancel', 'lostCapture'] as const).map((kind) => pointerCase(`${kind}-ends-pending-and-active-drag`, [
    pointerInput('down'), pointerInput(kind), pointerInput('up'),
    pointerInput('down'), pointerInput('move', 170, 100), pointerInput(kind), pointerInput('up', 170, 100),
    pointerInput('down'), pointerInput('up'),
  ])),
  pointerCase('geometry-is-read-at-release-not-at-press', [
    pointerInput('down'), { kind: 'setBox', box: { left: 90, top: 40, width: 1000, height: 500 } },
    pointerInput('up'), pointerInput('down'),
    { kind: 'setBox', box: { left: 110, top: 65, width: 250, height: 500 } }, pointerInput('up'),
  ]),
  pointerCase('letterbox-points-are-not-clamped-to-scene', [
    pointerInput('move', 110, 60), pointerInput('down', 110, 60, { button: 2 }),
    pointerInput('move', 740, 420), pointerInput('leave'),
  ], { left: 100, top: 50, width: 640, height: 320 }),
  pointerCase('zero-size-scene-box-has-no-pointer-point', [
    pointerInput('move'), pointerInput('down'), pointerInput('up'),
    pointerInput('down', 140, 90, { button: 2 }),
    { kind: 'setBox', box: { left: 100, top: 50, width: 500, height: 0 } },
    pointerInput('move'), pointerInput('down'), pointerInput('up'),
  ], { left: 100, top: 50, width: 0, height: 500 }),
  pointerCase('ordinary-control-meta-and-zero-wheel', [
    { kind: 'wheel', deltaY: 100 }, { kind: 'wheel', deltaY: -100 },
    { kind: 'wheel', deltaY: 100, ctrlKey: true }, { kind: 'wheel', deltaY: -100, metaKey: true },
    { kind: 'wheel', deltaY: 0, ctrlKey: true }, { kind: 'wheel', deltaY: 0, metaKey: true },
    { kind: 'wheel', deltaY: -0.001, ctrlKey: true, metaKey: true },
  ]),
  pointerCase('dispose-removes-active-drag-and-all-listeners', [
    pointerInput('down'), pointerInput('move', 170, 100), { kind: 'dispose' },
    pointerInput('up', 170, 100), pointerInput('down'), pointerInput('move', 180, 110),
    pointerInput('up', 180, 110), pointerInput('leave'),
    pointerInput('down', 140, 90, { button: 2 }), { kind: 'wheel', deltaY: 1, ctrlKey: true },
    { kind: 'dispose' },
  ]),
  pointerCase('dispose-cancels-unreleased-click', [pointerInput('down'), { kind: 'dispose' }, pointerInput('up')]),
];

const output = process.env.EMPIRE_CAMERA_ORACLE_OUTPUT;
if (!output) throw new Error('Set EMPIRE_CAMERA_ORACLE_OUTPUT to the oracle JSON output path.');
const originalComputedStyle = Object.getOwnPropertyDescriptor(globalThis, 'getComputedStyle');
Object.defineProperty(globalThis, 'getComputedStyle', {
  configurable: true,
  value: dom.window.getComputedStyle.bind(dom.window),
});
try {
  writeFileSync(output, JSON.stringify({
    schemaVersion: 1,
    cameraCases: cameraCases.map((input) => ({ ...input, expected: cameraOracle(input) })),
    pointerCases: pointerCases.map((input) => ({ ...input, expected: pointerOracle(input) })),
  }, null, 2) + '\n');
} finally {
  if (originalComputedStyle) Object.defineProperty(globalThis, 'getComputedStyle', originalComputedStyle);
  else Reflect.deleteProperty(globalThis, 'getComputedStyle');
  dom.window.close();
}
