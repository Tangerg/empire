/**
 * A test that is the clock.
 *
 * Every animation in this package asks the global `requestAnimationFrame` for its
 * next frame, so a test can hand out frames itself and assert what happened
 * between them — no waiting, no flake. Two suites hand-rolled that: one queued
 * callbacks and counted the requests, the other queued them and did not, and each
 * restored the real functions in its own `afterEach`. Same obligation, two
 * implementations, so a change to what the timeline asks for could keep one of them
 * green.
 *
 * `requests` is the count since the last `pump`, which is how a test tells a
 * timeline that has stopped asking from one that is still running.
 */
export class FrameClock {
  private pending: Array<(time: number) => void> = [];
  private realRequest: typeof requestAnimationFrame | null = null;
  private realCancel: typeof cancelAnimationFrame | null = null;
  /** When the clock started, so a test can say "two hundred milliseconds in". */
  private base = 0;

  /** How many frames were asked for since the last `pump`. */
  requests = 0;

  /** Takes over the frame source. Call from `beforeEach`. */
  install(): void {
    this.realRequest = globalThis.requestAnimationFrame;
    this.realCancel = globalThis.cancelAnimationFrame;
    this.pending = [];
    this.requests = 0;
    this.base = performance.now();
    globalThis.requestAnimationFrame = ((callback: (time: number) => void) => {
      this.requests += 1;
      this.pending.push(callback);
      return this.requests;
    }) as typeof requestAnimationFrame;
    // Nothing to cancel: a frame this clock never runs is a frame that never fires.
    globalThis.cancelAnimationFrame = (() => {}) as typeof cancelAnimationFrame;
  }

  /** Gives the frame source back. Call from `afterEach`. */
  restore(): void {
    if (this.realRequest) globalThis.requestAnimationFrame = this.realRequest;
    if (this.realCancel) globalThis.cancelAnimationFrame = this.realCancel;
    this.realRequest = null;
    this.realCancel = null;
  }

  /**
   * Runs the pending frame as if `ms` had passed since the clock was installed.
   *
   * For a test about *when* something happens — a clip at eight frames a second is
   * on its second frame two hundred milliseconds in — rather than about how many
   * frames were asked for. Both suites used to reach into the queue themselves and
   * call it with a timestamp they had computed.
   */
  at(ms: number): void {
    const due = this.pending;
    this.pending = [];
    for (const callback of due) callback(this.base + ms);
  }

  /**
   * Runs the frames that were asked for, and answers how many were asked for next.
   *
   * Each frame runs only what was pending when it started, so a callback that asks
   * for another frame is served by the *next* one rather than looping forever.
   */
  pump(frames = 1): number {
    this.requests = 0;
    for (let frame = 0; frame < frames; frame++) {
      const due = this.pending;
      this.pending = [];
      for (const callback of due) callback(performance.now() + frame * 16);
    }
    return this.requests;
  }
}
