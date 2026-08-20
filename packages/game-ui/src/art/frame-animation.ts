/** A named sequence in a horizontal sprite strip. */
export interface FrameAnimationClip {
  id: string;
  frames: readonly number[];
  fps: number;
  loop?: boolean;
}

export interface FrameAnimationTarget {
  frameCount: number;
  setFrame(frame: number): void;
}

export interface FrameAnimationDriver {
  now(): number;
  request(callback: FrameRequestCallback): number;
  cancel(handle: number): void;
}

interface Track {
  target: FrameAnimationTarget;
  clips: ReadonlyMap<string, FrameAnimationClip>;
  clip: FrameAnimationClip | null;
  startedAt: number;
  frame: number;
}

const browserDriver: FrameAnimationDriver = {
  now: () => performance.now(),
  request: (callback) => requestAnimationFrame(callback),
  cancel: (handle) => cancelAnimationFrame(handle),
};

const browserMotionEnabled = (): boolean =>
  typeof matchMedia === 'undefined' || !matchMedia('(prefers-reduced-motion: reduce)').matches;

function validateClip(clip: FrameAnimationClip, frameCount: number): void {
  if (!clip.id) throw new Error('frame animation clip id cannot be empty');
  if (!Number.isFinite(clip.fps) || clip.fps <= 0) {
    throw new Error(`frame animation ${clip.id} fps must be greater than zero`);
  }
  if (!clip.frames.length) throw new Error(`frame animation ${clip.id} must contain at least one frame`);
  for (const frame of clip.frames) {
    if (!Number.isInteger(frame) || frame < 0 || frame >= frameCount) {
      throw new Error(`frame animation ${clip.id} references invalid frame ${frame}`);
    }
  }
}

/**
 * Shared timeline for sprite strips. It is presentation-only, owns one RAF loop
 * for every registered sprite, and sleeps while no multi-frame clip is active.
 */
export class FrameAnimationSystem {
  private readonly tracks = new Map<string, Track>();
  private requestHandle: number | null = null;

  constructor(
    private readonly driver: FrameAnimationDriver = browserDriver,
    private readonly motionEnabled: () => boolean = browserMotionEnabled,
  ) {}

  /**
   * Puts a target on the timeline, standing on its first frame.
   *
   * There was an `initialFrame` parameter, validated against the frame count and
   * passed by exactly one caller — the DOM strip reader, which read it out of an
   * attribute it had itself written as `0` every time.
   */
  register(
    id: string,
    target: FrameAnimationTarget,
    clips: readonly FrameAnimationClip[],
  ): void {
    if (!id) throw new Error('frame animation target id cannot be empty');
    if (!Number.isInteger(target.frameCount) || target.frameCount < 1) {
      throw new Error('frame animation target frameCount must be a positive integer');
    }
    const catalog = new Map<string, FrameAnimationClip>();
    for (const clip of clips) {
      validateClip(clip, target.frameCount);
      if (catalog.has(clip.id)) throw new Error(`duplicate frame animation clip: ${clip.id}`);
      catalog.set(clip.id, Object.freeze({ ...clip, frames: Object.freeze([...clip.frames]) }));
    }
    this.tracks.set(id, { target, clips: catalog, clip: null, startedAt: 0, frame: 0 });
    target.setFrame(0);
  }

  unregister(id: string): void {
    this.tracks.delete(id);
    this.pauseLoopIfIdle();
  }

  has(id: string): boolean {
    return this.tracks.has(id);
  }

  play(id: string, clipId: string, restart = true): void {
    const track = this.track(id);
    const clip = track.clips.get(clipId);
    if (!clip) throw new Error(`unknown frame animation clip: ${clipId}`);
    if (!restart && track.clip?.id === clipId) return;
    track.clip = clip;
    track.startedAt = this.driver.now();
    this.applyFrame(track, clip.frames[0]);
    if (!this.motionEnabled()) {
      track.clip = null;
      this.pauseLoopIfIdle();
      return;
    }
    if (clip.frames.length > 1) this.ensureLoop();
  }

  stop(id: string, frame?: number): void {
    const track = this.track(id);
    track.clip = null;
    if (frame !== undefined) {
      if (!Number.isInteger(frame) || frame < 0 || frame >= track.target.frameCount) {
        throw new Error(`invalid stopped frame ${frame}`);
      }
      this.applyFrame(track, frame);
    }
    this.pauseLoopIfIdle();
  }

  dispose(): void {
    if (this.requestHandle !== null) this.driver.cancel(this.requestHandle);
    this.requestHandle = null;
    this.tracks.clear();
  }

  private readonly tick = (now: number): void => {
    this.requestHandle = null;
    for (const track of this.tracks.values()) {
      const clip = track.clip;
      if (!clip || clip.frames.length < 2) continue;
      const elapsedFrames = Math.max(0, Math.floor(((now - track.startedAt) * clip.fps) / 1000));
      if (!clip.loop && elapsedFrames >= clip.frames.length) {
        this.applyFrame(track, clip.frames[clip.frames.length - 1]);
        track.clip = null;
        continue;
      }
      const index = clip.loop ? elapsedFrames % clip.frames.length : elapsedFrames;
      this.applyFrame(track, clip.frames[index]);
    }
    if (this.hasRunningTimeline()) this.ensureLoop();
  };

  private applyFrame(track: Track, frame: number): void {
    if (track.frame === frame) return;
    track.frame = frame;
    track.target.setFrame(frame);
  }

  private track(id: string): Track {
    const track = this.tracks.get(id);
    if (!track) throw new Error(`unknown frame animation target: ${id}`);
    return track;
  }

  private hasRunningTimeline(): boolean {
    for (const track of this.tracks.values()) {
      if (track.clip && track.clip.frames.length > 1) return true;
    }
    return false;
  }

  private ensureLoop(): void {
    if (this.requestHandle === null) this.requestHandle = this.driver.request(this.tick);
  }

  private pauseLoopIfIdle(): void {
    if (this.requestHandle !== null && !this.hasRunningTimeline()) {
      this.driver.cancel(this.requestHandle);
      this.requestHandle = null;
    }
  }
}

/*
 * There was a `registerSvgStrip` here, and it is why this module used to know what
 * a DOM is.
 *
 * It took an `<image>` element, read `data-frame-width`, `data-frame-count`,
 * `data-frame-initial` and `JSON.parse(data-frame-clips)` off it, validated all
 * four against the possibility of being malformed, and registered a target that
 * shifted the element's `x`. Every one of those attributes had been written by
 * `runtimeFrameStripMarkup` from data of exactly this shape, and the only reason
 * they existed was that a strip crossed the renderer seam as a string.
 *
 * A strip is declared now — `BoardStrip` — so a backend registers a target for the
 * strip it drew, in whatever way it draws one. This module is what it always
 * claimed to be: a timeline, with no opinion about what a frame looks like.
 */
