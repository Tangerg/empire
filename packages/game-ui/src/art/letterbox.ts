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
 * The two backends had a copy each — the same five listeners, the same policy that
 * a wheel with ctrl or meta held is a zoom and a right-click is a secondary press —
 * and the DOM one also carried a second copy of the letterbox arithmetic this
 * module exists to own, in a method whose comment called it "the only geometry this
 * renderer owns". It was not: the GPU backend already read `scenePointOf` from
 * here, so the two surfaces held two answers to where a click landed, which is two
 * different games.
 *
 * A backend still owns *what* it is listening on. This owns what the listening
 * means — and it hands back the way to stop, because one of the two backends draws
 * every battle of a session onto *one* canvas. Five listeners went onto it per
 * battle and none came off, so the third battle's click also reached the first
 * two — boards that had been disposed.
 */
export function listenForPointer(
  element: Element,
  scene: SceneBox,
  pointer: {
    press(at: { x: number; y: number }, button: number): void;
    move(at: { x: number; y: number } | null): void;
    leave(): void;
    scale(notches: number): void;
  },
): () => void {
  const at = (event: Event) =>
    scenePointOf(element.getBoundingClientRect(), scene, event as MouseEvent);

  const listeners: [string, (event: Event) => void, AddEventListenerOptions?][] = [
    ['pointerdown', (event) => {
      const point = at(event);
      if (point) pointer.press(point, (event as MouseEvent).button);
    }],
    ['contextmenu', (event) => event.preventDefault()],
    ['pointermove', (event) => pointer.move(at(event))],
    ['pointerleave', () => pointer.leave()],
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
