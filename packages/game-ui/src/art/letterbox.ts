import type { BoardPointer } from './board-surface';

/**
 * Where a pointer is, in scene units, for a picture letterboxed inside its box.
 *
 * Shared by every backend rather than reimplemented per renderer: two surfaces that
 * disagree about where a click landed would be two different games. The scale is
 * the smaller of the two ratios and the leftover is split evenly, which is what an
 * SVG `viewBox` does by default and what a GPU backend has to be told to do.
 */
export interface SceneBox {
  readonly width: number;
  readonly height: number;
}

export interface ShownAt {
  /** Scene units per presented pixel. */
  readonly scale: number;
  /** Where the picture's own origin sits inside the box. */
  readonly left: number;
  readonly top: number;
}

/** How a scene of this size is shown inside a box of that size. */
export function shownAt(box: { width: number; height: number }, scene: SceneBox): ShownAt | null {
  if (box.width <= 0 || box.height <= 0) return null;
  const scale = Math.min(box.width / scene.width, box.height / scene.height);
  return {
    scale,
    left: (box.width - scene.width * scale) / 2,
    top: (box.height - scene.height * scale) / 2,
  };
}

/**
 * Every event a board answers, wired onto one element.
 *
 * The two backends had a copy each — the same listeners, the same policy that
 * a wheel with ctrl or meta held is a zoom and a right-click is a secondary press —
 * and the DOM one also carried a second copy of the letterbox arithmetic this
 * module exists to own, in a method whose comment called it "the only geometry this
 * renderer owns". It was not: the GPU backend already read `scenePointOf` from
 * here, so the two surfaces held two answers to where a click landed, which is two
 * different games.
 *
 * A backend still owns *what* it is listening on. This owns what the listening
 * means — and it hands back the way to stop, because one of the two backends draws
 * every battle of a session onto *one* canvas. Listeners went onto it per
 * battle and none came off, so the third battle's click also reached the first
 * two — boards that had been disposed.
 */
export function listenForPointer(
  element: Element,
  scene: SceneBox,
  pointer: BoardPointer,
): () => void {
  const at = (event: Event) =>
    scenePointOf(element.getBoundingClientRect(), scene, event as MouseEvent);
  let gesture: {
    id: number;
    button: number;
    startX: number;
    startY: number;
    x: number;
    y: number;
    dragging: boolean;
  } | null = null;

  const stopGesture = () => {
    const ended = gesture;
    gesture = null;
    element.classList.remove('is-panning');
    if (ended && element.hasPointerCapture?.(ended.id)) element.releasePointerCapture(ended.id);
  };

  const moveGesture = (event: PointerEvent) => {
    if (!gesture || (event.pointerId ?? 0) !== gesture.id) return;
    const dx = event.clientX - gesture.x;
    const dy = event.clientY - gesture.y;
    if (!gesture.dragging
      && Math.hypot(event.clientX - gesture.startX, event.clientY - gesture.startY) >= 6) {
      gesture.dragging = true;
      element.classList.add('is-panning');
      pointer.leave();
    }
    if (!gesture.dragging) return;
    event.preventDefault();
    pointer.pan({ x: dx, y: dy });
    gesture.x = event.clientX;
    gesture.y = event.clientY;
  };

  const listeners: [string, (event: Event) => void, AddEventListenerOptions?][] = [
    ['pointerdown', (event) => {
      const input = event as PointerEvent;
      if (gesture || input.isPrimary === false) return;
      if (input.button === 0 || input.button === 1) {
        gesture = {
          id: input.pointerId ?? 0,
          button: input.button,
          startX: input.clientX, startY: input.clientY,
          x: input.clientX, y: input.clientY,
          dragging: false,
        };
        // Keep a drag alive outside the board, including across floating HUDs.
        if (input.isTrusted) element.setPointerCapture(input.pointerId);
        event.preventDefault();
        return;
      }
      const point = at(event);
      if (point) pointer.press(point, input.button);
    }],
    ['pointerup', (event) => {
      const input = event as PointerEvent;
      if (!gesture || (input.pointerId ?? 0) !== gesture.id || input.button !== gesture.button) return;
      moveGesture(input);
      const click = !gesture.dragging && gesture.button === 0;
      stopGesture();
      const point = at(event);
      if (click && point) pointer.press(point, 0);
    }],
    ['pointercancel', (event) => {
      if (((event as PointerEvent).pointerId ?? 0) === gesture?.id) stopGesture();
    }],
    ['lostpointercapture', (event) => {
      if (((event as PointerEvent).pointerId ?? 0) === gesture?.id) stopGesture();
    }],
    ['dragstart', (event) => event.preventDefault()],
    ['pointermove', (event) => {
      if (gesture) moveGesture(event as PointerEvent);
      else pointer.move(at(event));
    }],
    ['pointerleave', () => {
      if (!gesture) pointer.leave();
    }],
    ['contextmenu', (event) => event.preventDefault()],
    ['wheel', (event) => {
      const wheel = event as WheelEvent;
      // Ctrl or meta held: the gesture every application uses for zoom, and the
      // one a trackpad pinch arrives as. Anything else is the page scrolling.
      if (!wheel.ctrlKey && !wheel.metaKey) return;
      wheel.preventDefault();
      pointer.scale(-Math.sign(wheel.deltaY));
    }, { passive: false }],
  ];

  for (const [type, handler, options] of listeners) element.addEventListener(type, handler, options);
  return () => {
    for (const [type, handler, options] of listeners) element.removeEventListener(type, handler, options);
    stopGesture();
  };
}

/**
 * A point on the page, as a point in the scene, or `null` if nothing is shown.
 *
 * Not exported: `listenForPointer` above is the only thing that ever needed it, and
 * an export nobody outside this module names is a promise made to nobody. Its last
 * outside reader was the GPU backend's own `listen`, which this module now owns.
 */
function scenePointOf(
  box: { left: number; top: number; width: number; height: number },
  scene: SceneBox,
  page: { clientX: number; clientY: number },
): { x: number; y: number } | null {
  const shown = shownAt(box, scene);
  if (!shown) return null;
  return {
    x: (page.clientX - box.left - shown.left) / shown.scale,
    y: (page.clientY - box.top - shown.top) / shown.scale,
  };
}
