/**
 * Whether the built game survives being played.
 *
 * `ui:shot` answers "does it look right" for one mounted screen. `sweep` answers
 * "do the rules still produce the same battle". Neither of them clicks anything,
 * and the tests that do click run in jsdom — which has no layout, no real pointer
 * plumbing and no bundler. So the whole interactive path *through the shipped
 * bundle* was checked by a person opening a browser, which means it was checked
 * whenever someone remembered.
 *
 * This drives the built app in a real browser: title screen, a skirmish, every
 * cell of the board clicked, a turn ended, the AI answered, the campaign entered.
 * It reports every uncaught exception, every `console.error` and every failed
 * request, and exits non-zero if there were any.
 *
 *   npm run build:apps && npm run ui:play
 *   npm run ui:play -- --keep     # leave the screenshots in .shots/play-*.png
 *
 * A probe, not a test: nothing here asserts a pixel or a rule. It asserts that
 * playing the game does not throw, which is the one thing no other ruler covers.
 */
import { execFile } from 'node:child_process';
import { createServer } from 'node:http';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { extname, join, normalize, resolve } from 'node:path';

const CHROME = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
const ROOT = resolve('dist');
const OUT = resolve('.shots');
const PORT = 8231;
const DEBUG_PORT = 9333;
const VIEWPORT = { width: 1440, height: 900 };

const TYPES: Record<string, string> = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.woff2': 'font/woff2',
};

/** Everything the page complained about, in the order it complained. */
const trouble: string[] = [];

function serve(): { close: () => void } {
  const server = createServer((request, response) => {
    const path = (request.url ?? '/').split('?')[0]!;
    const file = join(ROOT, normalize(path.endsWith('/') ? `${path}index.html` : path));
    if (!file.startsWith(ROOT)) {
      response.writeHead(403).end();
      return;
    }
    try {
      const body = readFileSync(file);
      response.writeHead(200, { 'content-type': TYPES[extname(file)] ?? 'application/octet-stream' });
      response.end(body);
    } catch {
      response.writeHead(404).end();
    }
  });
  server.listen(PORT);
  return { close: () => server.close() };
}

/**
 * The parts of the debugging protocol this tool reads.
 *
 * Declared rather than `any`-ed: the protocol is untyped JSON on the wire, and
 * `as any` is how an untyped edge spreads inward. Everything below is a *read* of a
 * reply or an event, so `unknown` values are narrowed at the one place that knows
 * what they are.
 */
interface CdpReply {
  readonly id?: number;
  readonly method?: string;
  readonly error?: unknown;
  readonly result?: CdpResult;
  readonly params?: CdpParams;
}

interface CdpResult {
  readonly timedOut?: boolean;
  readonly data?: string;
  readonly result?: { readonly value?: unknown };
  readonly exceptionDetails?: { readonly exception?: { readonly description?: string } };
  readonly metrics?: readonly { readonly name: string; readonly value: number }[];
}

interface CdpParams {
  readonly type?: string;
  readonly args?: readonly { readonly description?: string; readonly value?: unknown }[];
  readonly entry?: { readonly level?: string; readonly text?: string; readonly url?: string };
  readonly exceptionDetails?: {
    readonly text?: string;
    readonly exception?: { readonly description?: string };
  };
}

/** A page target as `/json/list` reports it. */
interface PageTarget {
  readonly type: string;
  readonly webSocketDebuggerUrl: string;
}

/** One CDP connection to the page target, with a promise per command id. */
class Session {
  private next = 1;
  private readonly pending = new Map<
    number,
    { resolve: (value: CdpResult) => void; reject: (error: Error) => void }
  >();

  private constructor(private readonly socket: WebSocket) {
    socket.addEventListener('message', (event) => {
      const message = JSON.parse(String((event as MessageEvent).data)) as CdpReply;
      if (message.id !== undefined) {
        const waiting = this.pending.get(message.id);
        this.pending.delete(message.id);
        if (!waiting) return;
        if (message.error) waiting.reject(new Error(JSON.stringify(message.error)));
        else waiting.resolve(message.result ?? {});
        return;
      }
      note(message);
    });
  }

  static async open(url: string): Promise<Session> {
    const socket = new WebSocket(url);
    await new Promise<void>((done, fail) => {
      socket.addEventListener('open', () => done(), { once: true });
      socket.addEventListener('error', () => fail(new Error('cannot reach the browser')), { once: true });
    });
    return new Session(socket);
  }

  /**
   * One command, with a deadline.
   *
   * A command whose reply never arrives used to hang this tool forever: the page's
   * execution context can be destroyed between `Runtime.evaluate` and its answer,
   * and a screenshot of a wedged renderer never comes back. A ruler that hangs is
   * worse than one that reports — it looks like a slow build.
   */
  send(
    method: string,
    params: Record<string, unknown> = {},
    what = '',
    deadline = 20_000,
  ): Promise<CdpResult> {
    const id = this.next++;
    this.socket.send(JSON.stringify({ id, method, params }));
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        trouble.push(`timed out after ${deadline}ms: ${method}${what ? ` while ${what}` : ''}`);
        resolve({ timedOut: true });
      }, deadline);
      this.pending.set(id, {
        resolve: (value) => { clearTimeout(timer); resolve(value); },
        reject: (error) => { clearTimeout(timer); reject(error); },
      });
    });
  }

  /** Runs an expression in the page and returns its value, refusing a thrown one. */
  async eval<T>(expression: string, what = ''): Promise<T> {
    const result = await this.send('Runtime.evaluate', {
      expression: `(async () => { ${expression} })()`,
      awaitPromise: true,
      returnByValue: true,
    }, what);
    if (result.timedOut) return undefined as T;
    if (result.exceptionDetails) {
      throw new Error(result.exceptionDetails.exception?.description ?? 'page threw');
    }
    return result.result?.value as T;
  }

  close(): void {
    this.socket.close();
  }
}

/** What the page said, kept only when it is a complaint. */
function note(message: CdpReply): void {
  const { method } = message;
  const params = message.params ?? {};
  if (method === 'Runtime.exceptionThrown') {
    const detail = params.exceptionDetails;
    trouble.push(`exception: ${detail?.exception?.description ?? detail?.text ?? 'unknown'}`);
  }
  if (method === 'Runtime.consoleAPICalled' && (params.type === 'error' || params.type === 'assert')) {
    const said = (params.args ?? []).map((arg) => arg.description ?? String(arg.value)).join(' ');
    trouble.push(`console.${params.type}: ${said}`);
  }
  if (method === 'Inspector.targetCrashed') {
    trouble.push('the page crashed');
  }
  if (method === 'Log.entryAdded' && params.entry?.level === 'error') {
    // A headless browser asks for a favicon nobody ships; that is the browser's
    // habit, not the game's defect.
    if (String(params.entry.url ?? '').endsWith('/favicon.ico')) return;
    trouble.push(`log: ${params.entry.text ?? ''} ${params.entry.url ?? ''}`);
  }
}

const sleep = (ms: number) => new Promise<void>((done) => setTimeout(done, ms));

/**
 * A click where the board thinks that cell is.
 *
 * The board deliberately puts no per-cell handle in the DOM — a painted scene is
 * pieces, not a grid of elements — so a cell has to be found the way the pointer
 * finds it: through the viewBox and the letterbox. This is that arithmetic, in the
 * page, against the element the app actually mounted.
 */
const CLICK_CELL = (x: number, y: number) => `
  const svg = document.querySelector('svg.board') ?? document.querySelector('canvas');
  if (!svg) return false;
  const box = svg.getBoundingClientRect();
  const view = svg.viewBox?.baseVal ?? { x: 0, y: 0, width: box.width, height: box.height };
  const scale = Math.min(box.width / view.width, box.height / view.height);
  // The field's own cell (0,0) is at scene (0,0); a painted scene's margin is the
  // *negative* part of the viewBox. Adding \`view.x\` put cell (0,0) in the margin
  // and slid the whole sweep off the field's right and bottom edges.
  const point = {
    clientX: box.left + (box.width - view.width * scale) / 2 + (-view.x + (${x} + 0.5) * 32) * scale,
    clientY: box.top + (box.height - view.height * scale) / 2 + (-view.y + (${y} + 0.5) * 32) * scale,
  };
  svg.dispatchEvent(new PointerEvent('pointermove', { ...point, bubbles: true }));
  svg.dispatchEvent(new PointerEvent('pointerdown', { ...point, bubbles: true, button: 0 }));
  return true;
`;

const CLICK = (selector: string) => `
  const target = document.querySelector(${JSON.stringify(selector)});
  if (!target) return false;
  target.click();
  return true;
`;

/**
 * The game's own signal that it is the player's turn again.
 *
 * This was a fixed four-second sleep, and an AI turn measured 4.7 seconds — so the
 * next click landed on a disabled control and the navigation after it happened
 * mid-turn, which is what made the browser miss two commands. A ruler waits for a
 * condition, not a duration.
 *
 * It reads the HUD's own wording, which couples this file to a string in `hud.ts`.
 * Deliberate, and safe in the direction that matters: if the wording changes, this
 * wait times out and says so, rather than passing for the wrong reason.
 */
const PLAYERS_TURN = `
  const control = document.querySelector('[data-act="end"]');
  const hud = document.querySelector('.battle-hud')?.textContent ?? '';
  return Boolean(control && !control.disabled && hud.includes('你的回合'));
`;

/**
 * How many cells there are to click, counted from the field's own origin.
 *
 * `view.width / 32` is the *scene* — the field plus the painted margin around it —
 * so it over-counts by the margin on both sides. Counting from the field origin to
 * the far edge of the viewBox covers every field cell and a little of the right
 * and bottom margin, which is harmless: a click outside the field is a click on
 * nothing.
 */
const BOARD_SIZE = `
  const svg = document.querySelector('svg.board');
  if (!svg) return null;
  const view = svg.viewBox.baseVal;
  return {
    columns: Math.ceil((view.width + view.x) / 32),
    rows: Math.ceil((view.height + view.y) / 32),
  };
`;

/**
 * One browser, one phase, torn down after it.
 *
 * Its own profile directory and its own port, which is not fussiness: Chrome
 * refuses a second instance on a profile already in use and quietly forwards to
 * the first, so a phase that reused the directory attached to the *previous*
 * browser — including the wedged page it had been asked to leave behind.
 */
let browsers = 0;
async function inBrowser(run: (session: Session, tools: Tools) => Promise<void>): Promise<void> {
  const port = DEBUG_PORT + browsers++;
  const chrome = execFile(CHROME, [
    '--headless=new',
    '--disable-gpu',
    `--remote-debugging-port=${port}`,
    // Keyed by this run as well as this phase: a browser left alive by an earlier
    // run holds its directory, and Chrome would forward to that one instead.
    `--user-data-dir=${join(OUT, `.play-${process.pid}-${browsers}`)}`,
    `--window-size=${VIEWPORT.width},${VIEWPORT.height}`,
    '--no-first-run',
    '--no-default-browser-check',
    'about:blank',
  ]);
  let session: Session | null = null;
  try {
    let targets: PageTarget[] = [];
    for (let attempt = 0; attempt < 40 && targets.length === 0; attempt++) {
      await sleep(250);
      try {
        const listed = await fetch(`http://127.0.0.1:${port}/json/list`);
        targets = (await listed.json() as PageTarget[]).filter((target) => target.type === 'page');
      } catch {
        targets = [];
      }
    }
    const page = targets[0];
    if (!page) throw new Error('the browser never opened a page');
    session = await Session.open(page.webSocketDebuggerUrl);
    await session.send('Runtime.enable');
    await session.send('Log.enable');
    await session.send('Page.enable');
    await session.send('Inspector.enable');
    await run(session, tools(session));
  } finally {
    session?.close();
    chrome.kill();
    await sleep(400);
  }
}

interface Tools {
  shot(label: string): Promise<void>;
  go(path: string): Promise<void>;
  click(what: string, selector: string): Promise<void>;
  waitFor(what: string, expression: string, deadline?: number): Promise<void>;
  cells(): Promise<void>;
}

const shots: string[] = [];

function tools(session: Session): Tools {
  const shot = async (label: string): Promise<void> => {
    const { data } = await session.send('Page.captureScreenshot', { format: 'png' }, `photographing ${label}`);
    if (!data) return;
    const file = join(OUT, `play-${label}.png`);
    writeFileSync(file, Buffer.from(data, 'base64'));
    shots.push(file);
  };
  const go = async (path: string): Promise<void> => {
    await session.send('Page.navigate', { url: `http://127.0.0.1:${PORT}${path}` });
    await sleep(1200);
  };
  const click = async (what: string, selector: string): Promise<void> => {
    const found = await session.eval<boolean | undefined>(CLICK(selector), `clicking ${what}`);
    // `undefined` is a command that never answered, which the timeout already
    // reported; `false` is a control that is genuinely not on the screen.
    if (found === false) trouble.push(`missing control: ${what} (${selector})`);
    await sleep(500);
  };
  const waitFor = async (what: string, expression: string, deadline = 30_000): Promise<void> => {
    const until = Date.now() + deadline;
    while (Date.now() < until) {
      if (await session.eval<boolean>(expression, `waiting for ${what}`)) return;
      await sleep(250);
    }
    trouble.push(`waited ${deadline}ms for ${what} and it never happened`);
  };
  /** Every cell of the mounted board, which is how a board gets clicked wrong. */
  const cells = async (): Promise<void> => {
    const size = await session.eval<{ columns: number; rows: number } | null>(BOARD_SIZE);
    if (!size) {
      trouble.push('no board mounted where one was expected');
      return;
    }
    console.log(`  board ${size.columns}x${size.rows}: clicking every cell`);
    for (let y = 0; y < size.rows; y++) {
      for (let x = 0; x < size.columns; x++) await session.eval(CLICK_CELL(x, y));
    }
    await sleep(600);
  };
  return { shot, go, click, waitFor, cells };
}

/**
 * Whether the interface is standing on the battlefield.
 *
 * The board fills its box with the *field* now, so the field reaches further under
 * the panels than it used to. A cell the HUD covers is a cell you cannot give an
 * order to — the one failure mode that a screenshot shows as "looks great" and a
 * player discovers by clicking and nothing happening.
 *
 * Asked of the four corners and the middle of each edge, which is where a panel
 * would reach first, and asked the way the browser answers it: what is actually on
 * top at that point.
 */
const COVERED_CELLS = `
  const svg = document.querySelector('svg.board');
  if (!svg) return null;
  const box = svg.getBoundingClientRect();
  const view = svg.viewBox.baseVal;
  const scale = Math.min(box.width / view.width, box.height / view.height);
  const left = box.left + (box.width - view.width * scale) / 2 - view.x * scale;
  const top = box.top + (box.height - view.height * scale) / 2 - view.y * scale;
  // Counted over the whole scene on purpose: the probes that land in the painted
  // margin fall outside the window and are ignored below, and the ones that matter
  // — the field's own corners and edge middles — are the innermost of them.
  const columns = Math.round((view.width + view.x) / 32);
  const rows = Math.round((view.height + view.y) / 32);
  const probes = [];
  for (const [x, y] of [
    [0, 0], [columns - 1, 0], [0, rows - 1], [columns - 1, rows - 1],
    [Math.floor(columns / 2), 0], [Math.floor(columns / 2), rows - 1],
    [0, Math.floor(rows / 2)], [columns - 1, Math.floor(rows / 2)],
  ]) {
    const at = { x: left + (x + 0.5) * 32 * scale, y: top + (y + 0.5) * 32 * scale };
    const on = document.elementFromPoint(at.x, at.y);
    // A null hit is a point outside the window, which on a painted board is the
    // frame being cropped on purpose — not something to report. What matters is a
    // point that hits another element: the interface standing on the field.
    // (No back-quotes in here: this whole probe is one template literal.)
    if (on && !svg.contains(on)) {
      const named = on.closest('[class]');
      const label = named ? String(named.className.baseVal ?? named.className) : on.tagName;
      probes.push(x + ',' + y + ' under ' + label);
    }
  }
  return probes;
`;

async function main(): Promise<void> {
  mkdirSync(OUT, { recursive: true });
  const server = serve();
  try {
    /*
     * Each phase gets its own browser, on its own profile and port.
     *
     * The reason is worth writing down because it cost an evening: reusing one
     * profile directory across phases meant Chrome refused the second launch and
     * quietly forwarded to the first, so a later phase drove the browser an earlier
     * one had left behind — and every command it sent timed out. It read exactly
     * like the game hanging after a battle. It was not: no crash, 6 MB of heap, a
     * CPU profile that is 100% idle, no dialog. It was two browsers with one
     * profile between them.
     */
    console.log('the title, the codex');
    await inBrowser(async (_session, { go, click, shot }) => {
      await go('/game/');
      await shot('title');
      await click('codex', '[data-act="codex"]');
      await shot('codex');
    });

    console.log('a skirmish, played');
    await inBrowser(async (session, { go, click, shot, waitFor, cells }) => {
      await go('/game/');
      await click('skirmish', '[data-act="skirmish"]');
      await shot('levels');
      // The app's own intent for "play this one", not a guess at a class name.
      await click('first level', '[data-act="play"]');
      await sleep(1200);
      await shot('battle');
      await cells();
      await shot('after-every-cell');
      const covered = await session.eval<string[] | null>(COVERED_CELLS, 'checking the field edges');
      for (const cell of covered ?? []) trouble.push(`the interface covers cell ${cell}`);
      for (const round of [1, 2]) {
        await click(`end turn ${round}`, '[data-act="end"]');
        await waitFor(`round ${round} to come back to the player`, PLAYERS_TURN);
        await shot(`turn-${round}`);
      }
      // Leaving a battle goes to the title screen, which is what `renderMenu` is.
      await click('leave the battle', '[data-act="exit"]');
      await waitFor('the title screen', `return Boolean(document.querySelector('[data-act="campaignNew"]'));`);
      await shot('back-at-the-title');
    });

    console.log('a campaign, entered');
    await inBrowser(async (session, { go, click, shot, cells }) => {
      await go('/game/');
      await click('new campaign', '[data-act="campaignNew"]');
      await sleep(1500);
      await shot('campaign');
      /*
       * The campaign screen declares its own intents under `data-campaign-act`, and
       * which one is on screen depends on where the story is: a beat advances, a
       * fork asks, a staging screen starts the battle. Take whichever is there,
       * four times, which is enough to cross a beat, a choice and into a battle.
       */
      for (const step of [1, 2, 3, 4]) {
        await click(
          `story step ${step}`,
          '[data-campaign-act="choose"], [data-campaign-act="nextBeat"],'
          + ' [data-campaign-act="battle"], [data-campaign-act="aftermath"]',
        );
        await sleep(1500);
        await shot(`campaign-${step}`);
      }
      if (await session.eval<{ columns: number; rows: number } | null>(BOARD_SIZE)) {
        await cells();
        await shot('campaign-battle');
      }
    });
  } finally {
    server.close();
  }

  for (const file of shots) console.log(file);
  if (trouble.length === 0) {
    console.log('\nplayed clean: no exception, no console error, no failed control');
    return;
  }
  console.log(`\n${trouble.length} complaint(s), in the order they happened:`);
  for (const line of trouble) console.log(`  ${line}`);
  process.exitCode = 1;
}

await main();
