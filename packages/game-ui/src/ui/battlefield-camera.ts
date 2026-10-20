interface Point {
  x: number;
  y: number;
}

/**
 * The scroll position is the camera. The board keeps its readable pixel scale;
 * padding reserves the HUD's bands, and every oversized edge remains reachable.
 */
export class BattlefieldCamera {
  constructor(
    private readonly viewport: HTMLElement,
    private readonly board: Element,
  ) {}

  private get frame(): {
    origin: Point;
    center: Point;
    left: number;
    top: number;
    right: number;
    bottom: number;
  } {
    const viewport = this.viewport.getBoundingClientRect();
    const board = this.board.getBoundingClientRect();
    const style = getComputedStyle(this.viewport);
    const padding = (value: string) => Number.parseFloat(value) || 0;
    const left = padding(style.paddingLeft);
    const top = padding(style.paddingTop);
    const right = this.viewport.clientWidth - padding(style.paddingRight);
    const bottom = this.viewport.clientHeight - padding(style.paddingBottom);
    return {
      origin: {
        x: board.left - viewport.left + this.viewport.scrollLeft,
        y: board.top - viewport.top + this.viewport.scrollTop,
      },
      center: {
        x: (left + right) / 2,
        y: (top + bottom) / 2,
      },
      left, top, right, bottom,
    };
  }

  /** The visible tactical centre, relative to the board's top-left in pixels. */
  get position(): Point {
    const { origin, center } = this.frame;
    return {
      x: this.viewport.scrollLeft + center.x - origin.x,
      y: this.viewport.scrollTop + center.y - origin.y,
    };
  }

  centerOn(point: Point, behavior: ScrollBehavior = 'smooth'): void {
    const { origin, center } = this.frame;
    this.scrollTo(origin.x + point.x - center.x, origin.y + point.y - center.y, behavior);
  }

  pan(delta: Point): void {
    this.scrollTo(this.viewport.scrollLeft - delta.x, this.viewport.scrollTop - delta.y, 'instant');
  }

  /** Keyboard navigation scrolls only when the cursor leaves the useful view. */
  reveal(point: Point, margin: number): void {
    const { origin, left, top, right, bottom } = this.frame;
    const x = origin.x + point.x - this.viewport.scrollLeft;
    const y = origin.y + point.y - this.viewport.scrollTop;
    const marginX = Math.min(margin, Math.max(0, (right - left) / 2));
    const marginY = Math.min(margin, Math.max(0, (bottom - top) / 2));
    const dx = x - Math.max(left + marginX, Math.min(x, right - marginX));
    const dy = y - Math.max(top + marginY, Math.min(y, bottom - marginY));
    if (dx || dy) this.scrollTo(this.viewport.scrollLeft + dx, this.viewport.scrollTop + dy, 'instant');
  }

  resize(): void {
    this.scrollTo(this.viewport.scrollLeft, this.viewport.scrollTop, 'instant');
  }

  private scrollTo(x: number, y: number, behavior: ScrollBehavior): void {
    this.viewport.scrollTo({
      left: Math.max(0, Math.min(x, this.viewport.scrollWidth - this.viewport.clientWidth)),
      top: Math.max(0, Math.min(y, this.viewport.scrollHeight - this.viewport.clientHeight)),
      behavior,
    });
  }
}
