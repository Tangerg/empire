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

  register(
    id: string,
    target: FrameAnimationTarget,
    clips: readonly FrameAnimationClip[],
    initialFrame = 0,
  ): void {
    if (!id) throw new Error('frame animation target id cannot be empty');
    if (!Number.isInteger(target.frameCount) || target.frameCount < 1) {
      throw new Error('frame animation target frameCount must be a positive integer');
    }
    if (!Number.isInteger(initialFrame) || initialFrame < 0 || initialFrame >= target.frameCount) {
      throw new Error(`invalid initial frame ${initialFrame}`);
    }
    const catalog = new Map<string, FrameAnimationClip>();
    for (const clip of clips) {
      validateClip(clip, target.frameCount);
      if (catalog.has(clip.id)) throw new Error(`duplicate frame animation clip: ${clip.id}`);
      catalog.set(clip.id, Object.freeze({ ...clip, frames: Object.freeze([...clip.frames]) }));
    }
    this.tracks.set(id, { target, clips: catalog, clip: null, startedAt: 0, frame: initialFrame });
    target.setFrame(initialFrame);
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

interface StripMetadata {
  frameWidth: number;
  frameCount: number;
  initialFrame: number;
  clips: FrameAnimationClip[];
}

function numberAttribute(element: Element, name: string, min: number): number {
  const value = Number(element.getAttribute(name));
  if (!Number.isFinite(value) || value < min) throw new Error(`${name} must be a number >= ${min}`);
  return value;
}

export function stripMetadata(element: SVGImageElement): StripMetadata {
  const frameWidth = numberAttribute(element, 'data-frame-width', 0.001);
  const frameCount = numberAttribute(element, 'data-frame-count', 1);
  const initialFrame = numberAttribute(element, 'data-frame-initial', 0);
  if (!Number.isInteger(frameCount) || !Number.isInteger(initialFrame) || initialFrame >= frameCount) {
    throw new Error('invalid sprite strip frame metadata');
  }
  const raw = element.getAttribute('data-frame-clips') ?? '[]';
  const clips = JSON.parse(raw) as FrameAnimationClip[];
  if (!Array.isArray(clips)) throw new Error('data-frame-clips must be an array');
  return { frameWidth, frameCount, initialFrame, clips };
}

/** Register a self-describing SVG <image> emitted by runtime-raster. */
export function registerSvgStrip(
  system: FrameAnimationSystem,
  id: string,
  element: SVGImageElement,
): void {
  const metadata = stripMetadata(element);
  system.register(
    id,
    {
      frameCount: metadata.frameCount,
      setFrame: (frame) => element.setAttribute('x', String(-frame * metadata.frameWidth)),
    },
    metadata.clips,
    metadata.initialFrame,
  );
}
