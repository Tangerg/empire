import { describe, expect, it } from 'vitest';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative, sep } from 'node:path';

const coreRoot = join(import.meta.dirname, '..');
const packagesRoot = join(coreRoot, '..', '..');
const campaignRoot = join(packagesRoot, 'campaign-engine', 'src');

/**
 * Production sources only.
 *
 * Tests and benchmarks are excluded by name as well as by folder: some packages
 * keep `*.test.ts` next to the code it covers, and counting those as runtime
 * made every boundary check quietly weaker than it looked.
 */
function runtimeTypeScriptFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const path = join(directory, entry);
    if (entry === '__tests__' || entry === '__bench__') return [];
    if (statSync(path).isDirectory()) return runtimeTypeScriptFiles(path);
    if (!entry.endsWith('.ts')) return [];
    if (entry.endsWith('.test.ts') || entry.endsWith('.bench.ts')) return [];
    return [path];
  });
}

/** Sources including the tests and benchmarks, for guards about the manifest. */
function allTypeScriptFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const path = join(directory, entry);
    if (statSync(path).isDirectory()) return allTypeScriptFiles(path);
    return entry.endsWith('.ts') ? [path] : [];
  });
}

/** Every shipped story package, which the older guards never looked at. */
function storyPackageSources(): string[] {
  return readdirSync(packagesRoot)
    .filter((entry) => entry.startsWith('story-'))
    .flatMap((entry) => runtimeTypeScriptFiles(join(packagesRoot, entry, 'src')));
}

/** Every application entry point, which is runtime code too. */
function appSources(): string[] {
  const appsRoot = join(packagesRoot, '..', 'apps');
  return readdirSync(appsRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .flatMap((entry) => {
      const sources = join(appsRoot, entry.name, 'src');
      return statSync(sources, { throwIfNoEntry: false })?.isDirectory() ? runtimeTypeScriptFiles(sources) : [];
    });
}

/**
 * The rulers, which are runtime code that nothing imports.
 *
 * `tools/` is where the instruments that prove a refactor live. No guard read it,
 * and three of them had grown a shared copy of the same board harness.
 */
function toolSources(): string[] {
  return runtimeTypeScriptFiles(join(packagesRoot, '..', 'tools'));
}

/** Every workspace package, for the guards that are about the whole repository. */
function everyPackageSource(): string[] {
  return readdirSync(packagesRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .flatMap((entry) => {
      const sources = join(packagesRoot, entry.name, 'src');
      return statSync(sources, { throwIfNoEntry: false })?.isDirectory() ? runtimeTypeScriptFiles(sources) : [];
    });
}

/**
 * Source with its comments removed.
 *
 * A guard whose pattern also matches the paragraph explaining what it forbids
 * reports the explanation as the violation, and is impossible to satisfy.
 */
const escapeForRegExp = (value: string) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

function stripComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/[^\n]*/g, '');
}

/**
 * Source with its string and template literals blanked out — but not their holes.
 *
 * Markup and prose are data here, and a guard about *code* that reads what the
 * code prints is guarding the wrong text. But this used to blank a template
 * literal whole, `${…}` included, and the UI packages are mostly template
 * literals: `${hpBar(unit.hp / definition.maxHp, 72)}` is code, and every guard
 * built on this was blind to it. So the literal text goes and the holes stay.
 *
 * A scanner rather than a regular expression, because a hole may contain an
 * object literal, and a template literal, which may contain another hole.
 */
function stripStrings(source: string): string {
  let out = '';
  /** Enclosing template literals, each with the brace depth of the hole we are in. */
  const holes: number[] = [];
  let index = 0;
  while (index < source.length) {
    const character = source[index];
    if (character === '\\' && holes.length === 0) {
      out += character;
      index += 1;
      continue;
    }
    if (character === "'" || character === '"') {
      const quote = /^(['"])(?:[^\\\n]|\\.)*?\1/.exec(source.slice(index));
      out += character.repeat(2);
      index += quote ? quote[0].length : 1;
      continue;
    }
    if (character === '`') {
      // Opening a literal, or closing the one whose text we are skipping.
      out += '`';
      index += 1;
      if (holes.length > 0 && holes[holes.length - 1] === -1) holes.pop();
      else holes.push(-1);
      continue;
    }
    // -1 means "in a literal's text": skip it, and watch for a hole opening.
    if (holes.length > 0 && holes[holes.length - 1] === -1) {
      if (character === '$' && source[index + 1] === '{') {
        holes[holes.length - 1] = 0;
        out += '${';
        index += 2;
        continue;
      }
      if (character === '\\') index += 2;
      else index += 1;
      continue;
    }
    if (holes.length > 0) {
      const depth = holes[holes.length - 1];
      if (character === '{') holes[holes.length - 1] = depth + 1;
      else if (character === '}' && depth === 0) holes[holes.length - 1] = -1;
      else if (character === '}') holes[holes.length - 1] = depth - 1;
    }
    out += character;
    index += 1;
  }
  return out;
}

/**
 * Every `export interface` in a source, with its body brace-matched.
 *
 * Brace-matched rather than lazy-to-`\n}`: a member whose type is itself an
 * object literal ends the body early, and the guard then reads the next
 * interface's fields as this one's.
 */
function exportedInterfaces(source: string): { name: string; body: string; whole: string }[] {
  const out: { name: string; body: string; whole: string }[] = [];
  const head = /export interface (\w+)[^{]*\{/g;
  let match: RegExpExecArray | null;
  while ((match = head.exec(source))) {
    let depth = 1;
    let index = match.index + match[0].length;
    while (index < source.length && depth > 0) {
      if (source[index] === '{') depth++;
      if (source[index] === '}') depth--;
      index++;
    }
    out.push({
      name: match[1],
      body: source.slice(match.index + match[0].length, index - 1),
      whole: source.slice(match.index, index),
    });
  }
  return out;
}

function localCoreImports(file: string, files: ReadonlySet<string>): string[] {
  const source = readFileSync(file, 'utf8');
  return [...source.matchAll(/from\s+['"](\.\.?\/[^'"]+)['"]|import\s+['"](\.\.?\/[^'"]+)['"]/g)]
    .map((match) => match[1] ?? match[2])
    .flatMap((specifier) => {
      const base = join(dirname(file), specifier);
      const candidates = [`${base}.ts`, join(base, 'index.ts')];
      return candidates.filter((candidate) => files.has(candidate)).slice(0, 1);
    });
}

describe('source dependency boundaries', () => {
  it('keeps production core modules acyclic', () => {
    const files = new Set(runtimeTypeScriptFiles(coreRoot));
    const graph = new Map([...files].map((file) => [file, localCoreImports(file, files)]));
    const visiting = new Set<string>();
    const visited = new Set<string>();
    const path: string[] = [];
    const cycles: string[] = [];

    const visit = (file: string): void => {
      if (visiting.has(file)) {
        const start = path.indexOf(file);
        cycles.push([...path.slice(start), file].map((entry) => relative(coreRoot, entry)).join(' -> '));
        return;
      }
      if (visited.has(file)) return;
      visiting.add(file);
      path.push(file);
      for (const dependency of graph.get(file) ?? []) visit(dependency);
      path.pop();
      visiting.delete(file);
      visited.add(file);
    };

    for (const file of files) visit(file);
    expect(cycles).toEqual([]);
  });

  it('keeps the battle core independent from content and presentation packages', () => {
    const violations = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const source = readFileSync(file, 'utf8');
      const forbidden = [...source.matchAll(/from\s+['"]([^'"]+)['"]|import\s+['"]([^'"]+)['"]/g)]
        .map((match) => match[1] ?? match[2])
        .filter((specifier) => /(?:^|\/)\.{1,2}\/(?:campaign|content|application|art|ui|editor|game)(?:\/|$)/.test(specifier));
      return forbidden.map((specifier) => `${relative(coreRoot, file)} -> ${specifier}`);
    });

    expect(violations).toEqual([]);
  });

  it('keeps default content definitions out of core data registries', () => {
    const registrations = runtimeTypeScriptFiles(join(coreRoot, 'data')).flatMap((file) => {
      const source = readFileSync(file, 'utf8');
      return source.includes('.defineAll(') ? [relative(coreRoot, file)] : [];
    });

    expect(registrations).toEqual([]);
  });

  it('keeps the game controller behind the engine spatial port', () => {
    const controller = readFileSync(join(packagesRoot, 'game-ui', 'src', 'ui', 'game.ts'), 'utf8');
    const bypasses = [...controller.matchAll(/from\s+['"]@empire\/battle-engine\/(movement|vision)['"]/g)]
      .map((match) => match[1]);

    expect(bypasses).toEqual([]);
  });

  it('keeps engine and content packages independent from presentation packages', () => {
    const policies = [
      { package: 'battle-engine', forbidden: /@empire\/(?:game-ui|editor|story-candidate|experience-lab)/ },
      { package: 'campaign-engine', forbidden: /@empire\/(?:game-ui|editor|story-candidate|experience-lab)/ },
      { package: 'content-common', forbidden: /@empire\/(?:game-ui|editor|story-candidate|experience-lab)/ },
      { package: 'content-ancient-empires', forbidden: /@empire\/(?:game-ui|editor|story-candidate|experience-lab)/ },
    ];
    const violations = policies.flatMap(({ package: packageName, forbidden }) =>
      runtimeTypeScriptFiles(join(packagesRoot, packageName, 'src')).flatMap((file) => {
        const source = readFileSync(file, 'utf8');
        return [...source.matchAll(/from\s+['"]([^'"]+)['"]|import\s+['"]([^'"]+)['"]/g)]
          .map((match) => match[1] ?? match[2])
          .filter((specifier) => forbidden.test(specifier))
          .map((specifier) => `${packageName}/${relative(join(packagesRoot, packageName, 'src'), file)} -> ${specifier}`);
      }),
    );

    expect(violations).toEqual([]);
  });

  it('keeps the editor document aggregate independent from DOM and rendering modules', () => {
    const document = readFileSync(join(packagesRoot, 'editor', 'src', 'document.ts'), 'utf8');
    expect(document).not.toMatch(/\b(?:document|window)\.|\b(?:HTMLElement|SVGElement)\b/);
    expect(document).not.toMatch(/from\s+['"]\.\.\/(?:art|ui|application)/);
  });
});

describe('dependency injection invariants', () => {
  it('declares no port field its own module never reads', () => {
    // A port says what one module needs. Copying a collaborator's field into it
    // makes it that collaborator's port under a second name: `AbilityRules`
    // listed seven services abilities.ts never touches, because `execute` hands
    // the ruleset to combat planning, and five ports wrote out `GridRules`'s
    // single field rather than extending it. Both fail the same way — the day
    // the real port grows a service, the module that merely passes the ruleset
    // along is the one that stops compiling — and both hid a second copy of the
    // question: `activeTurnOrder` and `areaShapes.coverage(boardOf(…))` were
    // each written out again beside the port that had copied their field.
    //
    // A field that is passed on rather than read has one legitimate form, and it
    // is stated by reason rather than by file: a port published to registered
    // handlers carries what a handler may read.
    const published = new Map([['battle-engine/src/unit-departure.ts', ['content']]]);
    const offenders = everyPackageSource().flatMap((file) => {
      const path = relative(packagesRoot, file);
      const source = stripComments(readFileSync(file, 'utf8'));
      return exportedInterfaces(source)
        .filter(({ name }) => /(?:Rules|Dependencies|Ports)$/.test(name))
        .flatMap(({ name, body, whole }) => {
          const rest = source.replace(whole, '');
          return [...body.matchAll(/^ {2}(?:readonly )?(\w+)\s*[?:]/gm)]
            .map((member) => member[1])
            .filter((field) => !published.get(path)?.includes(field))
            // Read as `rules.field`, or destructured out of one.
            .filter((field) => !new RegExp(String.raw`[.{,]\s*${field}\b`).test(rest))
            .map((field) => `${path} ${name}.${field}`);
        });
    });

    expect(offenders).toEqual([]);
  });

  it('never defaults a dependency parameter to a global singleton', () => {
    const globals = [
      'TEST_CONTENT',
      'DefaultBattleResources',
      'CoreTacticalSpace',
      'StatusBehaviors',
      'ObjectiveHandlers',
      'DefaultRankProgression',
      'WeaponHitEffectHandlers',
      'ScenarioConditionHandlers',
      'ScenarioEffectHandlers',
      'DefaultAiObjectiveAdvisors',
      'DefaultAbilityAiEvaluators',
      'DefaultBattleEventPresenters',
    ];
    const pattern = new RegExp(`[:,)]\\s*\\w+\\s*=\\s*(?:${globals.join('|')})\\b|=\\s*(?:${globals.join('|')})(?=\\s*[,)])`);
    const scanned = [
      ...runtimeTypeScriptFiles(coreRoot),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'campaign-engine', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'game-ui', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'editor', 'src')),
    ];
    const offenders = scanned.filter((file) => {
      const source = readFileSync(file, 'utf8');
      // Only parameter lists matter; a module-level const binding is fine.
      return source
        .split('\n')
        .some((line) => pattern.test(line) && !/^\s*(?:export\s+)?const\s/.test(line));
    });

    expect(offenders.map((file) => relative(packagesRoot, file))).toEqual([]);
  });

  it('keeps presentation packages off the global content registries', () => {
    const packages = ['game-ui', 'editor'];
    const violations = packages.flatMap((packageName) =>
      runtimeTypeScriptFiles(join(packagesRoot, packageName, 'src')).flatMap((file) => {
        const source = readFileSync(file, 'utf8');
        return [...source.matchAll(/from\s+['"]([^'"]+)['"]/g)]
          .map((match) => match[1])
          .filter((specifier) => /@empire\/battle-engine\/data\//.test(specifier))
          .map((specifier) => `${packageName}/${relative(join(packagesRoot, packageName, 'src'), file)} -> ${specifier}`);
      }),
    );

    // Presentation renders whatever ruleset it was handed, never an ambient one.
    expect(violations).toEqual([]);
  });

  it('keeps ambient content installation out of libraries', () => {
    const packages = ['game-ui', 'editor', 'campaign-engine', 'story-candidate-01'];
    const violations = packages.flatMap((packageName) =>
      runtimeTypeScriptFiles(join(packagesRoot, packageName, 'src')).flatMap((file) => {
        const source = readFileSync(file, 'utf8');
        return /\binstallContentPacks\s*\(/.test(source)
          ? [`${packageName}/${relative(join(packagesRoot, packageName, 'src'), file)}`]
          : [];
      }),
    );

    // Only application composition roots may install content.
    expect(violations).toEqual([]);
  });
});

describe('no ambient content', () => {
  it('keeps reusable packages free of top-level mutable state', () => {
    // A module counter looks harmless until the same input renders differently
    // after an unrelated call. Runtime ownership belongs to an instance or an
    // ordinary parameter; immutable rule prototypes may remain top-level consts.
    const offenders = everyPackageSource().flatMap((file) => {
      const source = stripStrings(stripComments(readFileSync(file, 'utf8')));
      return /^(?:(?:export\s+)?(?:let|var)\s+|export\s+const\s+\w+\s*=\s*new\s+(?:Map|Set)\b)/m.test(source)
        ? [relative(packagesRoot, file)]
        : [];
    });

    expect(offenders).toEqual([]);
  });

  it('exposes no module-level registry of content definitions', () => {
    // Code registries (abilities, turn-order policies) are engine behaviour
    // populated at module load and cloned per ruleset — they are prototypes, not
    // shared data. What must never exist at module scope is a registry of
    // *content* definitions, because those are written by external packs at boot
    // and would put every engine instance back in one namespace.
    const contentDefinitions = [
      'TerrainDef', 'UnitDef', 'WeaponDef', 'StatusDef', 'StructureDef',
      'TerrainOverlayDef', 'TacticDef', 'CareerDef', 'FormationDef',
      'MovementProfileDef', 'DamageTypeDef', 'ArmorClassDef',
    ];
    const pattern = new RegExp(`^export const \\w+ = new Registry<(?:${contentDefinitions.join('|')})>`, 'm');
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) =>
      pattern.test(readFileSync(file, 'utf8')) ? [relative(coreRoot, file)] : []);

    expect(offenders).toEqual([]);
  });

  it('composes the art a shell draws with, never registering it globally', () => {
    // The presentation layer kept the disease the engine was cured of: two
    // module-level mutable arrays that story packages pushed themselves into, so
    // which theme won was a function of import order behind an idempotence flag.
    // It worked only because exactly one pack exists. `ArtDirection` is composed
    // by the application root, like the catalog and the ruleset beside it.
    const forbidden = [
      'registerArtProvider',
      'registerBattlePresentation',
      'resolveArt',
      'resetArtProviders',
      'registerCandidate01Presentation',
    ];
    const pattern = new RegExp(`\\b(?:${forbidden.join('|')})\\b`);
    // Story packages are scanned too: the swallowing `catch` and the install
    // flag both lived in one, outside every guard's reach.
    const scanned = [
      ...runtimeTypeScriptFiles(join(packagesRoot, 'game-ui', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'editor', 'src')),
      ...storyPackageSources(),
      ...runtimeTypeScriptFiles(join(packagesRoot, '..', 'apps')),
    ];
    const offenders = scanned.flatMap((file) => {
      const source = readFileSync(file, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
      return pattern.test(source) ? [relative(packagesRoot, file)] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('has removed every ambient content entry point', () => {
    const forbidden = [
      'GlobalContentCatalog',
      'GlobalContentPacks',
      'installContentPacks',
      'createDefaultBattleRuleServices',
      'CoreTacticalSpace',
    ];
    const pattern = new RegExp(`\\b(?:${forbidden.join('|')})\\b`);
    const scanned = [
      ...runtimeTypeScriptFiles(coreRoot),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'campaign-engine', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'game-ui', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'editor', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'story-candidate-01', 'src')),
    ];
    const offenders = scanned.filter((file) => pattern.test(readFileSync(file, 'utf8')));

    expect(offenders.map((file) => relative(packagesRoot, file))).toEqual([]);
  });

  it('keeps the test composition root out of runtime code', () => {
    const scanned = [
      ...runtimeTypeScriptFiles(coreRoot),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'campaign-engine', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'game-ui', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'editor', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'story-candidate-01', 'src')),
    ];
    const offenders = scanned.filter((file) =>
      /@empire\/test-content/.test(readFileSync(file, 'utf8')));

    expect(offenders.map((file) => relative(packagesRoot, file))).toEqual([]);
  });

  it('leaves content installation to composition roots only', () => {
    const installers = ['game-ui', 'editor', 'campaign-engine', 'content-common', 'content-ancient-empires']
      .flatMap((packageName) =>
        runtimeTypeScriptFiles(join(packagesRoot, packageName, 'src')).flatMap((file) =>
          /new ContentPackInstaller\(/.test(readFileSync(file, 'utf8'))
            ? [`${packageName}/${relative(join(packagesRoot, packageName, 'src'), file)}`]
            : []));

    // Apps and @empire/test-content compose; libraries never do.
    expect(installers).toEqual([]);
  });
});

describe('the guards are held to themselves', () => {
  /**
   * An exemption has to still exempt something.
   *
   * A dozen guards here name the files allowed to do the thing they forbid — the
   * module that owns the rule, the one place a blow is settled, the tiling that
   * declares its own facings. Those lists rot in one direction only: a file gets
   * cleaned up, stops doing the thing, and its allowance stays. Nothing fails,
   * because an allowance costs nothing to hold — it just quietly permits the
   * defect to come back to a file that no longer has it.
   *
   * Five were stale when this was written. `battle-aggregate.ts` no longer settles
   * a blow; `board-decorations.ts` no longer writes out facings; `scenario.ts`,
   * `objective-system.ts` and `types.ts` no longer name a payload kind — the
   * ladders they were exempt for are exactly what `references` replaced. All five
   * were removed and every guard stayed green, which is the proof they were dead.
   *
   * This checks the guards that state their pattern inline as a literal. The rest
   * build one from parts and cannot be read out of the source, so they are named
   * below and counted: a new exemption list lands in one bucket or the other, and
   * neither can grow without this failing.
   */
  it('keeps every exemption in this file load-bearing', () => {
    const path = join(coreRoot, '__tests__', 'architecture-boundaries.test.ts');
    const blocks = readFileSync(path, 'utf8').split(/\n  it\(/).slice(1);

    /** Guards whose pattern is assembled at run time rather than written out. */
    const assembled = [
      'never cross-checks a registry against content by hand',
      'builds every extension point on the shared registry',
      "writes the map's spatial layers in exactly one place",
      'asks a trigger whether it is due, never its repeat block',
      'measures the board through its tiling, never with a fixed metric',
      'asks one question about the right to react',
    ];

    const offenders: string[] = [];
    let checked = 0;
    for (const block of blocks) {
      // A title may contain an escaped quote, and one of them does.
      const title = (/^'((?:[^'\\]|\\.)*)'/.exec(block)?.[1] ?? '?').replace(/\\'/g, "'");
      const list = /const (?:allowed|owners)\s*(?::[^=]+)?=\s*\[([\s\S]*?)\n?\s*\];/.exec(block);
      if (!list) continue;
      const inline = /return (\/(?:[^/\\\n]|\\.)+\/)\.test\(/.exec(block);
      if (!inline) {
        if (!assembled.includes(title)) offenders.push(`${title}: exempts files but states no pattern to check it against`);
        continue;
      }
      // `join('domain', 'unit-entity.ts')` is one entry, not two literals.
      const body = list[1]
        .replace(/\/\/[^\n]*/g, ' ')
        .replace(/join\(([^)]*)\)/g, (_, parts: string) =>
          `'${[...parts.matchAll(/'([^']*)'/g)].map(([, part]) => part).join(sep)}'`);
      const entries = [...body.matchAll(/'([^']+)'/g)].map(([, name]) => name);
      const pattern = new RegExp(inline[1].slice(1, -1));
      for (const entry of entries) {
        const file = [join(coreRoot, entry), join(packagesRoot, entry)]
          .find((candidate) => statSync(candidate, { throwIfNoEntry: false })?.isFile());
        if (!file) {
          offenders.push(`${title}: exempts "${entry}", which is not a file`);
          continue;
        }
        if (!pattern.test(stripComments(readFileSync(file, 'utf8')))) {
          offenders.push(`${title}: exempts "${entry}", which no longer does the thing`);
        }
      }
      checked++;
    }

    // Half the exemption lists are the parseable kind; a run that found none of
    // them would pass by having read nothing.
    expect(checked).toBeGreaterThanOrEqual(6);
    expect(offenders).toEqual([]);
  });

  it('reads the code inside a template hole and the prose nowhere', () => {
    // `stripStrings` is load-bearing for a dozen guards, and it is the kind of
    // helper that fails silently: blank too much and every guard passes by
    // reading nothing. It used to blank a template literal whole — holes and all
    // — which is how a `!` assertion sat inside `${…}` in the HUD while the guard
    // that forbids assertions was green.
    const source = 'const row = `<b class="hp">${bar(unit.hp / def.maxHp)} ${f({ k: `${g()}` })}</b>`;';
    const code = stripStrings(source);

    // Every expression in a hole survives, including one nested a literal deep.
    expect(code).toContain('bar(unit.hp / def.maxHp)');
    expect(code).toContain('g()');
    // And no part of the prose does, at any depth.
    for (const prose of ['class=', '<b', '</b>', 'hp"']) expect(code).not.toContain(prose);
    // A quoted string keeps its quotes and loses its content, escapes included.
    expect(stripStrings("const s = 'it\\'s';")).toBe("const s = '';");
    expect(stripStrings('const s = "x";')).toBe('const s = "";');
    // Code outside any literal is untouched.
    expect(stripStrings('a.b!.c')).toBe('a.b!.c');
  });
});

describe('documentation is held to the code', () => {
  it('counts these guards for every document that states a number', () => {
    // Three documents claimed a guard count. Two of them said 41 and one said
    // 48, while there were 48 — so the docs disagreed with the code *and* with
    // each other, which is what a fact restated in prose does. A stated number
    // is only worth stating if something checks it.
    const guards = readFileSync(join(coreRoot, '__tests__', 'architecture-boundaries.test.ts'), 'utf8')
      .split('\n')
      .filter((line) => line.startsWith('  it(')).length;

    const docsRoot = join(packagesRoot, '..', 'docs');
    const claims = readdirSync(docsRoot)
      .filter((entry) => entry.endsWith('.md'))
      .flatMap((entry) => readFileSync(join(docsRoot, entry), 'utf8').split('\n').flatMap((line) => {
        const stated = /架构(?:依赖)?测试[^\n]*?共 (\d+) 条|当前共 (\d+) 条/.exec(line);
        return stated ? [{ doc: entry, count: Number(stated[1] ?? stated[2]) }] : [];
      }));

    // A guard that finds nothing to check passes for the wrong reason.
    expect(claims.length).toBeGreaterThanOrEqual(3);
    expect(claims.filter((claim) => claim.count !== guards)).toEqual([]);
  });

  /**
   * The battle overlay's regions, as the code declares them and as the doc lists.
   *
   * The doc's table is the only statement of what each region is *for*, which is
   * what stops the overlay from silently becoming a place to put panels. A region
   * added in code and not in the table has no declared question; one in the table
   * and not in code is a promise about a place that does not exist.
   */
  it('lists exactly the battle overlay regions the code declares', () => {
    const hud = readFileSync(join(packagesRoot, 'game-ui', 'src', 'ui', 'hud.ts'), 'utf8');
    const table = /const HUD_REGIONS = \{([\s\S]*?)\n\} as const;/.exec(hud);
    const declared = [...(table?.[1] ?? '').matchAll(/^\s{2}(\w+):/gm)].map(([, name]) => name);

    const doc = readFileSync(join(packagesRoot, '..', 'docs', 'presentation-system.md'), 'utf8');
    // Anchored on the table's own heading row, not on "a row whose first cell is
    // in backticks" — the document is mostly tables, and the loose pattern read
    // every art port and atlas name in the file as a region.
    const rows = /\| 区域 \| 它回答的问题 \|[^\n]*\n\|[-| ]+\|\n([\s\S]*?)\n\n/.exec(doc);
    const documented = [...(rows?.[1] ?? '').matchAll(/^\| `(\w+)` \|/gm)].map(([, name]) => name);

    // The count the prose states, read rather than repeated here: pinning it in
    // the test would fail a legitimate ninth region with a message about a
    // number instead of about the table it is missing from.
    const stated = /覆盖层是 (\d+) 个区域/.exec(doc);
    expect(Number(stated?.[1])).toBe(declared.length);
    expect(documented.sort()).toEqual([...declared].sort());
  });
});

describe('a package publishes one way in', () => {
  const manifests = (): [string, { exports?: unknown; sideEffects?: unknown }][] =>
    readdirSync(packagesRoot, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .flatMap((entry) => {
        const path = join(packagesRoot, entry.name, 'package.json');
        return statSync(path, { throwIfNoEntry: false })
          ? [[entry.name, JSON.parse(readFileSync(path, 'utf8'))] as [string, { exports?: unknown }]]
          : [];
      });

  it('exports no wildcard subpath of its own source', () => {
    // Every package published `"./*": "./src/*.ts"`, which is not an API but the
    // absence of one: every top-level module was public under a second name it
    // already had through the barrel, and `__tests__/fixtures` was public too —
    // one suite in another package was importing it.
    //
    // Assets are different. A stylesheet is not re-exported by any barrel, so
    // `./styles/*` is the only way to reach one and duplicates nothing.
    const offenders = manifests().flatMap(([name, manifest]) => {
      const map = (manifest.exports ?? {}) as Record<string, string>;
      return Object.entries(map)
        .filter(([subpath, target]) => subpath.includes('*') && target.endsWith('.ts'))
        .map(([subpath]) => `${name}: ${subpath}`);
    });

    expect(offenders).toEqual([]);
  });

  it('gives a second entry point only to what the first does not reach', () => {
    // A subpath earns its place by exporting something the root does not — the
    // browser-only presentation layer of a story pack, say. A subpath whose
    // module the index already re-exports is a second name for one thing.
    const offenders = manifests().flatMap(([name, manifest]) => {
      const map = (manifest.exports ?? {}) as Record<string, string>;
      const index = join(packagesRoot, name, 'src', 'index.ts');
      if (!statSync(index, { throwIfNoEntry: false })) return [];
      const barrel = readFileSync(index, 'utf8');
      return Object.entries(map).flatMap(([subpath, target]) => {
        if (subpath === '.' || !target.endsWith('.ts')) return [];
        // `./src/presentation/index.ts` is re-exported as `'./presentation'`.
        const module = target.replace(/^\.\/src\//, './').replace(/(?:\/index)?\.ts$/, '');
        const reExported = new RegExp(`export \\* from '${escapeForRegExp(module)}(?:/index)?';`);
        return reExported.test(barrel) ? [`${name}: ${subpath} is already in the barrel`] : [];
      });
    });

    expect(offenders).toEqual([]);
  });

  it('declares itself free of side effects apart from stylesheets', () => {
    // Without this a bundler must assume importing the barrel runs code, so it
    // keeps every module the barrel names. Collapsing the deep imports onto the
    // roots cost the editor 8% of its bundle until these were declared — and
    // then returned more than it took, because the deep imports had been
    // pulling in whole modules for one symbol.
    const offenders = manifests()
      .filter(([, manifest]) => JSON.stringify(manifest.sideEffects) !== '["**/*.css"]')
      .map(([name]) => name);

    expect(offenders).toEqual([]);
  });

  it('keeps assembly machinery, aggregate writers and raw content behind package APIs', async () => {
    // A consumer composes through `createBattleEngine`; it does not run the
    // kernel, manufacture a naked state, or bypass the aggregate's writer.
    // These names were all exported by `export *`, although no production
    // package used them. That made the internal implementation look canonical.
    const api = await import('../index');
    const privateNames = [
      'BattleEngine',
      'SrpgMicrokernel',
      'createDefaultMicrokernel',
      'DEFAULT_RULE_PLUGINS',
      'createBattleRules',
      'applyAction',
      'CoreActionHandlers',
      'createState',
      'cloneState',
      'restoreState',
      'spawnUnit',
      'removeUnit',
      'requireUnit',
      'sealContentCatalog',
      'createBattleSave',
      'BattleSaveMigrator',
      'SchemaMigrator',
      'SchemaMigration',
      'migrateLevel',
      'RuntimeGridAtlas',
      'routeUnit',
      'cloneContentCatalog',
      'clearCaptureAt',
      'coordOf',
      'terrainAt',
    ];

    expect(privateNames.filter((name) => name in api)).toEqual([]);
    expect(api.createBattleEngine).toBeTypeOf('function');

    const [common, ancient, candidate, presentation, experience, testContent, gameUi] = await Promise.all([
      import('../../../content-common/src/index'),
      import('../../../content-ancient-empires/src/index'),
      import('../../../story-candidate-01/src/index'),
      import('../../../story-candidate-01/src/presentation/index'),
      import('../../../experience-lab/src/index'),
      import('../../../test-content/src/index'),
      import('../../../game-ui/src/index'),
    ]);
    expect(Object.keys(common).sort()).toEqual(['COMMON_CONTENT_PACK', 'IMPASSABLE', 'moveCosts']);
    expect(Object.keys(ancient).sort()).toEqual(['ANCIENT_EMPIRES_CONTENT_PACK', 'ANCIENT_EMPIRES_LEVELS']);
    expect(Object.keys(candidate).sort()).toEqual([
      'CANDIDATE_01_CONTENT_PACK',
      'CANDIDATE_01_FIRST_THREE_CHAPTERS_CAMPAIGN',
      'CANDIDATE_01_LEVELS',
      'candidate01Level',
    ]);
    expect(Object.keys(presentation).sort()).toEqual([
      'CANDIDATE_01_ART',
      'CANDIDATE_01_MENU_ART',
      'candidate01CampaignAdapter',
    ]);
    expect(Object.keys(experience)).toEqual(['experienceLevel']);
    expect(Object.keys(testContent).sort()).toEqual(['createTestCatalog', 'makeLevel', 'u']);
    expect('html' in gameUi).toBe(false);
  });

  it('publishes one stateless campaign save reader, not a migration registry', async () => {
    const api = await import('../../../campaign-engine/src/index');
    expect('DefaultCampaignSaveMigrator' in api).toBe(false);
    expect('CampaignSaveMigrator' in api).toBe(false);
    expect(api.loadCampaignSave).toBeTypeOf('function');
  });
});

describe('one composition root', () => {
  it('builds an engine in exactly one place, through the plugins', () => {
    // There used to be two composition roots, and only one of them ran the
    // plugins: every app and every test but one called a factory that assembled
    // the same twenty-one defaults by hand, so the plugin architecture was real
    // code the product never executed — and the defaults had to be kept in step
    // with the plugins that shadowed them.
    //
    // A third one then grew where the first guard could not see it: this used to
    // read the engine package alone, and the demo — an app — was running the
    // kernel itself, because composing by hand was the only way to add a plugin
    // or to read the manifest afterwards. Both are parameters of the root now,
    // so the whole repository is held to it, and running a kernel counts as
    // building an engine even when the constructor is out of sight.
    const root = join(coreRoot, 'plugins', 'default.ts');
    const assembly = /new BattleEngine\(|new SrpgMicrokernel\(|createDefaultMicrokernel\(/;
    const offenders = [
      ...runtimeTypeScriptFiles(coreRoot),
      ...everyPackageSource(),
      ...appSources(),
    ].flatMap((file) => {
      if (file === root) return [];
      return assembly.test(stripComments(readFileSync(file, 'utf8'))) ? [relative(packagesRoot, file)] : [];
    });

    expect(offenders).toEqual([]);

  });

  it('installs a default rule registry only from a plugin', () => {
    // A registry cloned outside the plugins is a default that the kernel does
    // not know about, which is how the second root came to exist in the first
    // place. `data/` holds the shipped content tables, which packs install.
    const prototypes = [
      'Abilities', 'Reactions', 'TurnOrders', 'StatusBehaviors', 'ObjectiveHandlers',
      'WeaponAreaShapes', 'UnitDirectives', 'WeaponHitEffectHandlers',
      'ScenarioConditionHandlers', 'ScenarioEffectHandlers', 'CoreActionHandlers',
      'DefaultBattleResources', 'CombatModifierProviders', 'DefaultAiIntents',
      'DefaultAbilityAiEvaluators', 'DefaultAiObjectiveAdvisors',
    ];
    const pattern = new RegExp(`\\b(?:${prototypes.join('|')})\\.clone\\(`);
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const name = relative(coreRoot, file);
      if (name.startsWith('plugins')) return [];
      return pattern.test(readFileSync(file, 'utf8')) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });
});

describe('a content pack does not re-declare the common layer', () => {
  it('builds movement costs with the shared builder, not a local copy', () => {
    // Two packs carried a verbatim copy of a six-argument positional `costs()`
    // helper, and a third story would have copied it again. Worse, the call
    // sites read `costs(2, 3, 3, 1)` — six numbers whose only meaning was their
    // order, in a list whose order lives in another package.
    //
    // The builder now sits beside the movement profiles it names, in
    // `content-common`: the engine's `MovementClass` is an open string and must
    // not learn that `mounted` exists, and the packs must not each decide what
    // order the classes come in.
    const owner = join(packagesRoot, 'content-common', 'src', 'movement.ts');
    const offenders = everyPackageSource().flatMap((file) => {
      if (file === owner) return [];
      const code = stripComments(readFileSync(file, 'utf8'));
      // A local builder returning `MoveCosts`, or a bare positional `costs(…)`.
      return /:\s*MoveCosts\s*=>|\bconst costs = \(/.test(code)
        ? [relative(packagesRoot, file)]
        : [];
    });

    expect(offenders).toEqual([]);
  });
});

describe('an event stream is asked one question in one place', () => {
  it('lets only the emitting module recognise a semantic event by name', () => {
    // Two questions about a battle's events had drifted into several answers.
    //
    // "Which signals did it raise" was written out three times — the campaign
    // bridge, the campaign shell, a balance probe — as the same filter over the
    // same event name, in modules that do not emit it.
    //
    // "How much fighting happened" was worse than duplicated: the shell counted
    // `attack`, `areaAttack` and `counter`, and the engine emits seven strikes.
    // A support attack, a parting shot and both structure strikes were not
    // counted, so a battle decided by reaction fire reported almost no combat.
    //
    // Both now have one owner: `scenarioSignalsOf` beside the effect that emits
    // the signal, and `isStrike` beside combat — the latter recognising a strike
    // by its payload rather than by a list of names, so the seven stay open.
    const owners: Record<string, string> = {
      scenarioSignal: join('battle-engine', 'src', 'scenario.ts'),
      attack: join('battle-engine', 'src', 'combat.ts'),
      areaAttack: join('battle-engine', 'src', 'combat.ts'),
      counter: join('battle-engine', 'src', 'combat.ts'),
      supportAttack: join('battle-engine', 'src', 'combat.ts'),
      partingShot: join('battle-engine', 'src', 'combat.ts'),
      attackStructure: join('battle-engine', 'src', 'combat.ts'),
      areaAttackStructure: join('battle-engine', 'src', 'combat.ts'),
    };
    const offenders = [...everyPackageSource(), ...appSources()].flatMap((file) => {
      const name = relative(packagesRoot, file);
      const code = stripComments(readFileSync(file, 'utf8'));
      return Object.entries(owners).flatMap(([event, owner]) =>
        // Comparison only. Emitting `{ type: 'attack', … }` is how the event
        // gets raised, and the presenter registry is keyed by the name, so both
        // are declarations of the kind rather than reasoning about it.
        new RegExp(`\\.type === '${event}'`).test(code) && !name.endsWith(owner)
          ? [`${name} recognises '${event}' itself`]
          : []);
    });

    expect(offenders).toEqual([]);
  });
});

describe('a strategy is asked for its behaviour, not its name', () => {
  it('never branches on a registered presentation id', () => {
    // Six places in the board asked `presentation.id === 'generic'` to pick
    // between ruled squares and ground-level ellipses, so a third look was
    // unreachable and no presentation could mix. Art states what it wants.
    const scanned = [
      ...runtimeTypeScriptFiles(join(packagesRoot, 'game-ui', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'story-candidate-01', 'src')),
    ];
    const offenders = scanned.filter((file) =>
      /\b(?:presentation|decorations|decor)\.id\s*[!=]==?\s*['"]/.test(readFileSync(file, 'utf8')));

    expect(offenders.map((file) => relative(packagesRoot, file))).toEqual([]);
  });

  it('never guesses what an order does from the ability id on it', () => {
    // `AbilityDef` has said which weapon an order fires since the round that
    // added `weaponFor`, and six sites across three packages still decided
    // whether it fires one at all by testing the id against `'attack'`: the
    // command menu skipped that ability and expanded it by hand beside its own
    // loop, the dispatcher silently dropped a weapon named on any other order,
    // the board tinted a helpful order's targets in the enemy's colour, and the
    // AI's withdrawal penalty stopped applying to every other way of striking.
    // Both orders, and the reader may be reached through a path: the first
    // draft anchored the right-hand side at the operator and so read
    // `ability === 'wait'` but not `'wait' === command.ability`.
    const read = String.raw`[\w.$\[\]]*\b\w*[Aa]bilit(?:y|ies)\w*\b`;
    const text = String.raw`'[^']*'|"[^"]*"`;
    const compared = new RegExp(
      String.raw`${read}\s*[!=]==?\s*(?:${text})|(?:${text})\s*[!=]==?\s*${read}`,
    );
    const offenders = [...everyPackageSource(), ...appSources()].flatMap((file) => {
      const code = stripComments(readFileSync(file, 'utf8'));
      return compared.test(code) ? [relative(packagesRoot, file)] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('validates a campaign node kind through its handler', () => {
    // The definition validator held a four-armed ladder over node kinds, which
    // is why a story pack could add a condition and an effect but not a shop.
    // What one kind must declare belongs to that kind.
    const aggregate = readFileSync(join(packagesRoot, 'campaign-engine', 'src', 'aggregate.ts'), 'utf8');

    expect(aggregate).not.toMatch(/node\.type\s*[!=]==?\s*['"]/);
  });
});

describe('referential integrity has an owner', () => {
  it('never cross-checks a registry against content by hand', () => {
    // "Does this ruleset implement the name that content wrote down" was three
    // hand-written loops in the engine's constructor plus three more in a
    // traversal beside it — and every extension point added since was checked by
    // nobody. Asking a rule registry `.has(...)` or `.keys()` outside the checks
    // is how the seventh unowned cross-check starts.
    const allowed = ['rule-references.ts', 'engine.ts'];
    const registries = [
      'abilities', 'hitEffects', 'objectives', 'scenarioConditions', 'scenarioEffects',
      'reactions', 'turnOrders', 'areaShapes', 'directives',
    ];
    const pattern = new RegExp(`\\brules\\.(?:${registries.join('|')})\\.(?:has|keys|ids)\\(`);
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const name = relative(coreRoot, file);
      if (allowed.includes(name)) return [];
      return pattern.test(readFileSync(file, 'utf8')) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });
});

describe('behaviour has an owner', () => {
  it('names an owner that exists', () => {
    // "Every rule has exactly one owner, and the owner is named in
    // `docs/engine-capabilities.md`" is only true while the names resolve. Five
    // rows named registries that had grown a `Default` prefix, so a reader
    // following the table to `AiIntents` or `ResourceSubjectResolvers` found
    // nothing at all.
    //
    // Table rows only, and that is the point rather than a convenience: a row
    // is a claim about the code as it stands, while the prose around it is
    // allowed — required — to name what was renamed or deleted and why.
    //
    // Which is exactly why the sources have their comments stripped. Those
    // paragraphs are dense with names that no longer exist, so "mentioned
    // anywhere" was satisfied by the very explanation of a rename: rename a rule
    // everywhere in code, leave the old name in the note above it, and a stale
    // table row went on looking correct.
    //
    // Comment-stripped presence is also all that is worth checking. A stricter
    // "is it *declared*" version was written and thrown away: it matched call
    // sites too, so it was no stronger than this while appearing to be — and
    // with `tsc -b` clean, any mention in code already proves the name resolves.
    const docsRoot = join(packagesRoot, '..', 'docs');
    const sources = [...everyPackageSource(), ...appSources()]
      .map((file) => stripComments(readFileSync(file, 'utf8')))
      .join('\n');
    /** Language built-ins the prose names in passing; no source declares them. */
    const BUILT_INS = new Set(['Error', 'Map', 'Set', 'Promise', 'JSON', 'Object', 'Array']);
    const named = readdirSync(docsRoot)
      .filter((entry) => entry.endsWith('.md'))
      .flatMap((entry) => readFileSync(join(docsRoot, entry), 'utf8')
        .split('\n')
        .filter((line) => line.startsWith('|'))
        .flatMap((line) => [...line.matchAll(/`([^`\n]+)`/g)]
          // An identifier a reader would grep for: a type or a called function.
          .map(([, code]) => /^([A-Z][A-Za-z0-9]{2,}|[a-z][A-Za-z0-9]{2,}\(\))$/.exec(code.trim()))
          .flatMap((match) => (match ? [match[1].replace('()', '')] : []))
          .filter((name) => !BUILT_INS.has(name))
          .map((name) => ({ doc: `docs/${entry}`, name }))));

    // A guard that finds no table passes by having nothing to check, and the
    // ownership table alone is over ninety rows.
    expect(named.length).toBeGreaterThan(90);
    const offenders = named
      .filter(({ name }) => !new RegExp(String.raw`\b${escapeForRegExp(name)}\b`).test(sources))
      .map(({ doc, name }) => `${doc}: ${name}`);

    expect([...new Set(offenders)]).toEqual([]);
  });

  it('lets only an entity write its own fields', () => {
    // `UnitEntity` says all state-changing rules should go through it "so
    // callers cannot forget clamping, resource consumption, or lifecycle
    // invariants" — and eight modules wrote the fields anyway, two of them with
    // the entity already constructed on the line above. Where a unit stands was
    // reassembled at four sites, morale had two writers, and a *query* wrote the
    // requested formation onto the live unit to ask whether it would hold.
    //
    // Two field lists, because the scope is a real distinction rather than a
    // convenience: inside the battle engine a `Unit` is a live entity, so all of
    // its fields are the entity's to write. Everywhere else the same names
    // belong to `LevelUnit` (level data being authored) or to editor document
    // state, so only the fields a runtime unit alone has are forbidden.
    const runtimeOnly = ['done', 'capture', 'statuses', 'weaponState', 'reactionUsedRound', 'commanderId'];
    const entityOwned = [
      ...runtimeOnly, 'id', 'type', 'owner', 'x', 'y', 'hp', 'rank', 'rankProgress',
      'resources', 'reaction', 'facing', 'morale', 'formation', 'directive', 'career',
      'learnedAbilities',
    ];
    // Not anchored to the start of a line, and compound assignment counts: the
    // first draft was `^\s*…\s*=`, which read `unit.done = true` on its own line
    // but not `if (spent) unit.done = true` or `unit.capture += 1`.
    const assignment = (fields: string[]) =>
      new RegExp(String.raw`[A-Za-z_$][\w.$\[\]]*\.(?:${fields.join('|')})\s*(?:[+\-*/]=|=(?!=))`);
    const domainRoot = join(coreRoot, 'domain');
    const offenders = everyPackageSource().flatMap((file) => {
      if (file.startsWith(domainRoot)) return [];
      const inEngine = file.startsWith(coreRoot);
      const source = stripStrings(stripComments(readFileSync(file, 'utf8')));
      return assignment(inEngine ? entityOwned : runtimeOnly).test(source)
        ? [relative(packagesRoot, file)]
        : [];
    });

    expect(offenders).toEqual([]);
  });

  it('lets only the lifecycle change a battle phase or advance either clock', () => {
    // `state.phase` used to be assigned from three unrelated places and the
    // round counter from a free function, so "when does a round end" had no
    // owner and a per-unit turn order had nowhere to plug in. The actor-turn
    // clock joined it: delays are measured in that unit, so a second writer
    // would make one content pack mean two things.
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const source = readFileSync(file, 'utf8');
      const writes = /\bphase\s*=\s*'(?:playing|over|deployment)'|\bturn\+\+|\bturn\s*\+=|\bactorTurns\s*(?:\+\+|\+=|=[^=])/.test(source);
      return writes && relative(coreRoot, file) !== 'turn-cycle.ts' ? [relative(coreRoot, file)] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('lets only the transport rules read who is aboard', () => {
    // Same shape as the deployment roster below: `embarkedUnits` is the
    // transport aggregate, and a second reader is how "can this unit board" and
    // "may this unit board" end up as two different answers. The save writer and
    // the reference checker read it as *data* — every passenger is still a unit
    // whose content ids must be checked and written down — which is why they are
    // named here rather than the rule being copied into them. `types.ts`
    // declares the field; declaring is not reading.
    const owners = ['transports.ts', 'state.ts', 'battle-save.ts', 'rule-references.ts', 'types.ts'];
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const source = stripStrings(stripComments(readFileSync(file, 'utf8')));
      return /\bembarkedUnits\b/.test(source) && !owners.includes(relative(coreRoot, file))
        ? [relative(coreRoot, file)]
        : [];
    });

    expect(offenders).toEqual([]);
  });

  it('lets only the deployment rules read the deployment roster', () => {
    // The pre-battle arrangement is one aggregate, and it used to have two
    // owners: the action handler knew about zones, swaps and terrain, while
    // anything wanting to *offer* those placements would have had to work them
    // out again. It did — that is why the phase shipped with no interface. The
    // menu and the order now ask the same module, so a spot the board draws and
    // a placement the engine takes cannot drift apart.
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const source = stripStrings(stripComments(readFileSync(file, 'utf8')))
        // Retiring the aggregate is the lifecycle's job — it owns the phase
        // change that ends deployment — and is not reasoning about the roster.
        .replace(/\bstate\.deployment\s*=\s*null/g, '');
      // The runtime aggregate, not the level document: `LevelDeployment` also
      // has an `order`, and flagging a validator for reading the file it
      // validates would be a false positive that the guard gets deleted over.
      const reads = /\bstate\.deployment\b|\bdeployment[?]?\.(?:assignments|currentIndex)\b/.test(source);
      // `state.ts` builds it and copies it; `deployment.ts` is the rule.
      const owner = ['deployment.ts', 'state.ts'].includes(relative(coreRoot, file));
      return reads && !owner ? [relative(coreRoot, file)] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('lets only the casting rules mutate the charge queue', () => {
    // Everything else reads it through `activeCasts()`, which is what keeps a
    // dead caster's charge from being visible to some readers and not others.
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const source = readFileSync(file, 'utf8');
      const writes = /pendingCasts\s*(?:=[^=]|\.push\(|\.splice\()/.test(source);
      const owner = ['casting.ts', 'state.ts'].includes(relative(coreRoot, file));
      return writes && !owner ? [relative(coreRoot, file)] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('never throws the one error class that says nothing', () => {
    // The engine declares what a failure means — `IllegalActionError` (the order
    // was wrong, show it), `DomainInvariantError` (the caller was wrong, a
    // defect), `StoredDocumentError` (the file cannot be read) — and eighty-four
    // throws opted out by raising the base class. Only three sites in the whole
    // repository branch on error type, so an unclassified throw is not merely
    // untidy: it is indistinguishable from every other kind at the one place
    // that has to tell them apart. A reinforcement landing on an occupied tile —
    // ordinary play — reached the shell as a bare `Error` and ended the battle.
    //
    // Battle and campaign both expose this contract. Match construction rather
    // than only `throw new Error`: dependency-order callbacks used to return a
    // bare error and walked straight through the old guard.
    // `RangeError` stays allowed; it names a category of its own.
    const offenders = [
      ...runtimeTypeScriptFiles(coreRoot),
      ...runtimeTypeScriptFiles(campaignRoot),
    ].flatMap((file) =>
      /new Error\(/.test(stripComments(readFileSync(file, 'utf8')))
        ? [relative(packagesRoot, file)]
        : []);

    expect(offenders).toEqual([]);

    // Named subclasses may add context, but classification still terminates in
    // one of the domain boundary modules. A new direct `extends Error` silently
    // creates the fourth category the catch sites cannot classify.
    const categoryOwners = new Set([
      'battle-engine/src/domain/errors.ts',
      'campaign-engine/src/errors.ts',
    ]);
    const unclassifiedClasses = [
      ...runtimeTypeScriptFiles(coreRoot),
      ...runtimeTypeScriptFiles(campaignRoot),
    ].flatMap((file) => {
      const name = relative(packagesRoot, file);
      if (categoryOwners.has(name)) return [];
      return /class\s+\w*Error\s+extends\s+Error\b/.test(stripComments(readFileSync(file, 'utf8')))
        ? [name]
        : [];
    });

    expect(unclassifiedClasses).toEqual([]);
  });

  it('never relabels a caught error as a refused order', () => {
    // Wrapping a collaborator in `try/catch` and calling `fail(error.message)`
    // presents genuine defects to the player as "that move is not allowed".
    // Collaborators raise `IllegalActionError` themselves instead.
    const pattern = /catch\s*\([^)]*\)\s*\{[^}]*\bfail\(/;
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) =>
      pattern.test(readFileSync(file, 'utf8')) ? [relative(coreRoot, file)] : []);

    expect(offenders).toEqual([]);
  });

  it('never lets a caught error decide anything', () => {
    // Sibling of the guard above. `catch { return false }` answers a question
    // with an exception it never looked at: a weapon on cooldown and a weapon
    // the content never defined come back as the same quiet "no", and the
    // second one stops being findable. Asking and committing are different
    // acts — the query returns null, the command throws.
    //
    // The rule is the axiom's, letter for letter: an unbound `catch` may not
    // produce a value, so its body must be empty and say why in a comment.
    // Anything else — a fallback, a status message, a rethrow of something new
    // — has to bind the error and look at it.
    //
    // This guard used to look only for `return` inside an unbound catch, and
    // that is not the only way to produce a value. It missed `catch { events =
    // this.session.tryDispatch(...) }` in the AI loop, which turned every defect
    // thrown anywhere in the rules into an invisible turn pass, and `catch {
    // existing = [] }` in level storage, which then wrote the empty list back.
    const offenders: string[] = [];
    for (const file of [...everyPackageSource(), ...appSources()]) {
      // Comments are stripped first: a doc comment that *quotes* the mistake it
      // is warning about is not the mistake, and the guard flagged this very
      // file's explanation of itself before the strip went in.
      const source = stripComments(readFileSync(file, 'utf8'));
      for (const match of source.matchAll(/catch\s*\{/g)) {
        let depth = 0;
        let end = match.index;
        for (let index = match.index; index < source.length; index++) {
          if (source[index] === '{') depth++;
          else if (source[index] === '}' && --depth === 0) {
            end = index;
            break;
          }
        }
        const body = source.slice(source.indexOf('{', match.index) + 1, end).trim();
        if (body.length > 0) {
          offenders.push(`${relative(packagesRoot, file)}: unbound catch does something (${body.slice(0, 40)})`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it('asks one question about the right to act', () => {
    // Every caller must go through `mayAct`/`commandableUnit`. Re-deriving
    // "mine and not yet done" inline is what silently ignored the ordering
    // policy in six action handlers.
    // Scoped to *unit* ownership on purpose: a commander's allegiance and a
    // building's owner are different questions with their own rules.
    const pattern = /\b(?:unit|actor|passenger|carrier|troop|candidate)\.owner\s*[!=]==\s*\w+\.currentPlayer/;
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const name = relative(coreRoot, file);
      if (name === 'turn-order.ts') return [];
      return pattern.test(readFileSync(file, 'utf8')) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('settles damage in exactly one place', () => {
    // Applying damage means: report it, resolve the death, spill the transport,
    // shake the survivors, tell whoever cares that a unit left. Five sites used
    // to do that by hand, in three different orders. `resolveDamage` owns it;
    // only the aggregate beneath it may take a unit's hit points away, and only
    // the departure module may raise the fall it produces.
    // `battle-aggregate.ts` was exempt here and no longer settles a blow itself.
    const allowed = ['damage.ts', 'unit-departure.ts'];
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const name = relative(coreRoot, file);
      if (allowed.includes(name)) return [];
      return /\.damageUnit\(|announceUnitFall\(/.test(readFileSync(file, 'utf8')) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('puts a unit back on the field in exactly one place', () => {
    // Reviving a corpse and recalling a withdrawal were two hand-written
    // blocks, and they had drifted: one floored morale on the way back and the
    // other did not, so a unit revived from a rout returned at zero and broke
    // again on the next shock. `state.units.push` is the tell — anything that
    // adds a unit to the battlefield goes through a named lifecycle step.
    // `transports.ts` is deliberately not one of them: a passenger stepping
    // out of a carrier never left the world, so it keeps its statuses and its
    // morale. Curing a poison by taking a taxi is not a lifecycle rule anyone
    // wants unified into this one.
    const allowed = ['unit-return.ts', 'state.ts', 'transports.ts'];
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const name = relative(coreRoot, file);
      if (allowed.includes(name)) return [];
      return /\bunits\.push\(/.test(readFileSync(file, 'utf8')) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('asks one question about where a unit may stand', () => {
    // Impassable ground and a structure that fills the tile are two layers, and
    // both have to say yes. Deployment, disembarking, a shove, a teleport, a
    // scenario spawn and a rescue from a corpse marker each asked the two
    // layers by hand — six copies of one rule, each free to remember only half
    // of it. The cell answers it now; `blocksMovement` outside the cell means
    // somebody is assembling the rule again.
    const allowed = ['types.ts', 'content-builders.ts', join('domain', 'battlefield.ts')];
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const name = relative(coreRoot, file);
      if (allowed.includes(name)) return [];
      return /\bblocksMovement\b/.test(readFileSync(file, 'utf8')) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('reads a standing order through the registry, never by name', () => {
    // The four orders were compared to string literals in four places — what
    // ground to want, how close to stand to the enemy, whether to stop and
    // fight, when a patrol advances — so a fifth order meant finding all four,
    // and one of them had already grown a fall-through nobody wrote on purpose.
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const name = relative(coreRoot, file);
      if (name === 'unit-directive.ts') return [];
      return /directive\.mode\s*[!=]==?\s*'/.test(readFileSync(file, 'utf8')) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('never asserts that a value is there', () => {
    // `x!` is a guess the type checker disagreed with, written where a refusal
    // belongs. Every one of them was load-bearing on something the reader could
    // not see: that the caller had already null-checked the map, that a `find`
    // over a list built elsewhere could not miss, that a push two lines above
    // had worked. Ask, refuse, hold what you found, or pass it in.
    //
    // The first version of this guard matched only `x!.field` and `x![i]`, so
    // it passed while sixteen assertions of the other shapes — `f()!`, `id!`,
    // `g(x!)` — sat in the same files. It is written against the assertion
    // operator itself now: a `!` right after something that can produce a
    // value, and not the start of `!=`.
    //
    // And the second version read the packages only. The last three assertions in
    // the repository were in the shells: two of `document.getElementById('app')!`
    // — while two sibling shells checked for the element and named it in a refusal
    // — and one on a lookup whose key came from a regular expression written above
    // it, in a hand-rolled copy of an escaper the same file already imported.
    const offenders = [...everyPackageSource(), ...appSources()].flatMap((file) => {
      const code = stripStrings(stripComments(readFileSync(file, 'utf8')));
      return /[A-Za-z0-9_$)\]]!(?!=)/.test(code) ? [relative(packagesRoot, file)] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('never writes out the facings of a board', () => {
    // Three times now a module has kept its own list of `north | east | south |
    // west`: the level linter refused every hex facing, the map builder threw on
    // one before a state could be built, the sprite badge drew an empty circle,
    // the render key hashed nothing, and the editor could not author cover on
    // those boards at all. The tiling declares its facings; ask it.
    // The decorations module was exempt and now asks the tiling like everything else.
    //
    // Widened rather than joined by a second guard, because one axiom gets one
    // guard. A *list* of facings was the only tell, and three single ones were
    // sitting in plain sight: both boards mirrored a sprite on `facing ===
    // 'west'`, so a diagonal board drew a unit facing away to the left looking
    // right and a hex board drew every unit that way; `state.ts` gave a unit with
    // no authored facing `'south'`, which a hex tiling would have refused had
    // `validateLevel` been the one to see it; and the editor's brush opened
    // pointing `'north'`.
    //
    // Content is not exempted, because content is not the offender: a level
    // authoring `facing: 'west'` for its own square board is naming a direction
    // that board has. The tell is a facing inside a *decision* — compared
    // against, or defaulted to — which is what no module but the tiling may make.
    const owners = ['battle-engine/src/tactical-grid.ts'];
    const offenders = [...everyPackageSource(), ...appSources()].flatMap((file) => {
      const name = relative(packagesRoot, file);
      if (owners.includes(name)) return [];
      // Strings stay: a facing *is* one. Only the prose goes. And the pattern is
      // one literal rather than three assembled ones so that the exemption above
      // stays checkable — the meta-guard reads this line.
      const code = stripComments(readFileSync(file, 'utf8'));
      return /'north'[\s\S]{0,80}'(?:east|south|west)'|(?:===|!==|\?\?)\s*'(?:north|east|south|west|north(?:east|west)|south(?:east|west)|hex[A-Za-z]+)'|: Direction = '(?:north|east|south|west)'/.test(code) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('asks a payload what it points at, never what kind it is', () => {
    // Which names a scenario effect, a trigger condition or an objective writes
    // down was enumerated by whoever wanted to resolve them: two hundred lines
    // of `effect.type === '…'` in the level linter, and the same list a third
    // time to find which effects hand out a standing order. A rule pack's own
    // kind was in none of those lists, so it was linted by nobody — a closed
    // union grown back inside an open registry. The kinds answer for themselves
    // through `references` now, and only their own modules may name them.
    const owners = [
      // `scenario.ts`, `objective-system.ts` and `types.ts` were exempt here, and
      // none of them names a payload kind any more: the ladders they were exempt
      // for are what `references` replaced.
      // A catalog judges the payloads it can judge alone, and says so in place.
      'content-pack.ts',
      // Both read `TacticEffect`: a deliberately closed two-case union — grant a
      // status or strip one — and not an open registry at all.
      'commanders.ts',
      'ai/default-intents.ts',
    ];
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const name = relative(coreRoot, file);
      if (owners.includes(name)) return [];
      // Comments talk about the old shape on purpose; a guard that matches its
      // own explanation guards nothing.
      const code = stripComments(readFileSync(file, 'utf8'));
      return /(?:effect|condition|objective)\.type\s*[!=]==?\s*'/.test(code) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('names a resource holder in exactly one place', () => {
    // Who can hold an account was stated six times: an open kind map, a closed
    // union on the transaction, a three-way `switch` that built one, a closed
    // union on the event, a ternary that filled that union in, and a third
    // ternary in the battle log. Only the first was open, so a plugin could
    // declare a holder no cost could charge and no line could mention. Writing
    // `kind: 'player'` outside the resolvers is how the sixth copy starts.
    const allowed = ['resources.ts', 'types.ts'];
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const name = relative(coreRoot, file);
      if (allowed.includes(name)) return [];
      return /kind:\s*'(?:player|unit|weapon)'/.test(readFileSync(file, 'utf8')) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('strikes one blow between two units in exactly one place', () => {
    // The volley, the riposte and the ally's covering shot are one act, and
    // they were written out three times: report the blow, teach the striker,
    // apply the weapon's rider, credit whoever survived it. The copies had
    // drifted — only one re-checked that the target was still standing after
    // the rider resolved, and only one noticed a blow that never landed. Two
    // calls in this file means a fourth copy is growing.
    const source = readFileSync(join(coreRoot, 'combat-plan.ts'), 'utf8');
    const twice = ['resolveDamage(', 'awardDamageTakenMomentum(', 'hitEffects.apply(']
      .filter((call) => source.split(call).length - 1 !== 1);

    expect(twice).toEqual([]);
  });

  it('keeps a types module to types, so a pack can safely merge into it', () => {
    // `types.ts` is 1200 lines and 110 exports, and the instinct is to split it
    // by size. That is the wrong axis: seven of those exports are the
    // declaration-merged kind maps a content pack augments, and one module is
    // exactly what makes `declare module '../types'` a single, obvious target.
    // Cohesion here is "the vocabulary a pack extends", not line count.
    //
    // What was wrong with it was one runtime value — `DEFAULT_RULES` — sitting
    // in a module whose whole contract is that it is erased. It now lives with
    // the other three answers to "what does a level default to". This keeps that
    // from creeping back, which is the only part a reader cannot check at a
    // glance.
    const offenders = everyPackageSource()
      .filter((file) => /(^|[\\/])types\.ts$/.test(file))
      .flatMap((file) => {
        const source = stripStrings(stripComments(readFileSync(file, 'utf8')));
        const values = source.match(/^export\s+(?:const|let|var|function|class|enum)\b.*/gm) ?? [];
        return values.map((line) => `${relative(packagesRoot, file)}: ${line.trim().slice(0, 60)}`);
      });

    expect(offenders).toEqual([]);
  });

  /**
   * A registry a module publishes is sealed by that module.
   *
   * Composition and runtime are different phases. A plugin `register`s and
   * `replace`s while an engine is being assembled; after that the ruleset is a
   * fact, and a registry that can still be edited is ambient mutable state with a
   * polite interface — one import with a side effect, or one stray `replace` from
   * a shell, and two engines in one process disagree about the rules.
   *
   * `seal()` closes that, and nineteen modules call it. Nothing checked that the
   * twentieth would: the guard next door forbids a top-level `new Map()`, and a
   * registry is exactly a `Map` with better manners. So the rule is stated where
   * it can be enforced — whoever declares a registry at module level seals it in
   * the same module, in whichever of the three spellings reads best.
   *
   * A registry built *inside* something is not module-level state and is not
   * covered: `new ContentCatalog()` per engine, `clone()` per composition.
   */
  it('seals every registry a package publishes at module level', () => {
    const sources = [...everyPackageSource(), ...appSources()];
    const code = new Map(sources.map((file) => [file, stripComments(readFileSync(file, 'utf8'))]));

    // What counts as a registry: the shared bases, and anything deriving from one.
    const registries = new Set(['KeyedRegistry', 'PriorityRegistry', 'ContentRegistry']);
    for (let grew = true; grew;) {
      grew = false;
      for (const source of code.values()) {
        for (const [, name, base] of source.matchAll(/class (\w+)(?:<[^>]*>)?\s+extends\s+(\w+)/g)) {
          if (registries.has(base) && !registries.has(name)) {
            registries.add(name);
            grew = true;
          }
        }
      }
    }
    // The bases plus the shipped subclasses; a run that found only the bases would
    // check nothing at all.
    expect(registries.size).toBeGreaterThan(10);

    const offenders: string[] = [];
    let sealed = 0;
    for (const [file, source] of code) {
      for (const declaration of source.matchAll(/^(?:export )?const (\w+)[^=\n]* = new (\w+)[<(]/gm)) {
        const [, name, type] = declaration;
        if (!registries.has(type)) continue;
        // Either `Name.seal()` somewhere in the module, or `.seal()` chained onto
        // the declaration itself — which is the same statement until the next one
        // starts at the left margin.
        const statement = source.slice(declaration.index).split(/\n(?=\S)/)[0];
        if (new RegExp(String.raw`\b${name}\s*\.\s*seal\(`).test(source) || statement.includes('.seal()')) {
          sealed++;
          continue;
        }
        offenders.push(`${relative(packagesRoot, file)} publishes ${name} unsealed`);
      }
    }

    expect(sealed).toBeGreaterThan(15);
    expect(offenders).toEqual([]);
  });

  it('builds every extension point on the shared registry', () => {
    // Twelve registries had each hand-written the same table, and they had
    // drifted where it mattered: some could be *asked* for an entry, some could
    // only be told and would throw, which is why the HUD once wrapped a lookup
    // in `try/catch` to print a fallback label. A registry that owns its own
    // Map is a registry free to be missing half its interface again.
    //
    // The two exceptions are not keyed tables at all: terrain encoding is a
    // bijection between characters and terrain, and the damage matchup is a
    // matrix keyed by a pair.
    //
    // Two holes this had to grow out of. It matched classes *named* `…Registry`,
    // and the editor's tool set was called `EditorToolbox` — a `Map` with a
    // `get` and none of the rest, under a comment promising that a tool set is
    // meant to grow. And it scanned the engine only, while the packages around
    // it hold extension points of their own. It looks for the shape now: a class
    // that owns a keyed table and answers lookups from it.
    const allowed = [
      // Not keyed tables at all: terrain encoding is a bijection between
      // characters and terrain, and a damage matchup is keyed by a pair.
      'battle-engine/src/data/terrain-encoding.ts',
      'battle-engine/src/data/damage.ts',
      // The base itself.
      'battle-engine/src/registry.ts',
      // A scheduler, not a contribution set: what its `register` admits is one
      // *running* animation track, and `unregister` ends it. The entries are the
      // work in flight, not the strategies that do the work.
      'game-ui/src/art/frame-animation.ts',
    ];
    const offenders = [...everyPackageSource(), ...appSources()].flatMap((file) => {
      const name = relative(packagesRoot, file);
      if (allowed.includes(name)) return [];
      const source = stripComments(readFileSync(file, 'utf8'));
      return [...source.matchAll(/(?:export )?(?:abstract )?class (\w+)\b([^{]*)\{([\s\S]*?)\n\}/g)]
        // A contribution set, not a cache: something outside may put entries in.
        // A `Map` of DOM nodes, of running animations or of per-call findings is
        // state, and none of those is an extension point.
        .filter(([, , heritage, body]) =>
          !/extends\s+(?:Keyed|Priority|Content)Registry/.test(heritage) &&
          /(?:private |protected )?readonly \w+ = new Map</.test(body) &&
          // A declaration, not a call: `registerSvgStrip(this.animations, …)`
          // inside a method body says nothing about the class it sits in — and nor
          // does `this.parts.get(role)`, which is how a drawing reaches its own
          // declared parts. Both halves ask for a method the class defines.
          /\n[ \t]+(?:override )?(?:public |private |protected )?(?:register|define|add)\w*\([^)]*\)\s*[:{]/.test(body) &&
          /\n[ \t]+(?:override )?(?:public |private |protected )?(?:get|tryGet|forHotkey|lookup)\w*\([^)]*\)\s*[:{]/.test(body))
        .map(([, className]) => `${name}: ${className}`);
    });

    expect(offenders).toEqual([]);
  });

  it('writes the map\'s spatial layers in exactly one place', () => {
    // Ground, height, ownership, capture progress, blocked edges and directional
    // cover are six layers of one map, and everything that changed them reached
    // into the arrays: four scenario effects, the capture ability, two unit
    // lifecycle steps, the editor document and — past the document entirely —
    // the editor controller. The copies had already drifted. One matched a cliff
    // edge by `edgeKey`, another by comparing both orientations by hand; one
    // dropped an emptied cover entry, another left it behind; the capture
    // ability wrote the same claim twice, once per way of arriving at it.
    // `MapLayers` owns the writes and reports what changed. `level/map.ts` is
    // exempt: it *builds* a map from a document rather than changing a live one.
    const writes = [
      /\bmap\.(?:tiles|elevation|owners|captureProgress)\[[^\]]*\]\s*=[^=]/,
      /\bmap\.cliffs\.(?:push|splice)\(/,
      /\bmap\.directionalCover(?:\s*=[^=]|\.push\()/,
    ];
    const allowed = [
      join('battle-engine', 'src', 'domain', 'map-layers.ts'),
      join('battle-engine', 'src', 'level', 'map.ts'),
    ];
    const offenders = [
      ...runtimeTypeScriptFiles(coreRoot),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'editor', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'game-ui', 'src')),
    ].flatMap((file) => {
      const name = relative(packagesRoot, file);
      if (allowed.includes(name)) return [];
      const source = readFileSync(file, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
      return writes.some((pattern) => pattern.test(source)) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('asks a trigger whether it is due, never its repeat block', () => {
    // Six inline conditions decided whether a repeating trigger could fire, and
    // the ledger behind them was read twice with the same defaulted literal —
    // once to test, once to update. A second caller (a debug panel, an editor
    // preview, a campaign that pre-fires a trigger) would have had to reproduce
    // all of it. The trigger answers for itself.
    const allowed = [join('domain', 'scenario-trigger.ts'), 'types.ts'];
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const name = relative(coreRoot, file);
      if (allowed.includes(name)) return [];
      const source = readFileSync(file, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
      // Seeding the ledgers and hashing them are fine; *deciding* with them is not.
      const tells = [
        /\btriggerRuntime\[/,
        /\btrigger\.repeat\b/,
        /\brepeat\.(?:everyRounds|startTurn|endTurn|maxFirings)\b/,
        /\bfiredTriggerIds\.(?:includes|indexOf|some)\(/,
      ];
      return tells.some((tell) => tell.test(source)) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('measures the board through its tiling, never with a fixed metric', () => {
    // Four-directional geometry used to be *assumed* in a dozen modules:
    // Manhattan `dist` in fifteen files, four `NEIGHBOURS` vectors in the
    // pathfinder, a diamond `ring` in five callers, four direction names and
    // four vectors in the flanking rules, and `x * TILE` in the board and every
    // decoration. None of it was wrong; all of it was unstated, which is why an
    // eight-way or hex board was an engine rewrite rather than a level's choice.
    // A hardcoded metric or direction vector outside the tilings is that
    // assumption growing back.
    //
    // It did, twice over, and both escapes were in this guard's own shape. It
    // only scanned three packages, and the campaign package — which reviews its
    // own levels — hand-wrote a Manhattan sum that fed a pacing assertion. And
    // the first tell demanded the *whole* two-term expression, so a Chebyshev
    // max or a single-axis comparison walked past it. The root cause under both:
    // `tactical-grid.ts` was not exported from the package root, so a module
    // outside the engine that wanted a distance had nothing to call.
    const tells = [
      // Half an expression is enough: Chebyshev, a single-axis test and a
      // partially-written Manhattan sum all start here.
      /Math\.abs\([A-Za-z_$][\w.$\[\]]*\.(?:x|y)\s*-/,
      /\bNEIGHBOURS\b/,
      /\{ x: 0, y: -1 \},\s*\{ x: 1, y: 0 \}/,
      /'north'\s*\|\s*'east'/,
    ];
    const allowed = [
      join('battle-engine', 'src', 'tactical-grid.ts'),
      // Storage geometry, which is not the tiling's: `sharesEdge` answers whether
      // two cells of the *file* touch, which is what defines a cliff edge.
      join('battle-engine', 'src', 'grid.ts'),
      // The editor draws the storage rectangle on purpose: a brush paints cells.
      join('editor', 'src', 'board.ts'),
    ];
    // Every workspace package and every application entry point, because the
    // module that measures is exactly the one nobody thinks to scan.
    const offenders = [...everyPackageSource(), ...appSources()].flatMap((file) => {
      // Screen space is not board space: pixels are the renderer's own units.
      if (file.includes(`${sep}art${sep}`)) return [];
      const name = relative(packagesRoot, file);
      if (allowed.includes(name)) return [];
      const source = stripStrings(stripComments(readFileSync(file, 'utf8')));
      return tells.some((tell) => tell.test(source)) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('asks one question about the right to react', () => {
    // The same lesson as `mayAct`: a budget compared by hand at each site is a
    // budget that the next kind of reaction quietly gets a second copy of.
    // `types.ts` declares the field; `state.ts` seeds it; nobody else looks.
    const allowed = ['types.ts', 'state.ts', join('domain', 'unit-entity.ts')];
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) => {
      const name = relative(coreRoot, file);
      if (allowed.includes(name)) return [];
      return readFileSync(file, 'utf8').includes('reactionUsedRound') ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });
});

describe('a rendered control is answered', () => {
  it('answers every intent it declares itself', () => {
    // The HUD and the editor each have a runtime test for this: render, collect
    // every `data-act`, and require the controller to handle it. Both exist
    // because a typo in `data-act` produces a button that looks alive and does
    // nothing — the editor's comment says its switch had grown to a hundred lines
    // with the two halves in different parts of the file.
    //
    // The two application shells had the same click listener and no such check.
    // And the campaign shell had a *third* attribute name, `data-campaign-act`,
    // dispatched by an if-chain in the same file — outside this guard's scope, its
    // attribute and its two accepted shapes at once. A button in the one screen
    // that carries a whole campaign was the least fenced of the four.
    //
    // The rule needs no file list: a file that answers *some* of what it declares
    // is dispatching its own intents and must answer all of them. A file that
    // answers none of them is a view handing them to a controller elsewhere —
    // `panels.ts` to `app.ts` — and that pair is what the runtime tests cover.
    const checked: string[] = [];
    const offenders = [...everyPackageSource(), ...appSources()].flatMap((file) => {
      const source = readFileSync(file, 'utf8');
      const declared = new Set([...source.matchAll(/data-(?:campaign-)?act="([^"$]+)"/g)].map(([, act]) => act));
      if (declared.size === 0) return [];
      // A switch, a table of them, or an if-chain: three shapes, all fine.
      const answered = new Set([
        ...[...source.matchAll(/case '([^']+)':/g)].map(([, value]) => value),
        ...[...source.matchAll(/^\s+'?([\w-]+)'?:\s*(?:\(|async)/gm)].map(([, key]) => key),
        ...[...source.matchAll(/=== '([^']+)'/g)].map(([, value]) => value),
      ]);
      const mine = [...declared].filter((act) => answered.has(act));
      if (mine.length === 0) return [];
      checked.push(relative(packagesRoot, file));
      return [...declared]
        .filter((act) => !answered.has(act))
        .map((act) => `${relative(packagesRoot, file)}: nothing answers "${act}"`);
    });

    // Four files dispatch their own intents. A run that found none of them would
    // pass by having skipped everything as a delegator.
    expect(checked.length).toBeGreaterThanOrEqual(4);
    expect(offenders).toEqual([]);
  });
});

describe('the board decides, a surface draws', () => {
  /**
   * `BoardView` does not touch the DOM.
   *
   * It used to be two jobs fused together: deciding what a battlefield looks like —
   * layers, placement, animation — and putting SVG elements in a tree. The first is
   * all of the game knowledge; the second is a backend, and a field of 5,575 nodes
   * under a colour matrix is the wrong shape for the DOM. There was no seam to put
   * another renderer behind, and this is what says the seam is still there: every
   * element, class, attribute and query lives below `BoardSurface`.
   */
  it('keeps every element, class and query below the surface port', () => {
    const board = stripComments(
      readFileSync(join(packagesRoot, 'game-ui', 'src', 'ui', 'board.ts'), 'utf8'),
    );
    const reachedFor = [
      'document.',
      'querySelector',
      'classList',
      'getBoundingClientRect',
      'addEventListener',
      'fromMarkup',
      'setAttrs',
      'appendChild',
      '.innerHTML',
    ].filter((token) => board.includes(token));

    expect(reachedFor).toEqual([]);
    // And it reaches the renderer only through the port's own vocabulary.
    expect(board).toMatch(/import \{[^}]*type BoardSurface[^}]*\} from '\.\.\/art\/board-surface'/);
    // Nothing here checks that a per-cell layer crosses the seam as one piece per
    // cell. A regex cannot: `wholeField(parts.join(''))` is the correct way to hand
    // over line work, and is indistinguishable from collapsing four thousand tiles
    // into one string. `ui.test.ts` counts the pieces on a rendered board instead,
    // which is the same claim made where it can actually be observed.
  });

  /**
   * The board does not choose the renderer.
   *
   * It used to write `new SvgBoardSurface(…)` in its constructor, which makes the
   * port a seam with only one possible other side — the same defect as an engine
   * that rebuilds its own defaults instead of being composed. The renderer arrives
   * in `BoardComposition` with the content, the tiling and the art, from whoever
   * composes a battle.
   */
  it('leaves the choice of renderer to whoever composes a battle', () => {
    const board = stripComments(
      readFileSync(join(packagesRoot, 'game-ui', 'src', 'ui', 'board.ts'), 'utf8'),
    );
    const named = [...board.matchAll(/new\s+(\w*(?:Board)?Surface)\s*\(/g)].map(([, name]) => name);
    expect(named).toEqual([]);
    expect(board).not.toMatch(/from '\.\.\/art\/\w*-board-surface'/);
    // And what it does hold is a factory it was handed.
    expect(board).toMatch(/composition\.renderer\(/);
    // Nothing here checks that animations use only the port's four continuous
    // properties, because with the DOM out of reach `tsc` already does: the only
    // thing the board can animate is a `BoardDrawing`, and `place`, `nudge`,
    // `swell` and `opacity` are all it declares. A test for it would look stronger
    // than the type and be weaker.
  });

  /**
   * A frame strip is declared, never serialised into markup and read back.
   *
   * It used to cross the renderer seam as an `<image>` carrying its own
   * description: `data-frame-width`, `data-frame-count`, `data-frame-initial` and
   * `data-frame-clips="[{…}]"`. One producer wrote those from data of exactly this
   * shape; one reader found the element by class name, parsed all four back, and
   * validated them against the possibility of being malformed.
   *
   * The cost was not the parsing. It was that the animation had become a DOM trick
   * described where only a DOM reader could look, so `play` on the GPU backend was
   * a no-op and a unit stood on one frame — and the obvious fix, which the design
   * note here actually recommended at the time, was a *second* parser of the same
   * attributes.
   *
   * So: nowhere in any shipped package does a picture describe its own animation.
   */
  it('declares a frame strip instead of writing its description into a picture', () => {
    const offenders = [...everyPackageSource(), ...appSources()]
      .filter((path) => stripComments(readFileSync(path, 'utf8')).includes('data-frame-'))
      .map((path) => relative(packagesRoot, path));

    expect(offenders).toEqual([]);
  });

  /**
   * The timeline has no idea what a frame looks like.
   *
   * `FrameAnimationTarget` is `{ frameCount, setFrame(frame) }` and always was, so
   * this module claimed to be backend-agnostic while holding the one function that
   * made it not: a reader that took an `SVGImageElement`. Both backends register a
   * plain target now — one moves a `viewBox`, the other assigns `sprite.texture` —
   * and a third would too.
   */
  it('keeps the frame timeline free of any idea what a frame is drawn with', () => {
    const timeline = stripComments(
      readFileSync(join(packagesRoot, 'game-ui', 'src', 'art', 'frame-animation.ts'), 'utf8'),
    );
    const reachedFor = ['SVG', 'Element', 'getAttribute', 'querySelector', 'document', 'Texture']
      .filter((token) => timeline.includes(token));

    expect(reachedFor).toEqual([]);
  });

});

describe('a picture is not optional', () => {
  /**
   * No generic art table may answer an id it does not hold with another entry of
   * itself.
   *
   * The same defect stood in three places for as long as the art layer existed:
   * `painters[id] ?? painters.plain` drew every unfamiliar terrain as grass,
   * `sprites[type] ?? sprites.soldier` drew every unfamiliar unit as a swordsman,
   * and `portraits[type] ?? portraits.soldier` did it again in the inspector.
   * Eleven of twenty-two shipped terrains and thirty-one of forty shipped unit
   * types hit those lines, and the map editor — which draws with the generic art —
   * showed the whole campaign that way.
   *
   * The answer for an id nobody drew is a drawing derived from what the rules can
   * see, never another id's picture. This forbids the shape that keeps coming
   * back: an index into a table, defaulted to a fixed key of the same table.
   */
  it('never answers an unknown id with another entry of the same table', () => {
    const artRoot = join(packagesRoot, 'game-ui', 'src', 'art');
    const offenders = runtimeTypeScriptFiles(artRoot).flatMap((file) => {
      const code = stripComments(readFileSync(file, 'utf8'));
      // `table[key] ?? table.fixed` and `table[key] ?? table['fixed']`.
      const borrowed = new RegExp(
        String.raw`(\w+)\[[^\]]+\]\s*\?\?\s*\1(?:\.\w+|\[['"\`]\w+['"\`]\])`,
        'g',
      );
      return [...code.matchAll(borrowed)]
        .map(([match]) => `${relative(packagesRoot, file)}: ${match.trim()}`);
    });

    expect(offenders).toEqual([]);
  });

  /**
   * And declining to draw is not the same as drawing nothing.
   *
   * `BattlePresentation.structure` and `.marker` return `null` for "no opinion",
   * the convention every `ArtProvider` method already uses — and the board read it
   * as "draw nothing". Six structure types and five marker kinds shipped here were
   * invisible on any board with no painted scene, including the 500 HP structure
   * that is `c01-15`'s victory condition.
   */
  it('draws a field object the presentation declined', () => {
    const board = readFileSync(join(packagesRoot, 'game-ui', 'src', 'ui', 'board.ts'), 'utf8');
    for (const declined of ['structure', 'marker']) {
      const asked = new RegExp(String.raw`presentation\.${declined}\([^)]*\)\s*\n?\s*\?\?\s*\w+FromRules\(`);
      expect(asked.test(stripComments(board)), declined).toBe(true);
    }
  });

  /**
   * `null` is no opinion. Empty is an answer.
   *
   * `ArtDirection.resolve` returns `T | null` and stops at the first provider that
   * answers, so a pack has two distinct things it can say: "I have no view on this
   * terrain, draw it from the rules" and "my painted scene has already drawn this
   * ground, draw nothing". Four consumers collapsed them by testing the answer for
   * truthiness, which made the second one unreachable.
   *
   * The campaign worked around it the only way left: `candidate01TerrainMarkup`
   * returned an invisible non-empty group so that the fallback would not fire. One
   * per cell — 4,131 empty groups and 8,352 nodes on the largest shipped map, 22%
   * of everything on the board, drawing nothing.
   *
   * What is forbidden is putting a *fallback* in place of an empty answer: `if (x)
   * return x` before a floor, `x || floor`, `x ? x : floor`. `?? floor` is the
   * comparison to `null` written the short way and is right.
   *
   * Discarding an empty answer is not the same mistake and is allowed — a cover
   * prop's `if (prop) pieces.push(…)` loses nothing, because markup with nothing
   * in it is not a piece however it came to be empty.
   */
  it('never substitutes a fallback for an empty answer from the art', () => {
    const offenders: string[] = [];
    for (const file of [...everyPackageSource(), ...appSources()]) {
      const source = stripComments(readFileSync(file, 'utf8'));
      for (const [, name] of source.matchAll(/const (\w+) = [\w.]*\bresolve\(/g)) {
        const substituted = new RegExp(
          String.raw`if\s*\(\s*${name}\s*\)\s*return|\b${name}\s*\|\||\b${name}\s*\?(?![.?])`,
        );
        if (substituted.test(source)) {
          offenders.push(`${relative(packagesRoot, file)} falls back when ${name} is empty`);
        }
      }
    }

    // Every one of these consumers exists, or the guard is looking at nothing.
    const consumers = [...everyPackageSource(), ...appSources()]
      .filter((file) => /\bresolve\(/.test(stripComments(readFileSync(file, 'utf8'))));
    expect(consumers.length).toBeGreaterThan(3);
    expect(offenders).toEqual([]);
  });
});

describe('the battle screen is its own screen', () => {
  /** Every stylesheet the workspace ships, by package-relative path. */
  function stylesheets(): string[] {
    return readdirSync(packagesRoot, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .flatMap((entry) => {
        const styles = join(packagesRoot, entry.name, 'src', 'styles');
        if (!statSync(styles, { throwIfNoEntry: false })?.isDirectory()) return [];
        return readdirSync(styles).filter((file) => file.endsWith('.css')).map((file) => join(styles, file));
      });
  }

  /**
   * A renderer nobody asked for is not in the bundle.
   *
   * `pixi.js` is 492 KB. Re-exported from the main barrel it landed in the game's
   * bundle whether or not a session ever chose that backend — the other three apps
   * were unaffected, because they never name it and the barrel tree-shakes, but the
   * one app that offers the choice paid for it up front. So it has its own entry
   * point, `@empire/game-ui/pixi`, and the app imports it when the choice is made.
   *
   * This is what keeps that true: nothing reachable from the main entry may name it.
   */
  it('keeps the GPU backend out of everything that does not ask for it', () => {
    const root = join(packagesRoot, 'game-ui', 'src');
    const files = new Set(runtimeTypeScriptFiles(root));
    const reachable = new Set<string>();
    const walk = (file: string): void => {
      if (reachable.has(file)) return;
      reachable.add(file);
      for (const next of localCoreImports(file, files)) walk(next);
    };
    walk(join(root, 'index.ts'));
    // A traversal that reached almost nothing would pass for the wrong reason.
    expect(reachable.size).toBeGreaterThan(15);

    const named = [...reachable]
      .filter((file) => /from\s+['"]pixi\.js['"]/.test(readFileSync(file, 'utf8')))
      .map((file) => relative(packagesRoot, file));
    expect(named).toEqual([]);
    // And the optional entry exposes one composition path, not the painter,
    // texture cache and half-assembled factory as three competing public APIs.
    const entry = stripComments(readFileSync(join(root, 'pixi.ts'), 'utf8'));
    expect(entry).toMatch(/preparePixiBoardSurface/);
    expect(entry).not.toMatch(/BrowserPictureTextures|pixiBoardSurface|preparePixiPainter|ScenePainter/);
  });

  /**
   * A named board state must be something a renderer actually draws.
   *
   * `BoardState` had six members and three of them drew nothing. `is-done` and
   * `is-attacking` were toggled on every render and every strike, and no stylesheet
   * in any package — shared, per-app, or carried inside a content pack's own art —
   * had a rule for either. `is-hidden` was named in the table and unreachable,
   * because `hidden` is `display: none` and both methods branched before they got
   * there. A GPU backend would have had to implement all six and guess which three
   * mattered.
   *
   * So: whatever the SVG backend spells as a class, some stylesheet must style.
   */
  it('gives every named board state something that draws it', () => {
    const surface = stripComments(
      readFileSync(join(packagesRoot, 'game-ui', 'src', 'art', 'svg-board-surface.ts'), 'utf8'),
    );
    const table = /STATE_CLASS[^=]*=\s*\{([^}]*)\}/.exec(surface);
    expect(table).toBeTruthy();
    const spelled = [...table![1].matchAll(/'([\w-]+)'/g)].map(([, name]) => name);
    // A table this guard failed to read would pass for the wrong reason.
    expect(spelled.length).toBeGreaterThan(1);

    // Every rule this repository ships, wherever it lives: the stylesheets, and the
    // `<style>` a content pack sends along inside its own pictures.
    const packStyles = readdirSync(packagesRoot, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .flatMap((entry) => {
        const presentation = join(packagesRoot, entry.name, 'src', 'presentation');
        if (!statSync(presentation, { throwIfNoEntry: false })?.isDirectory()) return [];
        return readdirSync(presentation)
          .filter((file) => file.endsWith('.ts'))
          .map((file) => readFileSync(join(presentation, file), 'utf8'));
      });
    const rules = [...stylesheets().map((file) => readFileSync(file, 'utf8')), ...packStyles]
      .map(stripComments)
      .join('\n');

    const undrawn = spelled.filter((name) =>
      !new RegExp(`\\.${escapeForRegExp(name)}(?![\\w-])`).test(rules));
    expect(undrawn).toEqual([]);
  });

  /**
   * The battle screen wore the map editor's chrome for as long as they shared it.
   *
   * `topbar`, `panel`, `stage` and `board-scroll` are a title bar above the
   * content and panels beside it — the shape of a tool, and the reason a battle
   * read as a document with a picture in the middle. The editor is a tool and
   * keeps them; nothing else may pick them up, in markup or in a stylesheet,
   * because sharing them is exactly how the game inherited them the first time.
   */
  it('leaves the tool its page furniture', () => {
    const furniture = ['topbar', 'panel', 'stage', 'board-scroll'];
    const owner = `editor${sep}src`;
    const offenders: string[] = [];

    for (const sheet of stylesheets()) {
      const declared = stripComments(readFileSync(sheet, 'utf8'));
      for (const name of furniture) {
        if (!new RegExp(`\\.${escapeForRegExp(name)}(?![\\w-])`).test(declared)) continue;
        if (sheet.includes(owner)) continue;
        offenders.push(`${relative(packagesRoot, sheet)} styles .${name}`);
      }
    }

    for (const file of [...everyPackageSource(), ...appSources()]) {
      if (file.includes(owner)) continue;
      const markup = readFileSync(file, 'utf8');
      for (const name of furniture) {
        // Only a rendered class counts: prose about the old layout is history,
        // and the point of the guard is that nothing wears it. The token has to
        // match whole — `campaign-topbar` is the campaign's own name for its own
        // thing, and `\b` treats the hyphen as the start of a word.
        const token = `(?<![\\w-])${escapeForRegExp(name)}(?![\\w-])`;
        if (new RegExp(`class(?:Name)?\\s*=\\s*['"\`][^'"\`]*${token}`).test(markup)) {
          offenders.push(`${relative(packagesRoot, file)} renders .${name}`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  /**
   * A shared stylesheet knows no content pack.
   *
   * `app.css` — the base sheet the map editor, the engine demo and the menu all load
   * — held twenty-five rules about one campaign's art: its shadows, its colour
   * grades, its image smoothing. Three applications that never draw a single one of
   * them were shipping all of them, and the general layer held the specifics of a
   * game it is not supposed to know. The same defect as the editor drawing with a
   * hard-coded fallback, and as the grid-at-rest rule being written
   * `.candidate-map .layer-grid`.
   *
   * A pack owns its appearance and ships it beside its art. Which pack it is comes
   * from the packages that exist, not from a list here, so a second story cannot be
   * forgotten.
   */
  it('keeps every content pack out of the shared stylesheets', () => {
    const packs = readdirSync(packagesRoot, { withFileTypes: true })
      .filter((entry) => entry.isDirectory() && (entry.name.startsWith('story-') || entry.name.startsWith('content-')))
      .map((entry) => entry.name);
    expect(packs.length).toBeGreaterThan(2);

    // What a pack's own classes are called: whatever its art actually emits.
    // Interpolations are blanked rather than skipping the whole attribute — the
    // first version of this required a class list with no `${}` in it, which is
    // almost none of them, so it collected nothing and passed for that reason.
    const named = /^[a-zA-Z][\w-]*$/;
    const inMarkup = (source: string): string[] =>
      [...source.matchAll(/class="([^"]*)"/g)]
        .flatMap(([, list]) => list.replace(/\$\{[^}]*\}/g, ' ').split(/\s+/))
        .filter((name) => named.test(name));
    /*
     * A class a pack declares as data rather than writing into markup.
     *
     * `boardClass: 'candidate-map'` never appears inside a `class="…"`, so this
     * guard could not see it — and `.battlefield .board.candidate-map .layer-grid`
     * sat in `battle.css` for exactly that reason. Collected separately so the
     * floor below can say that this half found something, rather than counting a
     * total that changes whenever a wrapper is added or removed.
     */
    const asData = (source: string): string[] =>
      [...source.matchAll(/\bboardClass:\s*'([^']*)'/g)]
        .map(([, name]) => name)
        .filter((name) => named.test(name));

    const emitted = new Set<string>();
    const declared = new Set<string>();
    for (const pack of packs) {
      for (const file of runtimeTypeScriptFiles(join(packagesRoot, pack, 'src'))) {
        const source = readFileSync(file, 'utf8');
        for (const name of inMarkup(source)) emitted.add(name);
        for (const name of asData(source)) declared.add(name);
      }
    }
    // Names the general layer emits too are general, whoever else uses them.
    for (const file of runtimeTypeScriptFiles(join(packagesRoot, 'game-ui', 'src'))) {
      for (const name of inMarkup(readFileSync(file, 'utf8'))) emitted.delete(name);
    }
    // Both halves have to have found something, or half the guard is asleep.
    expect(emitted.size).toBeGreaterThan(5);
    expect(declared.size).toBeGreaterThan(0);
    for (const name of declared) emitted.add(name);

    const shared = ['game-ui', 'editor'].flatMap((owner) => {
      const styles = join(packagesRoot, owner, 'src', 'styles');
      if (!statSync(styles, { throwIfNoEntry: false })?.isDirectory()) return [];
      return readdirSync(styles)
        .filter((file) => file.endsWith('.css'))
        .map((file) => [join(styles, file), readFileSync(join(styles, file), 'utf8')] as [string, string]);
    });

    const offenders = shared.flatMap(([file, css]) =>
      [...emitted]
        .filter((name) => new RegExp(`\\.${escapeForRegExp(name)}(?![\\w-])`).test(stripComments(css)))
        .map((name) => `${relative(packagesRoot, file)} styles .${name}`));

    expect(offenders).toEqual([]);
  });

  /**
   * Markup carries no label nobody reads.
   *
   * Three rounds in a row found these by hand: eight labels on every cell of the
   * biggest layer on the board, a `data-tile="x,y"` handle that made 4,131
   * identical tiles into 4,131 distinct pictures, wrapper classes on scene layers,
   * `data-frame-*` attributes serialising a strip's own description. Each one looked
   * like a contract and was not one, and each cost something: a texture cache's
   * hit rate, a node per cell, or a reader's belief that removing it might break
   * something.
   *
   * A class or a data attribute is a contract when a stylesheet selects on it, a
   * query looks it up, or a test asserts it. Anything else is a comment that costs
   * bytes — and a comment is cheaper and says more.
   *
   * The detection has to be careful about one thing: an emission must not count as
   * a read of itself. So `class="…"` and `data-x="…"` are blanked out of the
   * runtime sources before looking for readers, while tests and stylesheets are
   * searched whole, because they only ever read.
   */
  it('emits no class or data attribute that nothing reads', () => {
    const everyFile = (dir: string): string[] => readdirSync(dir, { withFileTypes: true })
      .flatMap((entry) => {
        if (['node_modules', 'dist', 'assets', '.git'].includes(entry.name)) return [];
        const path = join(dir, entry.name);
        if (entry.isDirectory()) return everyFile(path);
        return /\.(ts|css|html)$/.test(entry.name) ? [path] : [];
      });
    const repoRoot = join(packagesRoot, '..');
    const files = ['packages', 'apps', 'tools'].flatMap((base) => everyFile(join(repoRoot, base)));
    const emitters = new Set([...everyPackageSource(), ...appSources()]);

    /*
     * A label is written two ways, and both count.
     *
     * `class="unit"` inside a markup string, and `svg('g', { class: 'unit' })`
     * through the element helper. Collecting only the first left the renderer's own
     * structural handles out of this guard entirely — and `data-scene-layout`, whose
     * last reader was a stylesheet rule deleted the round before, went unnoticed
     * because the object key that emits it looks exactly like a quoted read.
     */
    const CLASS_IN_MARKUP = /class="([^"]*)"/g;
    const CLASS_AS_KEY = /\bclass:\s*'([^']*)'/g;
    const DATA_IN_MARKUP = /(data-[a-z][\w-]*)\s*=/g;
    const DATA_AS_KEY = /'(data-[a-z][\w-]*)'\s*:/g;

    const classes = new Map<string, string[]>();
    const attributes = new Map<string, string[]>();
    for (const file of emitters) {
      const source = stripComments(readFileSync(file, 'utf8'));
      for (const pattern of [CLASS_IN_MARKUP, CLASS_AS_KEY]) {
        for (const [, list] of source.matchAll(pattern)) {
          // Interpolations are blanked, and a name left ending in `-` is half a name.
          for (const name of list.replace(/\$\{[^}]*\}/g, ' ').split(/\s+/)) {
            if (/^[a-zA-Z][\w-]*\w$/.test(name)) classes.set(name, [...classes.get(name) ?? [], file]);
          }
        }
      }
      for (const pattern of [DATA_IN_MARKUP, DATA_AS_KEY]) {
        for (const [, name] of source.matchAll(pattern)) {
          attributes.set(name, [...attributes.get(name) ?? [], file]);
        }
      }
    }

    // Comments come out of *every* file, not only the emitters. The paragraph above
    // names `data-tile` and `.sprite-raster` as labels that were removed, and with
    // this file's own prose in the haystack the guard read that as evidence they are
    // still read — passing while both were emitted again.
    const haystack = files.map((file) => {
      const source = stripComments(readFileSync(file, 'utf8'));
      if (!emitters.has(file)) return source;
      const blank = (match: string) => ' '.repeat(match.length);
      return source
        .replace(CLASS_IN_MARKUP, blank)
        .replace(CLASS_AS_KEY, blank)
        .replace(DATA_AS_KEY, blank)
        // An attribute *selector* keeps its brackets, and is a read.
        .replace(/(?<!\[)\bdata-[a-z][\w-]*="[^"]*"/g, blank);
    });

    const camel = (name: string) => name.slice('data-'.length).replace(/-(\w)/g, (_, c) => c.toUpperCase());
    const isRead = (name: string, kind: 'class' | 'data'): boolean => {
      const quoted = `${escapeForRegExp(name)}(?![\\w-])`;
      const patterns = kind === 'class'
        ? [`\\.${quoted}`, `['"\`]${quoted}`]
        : [`\\[${escapeForRegExp(name)}`, `['"\`]${quoted}`, `\\.${quoted}`, `dataset\\.${camel(name)}(?![\\w])`];
      return haystack.some((source) => patterns.some((pattern) => new RegExp(pattern).test(source)));
    };

    // A guard that collected nothing would pass for the wrong reason.
    expect(classes.size).toBeGreaterThan(100);
    expect(attributes.size).toBeGreaterThan(5);

    const unread = [
      ...[...classes].filter(([name]) => !isRead(name, 'class'))
        .map(([name, where]) => `.${name} (${relative(packagesRoot, where[0])})`),
      ...[...attributes].filter(([name]) => !isRead(name, 'data'))
        .map(([name, where]) => `${name} (${relative(packagesRoot, where[0])})`),
    ];

    expect(unread).toEqual([]);
  });

  /**
   * A shell that stages a battle dresses it.
   *
   * The HUD's stylesheet is its own file so the editor cannot inherit it, which
   * means an application root now has to ask for it — and a root that mounts a
   * battle without it renders an unstyled pile of panels down the left edge, a
   * failure no type and no test would otherwise notice.
   */
  it('imports the battle stylesheet wherever a battle is mounted', () => {
    const shells = appSources().filter((file) => /new GameController\(|StoryCampaignController\(/.test(
      stripComments(readFileSync(file, 'utf8')),
    ));

    expect(shells.length).toBeGreaterThan(1);
    const undressed = shells.filter((file) =>
      !readFileSync(file, 'utf8').includes("@empire/game-ui/styles/battle.css"));

    expect(undressed.map((file) => relative(packagesRoot, file))).toEqual([]);
  });
});

describe('one call shape', () => {
  /** Balanced-paren parameter list, so `(event: GameEvent) => void` survives. */
  function parametersOf(source: string, from: number): string[] {
    const open = source.indexOf('(', from);
    let depth = 0;
    for (let index = open; index < source.length; index++) {
      if (source[index] === '(') depth++;
      else if (source[index] === ')' && --depth === 0) {
        const inner = source.slice(open + 1, index);
        const parts: string[] = [];
        let nesting = 0;
        let current = '';
        let previous = '';
        for (const character of inner) {
          // `=>` is not a closing angle bracket; counting it as one made
          // `Record<string, number>` split down the middle.
          if ('([{<'.includes(character)) nesting++;
          else if (')]}>'.includes(character) && !(character === '>' && previous === '=')) nesting--;
          previous = character;
          if (character === ',' && nesting === 0) {
            parts.push(current.trim());
            current = '';
          } else current += character;
        }
        if (current.trim()) parts.push(current.trim());
        return parts;
      }
    }
    return [];
  }

  it('takes dependencies first, as one named port, and the event channel last', () => {
    // One rule, not two: what it needs, what it acts on, where it reports.
    // "single content trailing, several services leading" was two rules, and
    // seventeen of the forty-two emitting functions had drifted between them.
    //
    // This guard used to be scoped to functions that emit — "pure queries are
    // left alone" — and that carve-out was hiding fifteen queries still written
    // the old way, plus five taking two or three services as separate
    // parameters. A query is not a different call shape from a command, so the
    // scope is every function in every package that names a service.
    //
    // "Exported" was the next carve-out to go, and it was hiding nineteen: the
    // module-private helpers were consistently written the *other* way round,
    // including one taking `(source, id, done, content)` — a bare boolean with
    // the catalog behind it. A reader has the same problem either way, and the
    // axiom says nothing about visibility.
    const dependencies = [
      'content', 'rules', 'resources', 'progression', 'policy', 'space', 'handlers', 'random',
      'grids', 'objectives', 'scenarioEffects', 'scenarioConditions', 'saves', 'turnOrders',
      'reactions', 'directives', 'areaShapes', 'abilities', 'hitEffects', 'statusBehaviors',
      'combatModifiers', 'referenceChecks', 'unitDepartures', 'advisors', 'dispatch', 'planning',
      'engine', 'art', 'presentation', 'layout', 'canvas', 'session',
    ];
    const offenders: string[] = [];
    for (const file of everyPackageSource()) {
      // Comments go first: this reads private helpers now, and a paragraph
      // containing "a function that has refused to be named" is prose.
      const source = stripComments(readFileSync(file, 'utf8'));
      for (const match of source.matchAll(/(?:export )?function (\w+)/g)) {
        const names = parametersOf(source, match.index + match[0].length)
          .map((parameter) => parameter.split(':')[0].trim().replace(/[?=].*$/, '').trim());
        const services = names.filter((name) => dependencies.includes(name));
        if (services.length === 0) continue;
        if (services.length > 1) {
          offenders.push(`${relative(packagesRoot, file)}#${match[1]}: ${services.join(' + ')} should be one port`);
          continue;
        }
        if (!names.includes('emit')) {
          const dependencyAt = names.findIndex((name) => dependencies.includes(name));
          const subjectAt = names.findIndex((name) => !dependencies.includes(name));
          if (subjectAt !== -1 && dependencyAt > subjectAt) {
            offenders.push(`${relative(packagesRoot, file)}#${match[1]}: ${names[dependencyAt]} follows ${names[subjectAt]}`);
          }
          continue;
        }
        const lastDependency = names.reduce(
          (last, name, index) => (dependencies.includes(name) ? index : last), -1);
        const firstSubject = names.findIndex((name) => !dependencies.includes(name));
        if (names[names.length - 1] === 'emit' && lastDependency < firstSubject) continue;

        const problems: string[] = [];
        if (lastDependency > firstSubject) problems.push(`${names[lastDependency]} follows ${names[firstSubject]}`);
        // Nothing may follow `emit`. This used to allow "a trailing optional (a
        // source id, a metadata bag)", and by the time anyone looked the
        // allowance was carrying four functions, two of them a *required*
        // `scope` — so the exception had stopped describing what it permitted.
        //
        // The axiom is unconditional and the fix is small: a scope is a subject
        // and goes before the channel, and where the trailer really was part of
        // the description it became one named subject. `addStatus(content, unit,
        // { id, remaining, sourceUnitId }, emit)` also stopped putting two bare
        // numbers next to each other at the call site.
        const afterEmit = names.slice(names.indexOf('emit') + 1);
        if (afterEmit.length > 0) problems.push(`${afterEmit.join(', ')} follows emit`);
        if (problems.length > 0) offenders.push(`${relative(packagesRoot, file)}#${match[1]}: ${problems.join('; ')}`);
      }
    }

    expect(offenders).toEqual([]);
  });

  it('never passes a bare boolean at a call site', () => {
    // `advanceInitiative(state, context, true)`, `march(action, false)`,
    // `levelCard(level, true)`, `createUnitState(source, id, false, content)`:
    // in every one of them the reader has to open the callee to learn what the
    // argument means, and in the last one it was sitting in front of the
    // dependency as well.
    //
    // A boolean that changes what a function *does* is part of the subject, so
    // it gets a name: `{ spendActive: true }`, `{ follow: false }`. Same rule as
    // the trailing-optional one it replaced — a parameter list is read at the
    // call site or it is not read at all.
    //
    // Only our own calls: undotted, or on `this`. A DOM method that takes a
    // positional flag is not ours to redesign.
    const offenders: string[] = [];
    for (const file of [...everyPackageSource(), ...appSources()]) {
      const source = stripStrings(stripComments(readFileSync(file, 'utf8')));
      source.split('\n').forEach((line, index) => {
        for (const call of line.matchAll(/(?<![.\w])((?:this\.)?[a-z][A-Za-z0-9]*)\(([^()]*)\)/g)) {
          const args = call[2].split(',').map((argument) => argument.trim());
          if (args.some((argument) => argument === 'true' || argument === 'false')) {
            offenders.push(`${relative(packagesRoot, file)}:${index + 1} ${call[1]}(${call[2].trim()})`);
          }
        }
      });
    }

    expect(offenders).toEqual([]);
  });

  it('never puts a required parameter after an optional one', () => {
    // `f(state, unit, from, weapon = undefined, content)` compiles, and then
    // every caller in the codebase writes `undefined` to reach past it. If the
    // argument is not optional at the call site it is not optional.
    const declaration = (parameter: string) => parameter.replace(/\/\*[\s\S]*?\*\//g, '').trim();
    const optional = (parameter: string) => {
      const text = declaration(parameter);
      const name = text.split(':')[0].trim();
      return name.endsWith('?') || text.replace(/=>/g, '').includes('=');
    };

    const offenders: string[] = [];
    for (const file of runtimeTypeScriptFiles(coreRoot)) {
      const source = stripComments(readFileSync(file, 'utf8'));
      for (const match of source.matchAll(/(?:export )?function (\w+)/g)) {
        const parameters = parametersOf(source, match.index + match[0].length);
        const first = parameters.findIndex(optional);
        if (first < 0) continue;
        for (const parameter of parameters.slice(first + 1).filter((candidate) => !optional(candidate))) {
          const name = declaration(parameter).split(':')[0].trim();
          offenders.push(`${relative(coreRoot, file)}#${match[1]}: ${name} is required but follows an optional`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });
});

/**
 * One question, one answer.
 *
 * "There should be one obvious canonical way to express each semantic
 * operation", and "the same question answered in two places will diverge the
 * moment a second kind of play arrives". Both of those had already happened here,
 * repeatedly, and in the one shape a reviewer never notices: a four-line helper
 * copied into the module that needed it.
 */
describe('one answer per question', () => {
  /**
   * Top-level declarations and their text, keyed by the name being declared.
   *
   * A chunk runs from the declaration line through the indented lines under it —
   * which is a body, or a multi-line signature and then a body. Classes are
   * deliberately not read: a class's name *is* its meaning, and three one-line
   * `extends Error` subclasses are three different errors, not one copied thrice.
   */
  function topLevelDeclarations(source: string): Array<{ name: string; text: string }> {
    const lines = source.split('\n');
    const found: Array<{ name: string; text: string }> = [];
    for (let index = 0; index < lines.length;) {
      const declared = /^(?:export )?(?:const|function|type|interface) (\w+)/.exec(lines[index]);
      if (!declared) {
        index++;
        continue;
      }
      const start = index++;
      while (index < lines.length && (lines[index].startsWith(' ') || /^[)\]}]/.test(lines[index]))) index++;
      found.push({ name: declared[1], text: lines.slice(start, index).join('\n') });
    }
    return found;
  }

  it('declares each helper in exactly one module', () => {
    // Twelve declarations of three functions in one directory, and it was not the
    // first time: `escapeAttr` carries a comment saying it was gathered from
    // three private copies, and a fourth copy had appeared beside it since.
    //
    // What was found, all byte-identical: the shape-check vocabulary of the two
    // save formats (which had already diverged in naming — `orNull` in one,
    // `nullable` in the other), `cloneAccounts` twice inside the engine, the
    // FNV-1a name hash four times under three names, `pick` four times, `r2`
    // four times, `definitionKey` twice, `attr`/`escapeAttr` twice, and the whole
    // board harness in three of the four rulers.
    //
    // The declared name is blanked before comparing, because a copy that was
    // renamed is still a copy: `idHash`, `nameHash` and a nested `hash` were one
    // function under three names.
    // A rule port is exempt, and the axiom is the reason: "Ports are declared by
    // the consumer that needs them, and `BattleRuleServices` satisfies them
    // structurally. Needing a new rule must not add a module edge." Five sets of
    // them are identical on purpose — `MovementRules` and `VisionRules` both want
    // the grid and the catalog and neither may import the other's module. They
    // are the interfaces whose name ends in `Rules`, which is the convention that
    // says so.
    const rulePort = /^(?:export )?interface \w+Rules\b/;
    const seen = new Map<string, string[]>();
    for (const file of [...everyPackageSource(), ...appSources(), ...toolSources()]) {
      const source = stripComments(readFileSync(file, 'utf8'));
      for (const { name, text } of topLevelDeclarations(source)) {
        if (rulePort.test(text)) continue;
        // Modulo the declared name and whether it is exported: a copy that was
        // renamed on the way in, or published in one module and kept private in
        // the other, is still a copy. Not modulo parameter names — that would be
        // alpha-renaming, and every duplicate found here was a paste.
        const body = text.replace(/^export /, '').split(name).join('@').replace(/\s+/g, ' ').trim();
        // Short enough to be a coincidence rather than a copy. `{ readonly width:
        // number; readonly height: number }` is a grid's extent in tiles and a
        // scene's box in scene units, and merging those would be the error.
        if (body.length < 80) continue;
        const where = `${relative(packagesRoot, file)}#${name}`;
        seen.set(body, [...(seen.get(body) ?? []), where]);
      }
    }

    // A run that parsed nothing would pass by finding no pairs.
    expect(seen.size).toBeGreaterThan(400);
    const offenders = [...seen.values()]
      .filter((places) => new Set(places.map((place) => place.split('#')[0])).size > 1)
      .map((places) => places.join(' == '));

    expect(offenders).toEqual([]);
  });

  it('reads every field of an art port through that port', () => {
    // Four fields of `ArtProvider` were assigned by the campaign and read by
    // nothing: `effectMarkup`, `structureMarkup`, `markerMarkup`, `weaponFx`.
    // Three were shadows of live `BattlePresentation` fields — a structure, a
    // mark on the ground and a weapon's effect are things a *scene* draws — and
    // two of the three were assigned `() => null`, a provider saying nothing
    // through a field nobody was listening to.
    //
    // Two checks, because the two failures look different. A field nothing reads
    // is found by looking for any read of it at all; a field that *is* read, but
    // on the other port, is found by the two ports sharing a name.
    const artRoot = join(packagesRoot, 'game-ui', 'src', 'art');
    const ports = [
      { file: join(artRoot, 'ports.ts'), name: 'ArtProvider' },
      { file: join(artRoot, 'battle-presentation.ts'), name: 'BattlePresentation' },
    ];
    const sources = [...everyPackageSource(), ...appSources()]
      .map((file) => stripComments(readFileSync(file, 'utf8')))
      .join('\n');

    const declared = new Map<string, string[]>();
    const offenders: string[] = [];
    for (const port of ports) {
      const body = new RegExp(String.raw`export interface ${port.name} \{([\s\S]*?)\n\}`)
        .exec(stripComments(readFileSync(port.file, 'utf8')));
      if (!body) throw new Error(`no ${port.name} interface in ${port.file}`);
      const members = [...body[1].matchAll(/^ {2}(?:readonly )?(\w+)\??[(:<]/gm)].map(([, name]) => name);
      declared.set(port.name, members);
      for (const member of members) {
        // A read, not an assignment: `structureMarkup: () => null` is the
        // campaign answering a question, and `.structureMarkup` is somebody
        // asking it. Only the second one makes the field load-bearing.
        if (!new RegExp(String.raw`\.${member}\b`).test(sources)) {
          offenders.push(`${port.name}.${member} is assigned but never asked for`);
        }
      }
    }

    // `id` is on both by design: identity is not a capability. Everything the
    // application root composes by name has one, and `ArtDirection` refuses two
    // entries under the same name in either list.
    for (const member of declared.get('ArtProvider') ?? []) {
      if (member !== 'id' && declared.get('BattlePresentation')?.includes(member)) {
        offenders.push(`${member} is declared by both ports; one of them is a shadow`);
      }
    }

    // Both ports parsed, or this passes by having read no members.
    expect((declared.get('ArtProvider') ?? []).length).toBeGreaterThan(8);
    expect((declared.get('BattlePresentation') ?? []).length).toBeGreaterThan(10);
    expect(offenders).toEqual([]);
  });

  it('reads a gauge as a fraction of itself in one place', () => {
    // `hp / maxHp` and `morale.current / morale.maximum` were written out in
    // twelve places with four behaviours: seven divided plainly, one guarded a
    // maximum of zero, two clamped to 0..1, and one did both. The disagreement
    // had reached the screen — a structure with `maxHp: 0` drew a full condition
    // bar under the generic art and a `NaN`-wide one under the campaign's.
    //
    // The tell is the *left* operand: a gauge's own reading. `damage / maxHp` and
    // `healed / def.maxHp` are a different question — how big was this compared
    // to the target — and morale damage is scaled by exactly that, so clamping
    // there would silently cap the blow. Which is why no exemption list is
    // needed: those never divide *from* `.hp` or `.current`.
    const owner = join(coreRoot, 'vitals.ts');
    const offenders: string[] = [];
    for (const file of [...everyPackageSource(), ...appSources(), ...toolSources()]) {
      if (file === owner) continue;
      const source = stripStrings(stripComments(readFileSync(file, 'utf8')));
      source.split('\n').forEach((line, index) => {
        if (/\.(?:hp|current)\s*\/(?!\/)/.test(line)) {
          offenders.push(`${relative(packagesRoot, file)}:${index + 1} ${line.trim()}`);
        }
      });
    }

    // The owner is where the division lives, so it has to contain one.
    expect(readFileSync(owner, 'utf8')).toContain('current / maximum');
    expect(offenders).toEqual([]);
  });

  it('decides what colour a gauge is in one place', () => {
    // Four bars showed how full something was, and they disagreed. The HUD's and
    // the unit's coloured three bands at 0.6 and 0.3; the generic structure bar
    // and the campaign's coloured two at 0.5, so a structure at four tenths was
    // red while a unit beside it at four tenths was amber. The two structure bars
    // also sat 0.6 units higher and 0.6 thinner than the unit bar, and the
    // campaign's wrote three colours that appear nowhere in the palette.
    //
    // A bar over a cell is read as a measurement, not looked at as a drawing, so
    // two of them on one board have to agree. The palette entries are the tell:
    // whoever reads `hpGood` is deciding the band.
    const artRoot = join(packagesRoot, 'game-ui', 'src', 'art');
    const owner = join(artRoot, 'gauges.ts');
    const palette = join(artRoot, 'palette.ts');
    const offenders: string[] = [];
    for (const file of [...everyPackageSource(), ...appSources()]) {
      if (file === owner || file === palette) continue;
      const source = stripComments(readFileSync(file, 'utf8'));
      if (/\bhp(?:Good|Mid|Low)\b/.test(source)) offenders.push(relative(packagesRoot, file));
    }

    // The owner reads all three, or the tell has moved and this guards nothing.
    for (const band of ['hpGood', 'hpMid', 'hpLow']) {
      expect(readFileSync(owner, 'utf8')).toContain(band);
    }
    expect(offenders).toEqual([]);
  });

  it('turns something thrown into a line for a person in one place', () => {
    // A `throw` may carry any value, and a rejected browser API often carries a
    // `DOMException` or a bare string. Four shells answered this differently: the
    // editor had a private `errorMessage`, two places wrote the ternary out, and
    // two wrote `String(cause)` — which prefixes the class name, so the player
    // was told 「无法开始战役：Error: …」 in two places and given a clean sentence
    // everywhere else.
    //
    // The tell is the narrowing, not the helper's name: `instanceof Error` is
    // what you write when you are about to decide this for yourself.
    const owner = join(coreRoot, 'domain', 'errors.ts');
    const offenders: string[] = [];
    for (const file of [...everyPackageSource(), ...appSources(), ...toolSources()]) {
      if (file === owner) continue;
      const source = stripComments(readFileSync(file, 'utf8'));
      if (/instanceof Error\b/.test(source)) offenders.push(relative(packagesRoot, file));
    }

    expect(stripComments(readFileSync(owner, 'utf8'))).toContain('instanceof Error');
    expect(offenders).toEqual([]);
  });

  it('declares no stylesheet variable that nothing reads', () => {
    // The other direction of "emits no class that nothing reads". Four custom
    // properties were declared and read nowhere — three of them the first three
    // lines of `.campaign-root`, where a reader would take them for the campaign's
    // palette. A dead colour in a stylesheet looks exactly like a live one.
    //
    // `var(--x)` is the only way to read one from CSS, and a `style="--x:…"`
    // attribute is the only way from a template, so this needs no exemptions.
    const styleRoots = [
      join(packagesRoot, 'game-ui', 'src', 'styles'),
      join(packagesRoot, 'editor', 'src', 'styles'),
      join(packagesRoot, 'story-candidate-01', 'src', 'styles'),
    ];
    const stylesheets = styleRoots.flatMap((root) =>
      readdirSync(root).filter((entry) => entry.endsWith('.css')).map((entry) => join(root, entry)));
    const css = stylesheets
      .map((file) => readFileSync(file, 'utf8').replace(/\/\*[\s\S]*?\*\//g, ''))
      .join('\n');
    const templates = [...everyPackageSource(), ...appSources()]
      .map((file) => readFileSync(file, 'utf8'))
      .join('\n');

    // Both halves found something, or one of them is looking in the wrong place.
    const declared = [...new Set([...css.matchAll(/(--[\w-]+)\s*:/g)].map(([, name]) => name))];
    const read = new Set([
      ...[...css.matchAll(/var\((--[\w-]+)/g)].map(([, name]) => name),
      ...[...templates.matchAll(/(--[\w-]+)\s*:/g)].map(([, name]) => name),
    ]);
    expect(declared.length).toBeGreaterThan(20);
    expect(read.size).toBeGreaterThan(20);

    expect(declared.filter((name) => !read.has(name))).toEqual([]);
  });

  it('selects nothing the code never emits', () => {
    // The other direction again, for rules rather than variables. A stylesheet is
    // the one place where deleting the code that used a thing leaves the thing
    // behind and nothing complains: a dead rule reads exactly like a live one.
    //
    // Two class families reach the DOM through an interpolation rather than as a
    // literal, and both are written over an enumeration — `hud-${region}` over
    // `HUD_REGIONS` and `layer-${name}` over `BOARD_LAYERS`. So they are expanded
    // from those lists rather than waved through by prefix: `.hud-ghost` is an
    // offender, and a third interpolated family fails here until it is taught.
    const styleRoots = ['game-ui', 'editor', 'story-candidate-01']
      .map((pkg) => join(packagesRoot, pkg, 'src', 'styles'));
    const css = styleRoots
      .flatMap((root) => readdirSync(root).filter((entry) => entry.endsWith('.css'))
        .map((entry) => readFileSync(join(root, entry), 'utf8')))
      .join('\n')
      .replace(/\/\*[\s\S]*?\*\//g, '')
      // A background image's file name is not a class.
      .replace(/url\([^)]*\)/g, 'url()');
    const emitted = [...everyPackageSource(), ...appSources()]
      .map((file) => readFileSync(file, 'utf8'))
      .join('\n');

    const uiRoot = join(packagesRoot, 'game-ui', 'src');
    const listed = (file: string, declaration: RegExp, member: RegExp): string[] => {
      const block = declaration.exec(readFileSync(file, 'utf8'));
      if (!block) throw new Error(`no ${String(declaration)} in ${file}`);
      return [...block[1].matchAll(member)].map(([, name]) => name);
    };
    const interpolated = [
      ...listed(join(uiRoot, 'ui', 'hud.ts'), /const HUD_REGIONS = \{([\s\S]*?)\n\}/, /^ {2}(\w+):/gm)
        .map((region) => `hud-${region}`),
      ...listed(join(uiRoot, 'art', 'board-surface.ts'), /const BOARD_LAYERS = \[([\s\S]*?)\n\]/, /'(\w+)'/g)
        .map((layer) => `layer-${layer}`),
    ];
    // Both enumerations parsed, or every one of their classes reads as dead.
    expect(interpolated.length).toBeGreaterThan(12);

    const classes = [...new Set([...css.matchAll(/\.(-?[A-Za-z_][\w-]*)/g)].map(([, name]) => name))];
    const offenders = classes
      .filter((name) => !emitted.includes(name))
      .filter((name) => !interpolated.includes(name));

    // A data attribute has a second legitimate spelling: the DOM's camel case.
    const camel = (attribute: string) =>
      `dataset.${attribute.slice(5).replace(/-([a-z])/g, (_, letter: string) => letter.toUpperCase())}`;
    for (const [, attribute] of css.matchAll(/\[(data-[\w-]+)/g)) {
      if (!emitted.includes(attribute) && !emitted.includes(camel(attribute))) {
        offenders.push(attribute);
      }
    }

    expect(classes.length).toBeGreaterThan(200);
    expect(offenders).toEqual([]);
  });

  it('declares every workspace package it imports', () => {
    // "Dependencies belong in declarations" — and a workspace is exactly where
    // that stops being self-enforcing. npm links every package into one
    // `node_modules`, so an import of a sibling resolves whether the manifest
    // mentions it or not: `game-ui`'s tests reached for four packs and its
    // manifest named none of them, and `battle-engine` had no dependency block at
    // all while its tests and its benchmark composed three content packs.
    //
    // Only `@empire/*`, and that is the reason rather than a convenience: an
    // undeclared third-party import is not installed and fails loudly, while the
    // toolchain — vitest, jsdom, vite — is declared once at the root on purpose.
    const manifests = [
      ...readdirSync(packagesRoot, { withFileTypes: true })
        .filter((entry) => entry.isDirectory())
        .map((entry) => join(packagesRoot, entry.name)),
      ...readdirSync(join(packagesRoot, '..', 'apps'), { withFileTypes: true })
        .filter((entry) => entry.isDirectory())
        .map((entry) => join(packagesRoot, '..', 'apps', entry.name)),
    ].filter((root) => statSync(join(root, 'package.json'), { throwIfNoEntry: false })?.isFile());

    const workspaceImports = (files: string[]): Set<string> => {
      const found = new Set<string>();
      for (const file of files) {
        const source = stripComments(readFileSync(file, 'utf8'));
        // `from '…'` rather than a whole import statement: a named import list
        // spans lines, and this file quotes package names in arrays, so matching
        // the specifier's own keyword is both simpler and narrower than matching
        // the statement — a guard that counted bare quotes would report itself.
        for (const pattern of [
          /\bfrom\s+'(@empire\/[\w-]+)/g,
          /^\s*import\s+'(@empire\/[\w-]+)/gm,
          /\bimport\('(@empire\/[\w-]+)/g,
        ]) {
          for (const [, name] of source.matchAll(pattern)) found.add(name);
        }
      }
      return found;
    };

    const offenders: string[] = [];
    let checked = 0;
    for (const root of manifests) {
      const manifest = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8')) as {
        name: string;
        dependencies?: Record<string, string>;
        devDependencies?: Record<string, string>;
      };
      const sources = join(root, 'src');
      if (!statSync(sources, { throwIfNoEntry: false })?.isDirectory()) continue;
      const everything = allTypeScriptFiles(sources);
      const runtime = new Set(runtimeTypeScriptFiles(sources));
      const atRuntime = workspaceImports(everything.filter((file) => runtime.has(file)));
      const anywhere = workspaceImports(everything);
      const declared = new Set(Object.keys(manifest.dependencies ?? {}));
      const forTests = new Set([...declared, ...Object.keys(manifest.devDependencies ?? {})]);
      checked++;

      if (anywhere.has(manifest.name)) {
        offenders.push(`${manifest.name} imports itself by package name`);
      }
      for (const name of [...atRuntime].sort()) {
        if (name !== manifest.name && !declared.has(name)) {
          offenders.push(`${manifest.name}: runtime imports ${name}, which is not a dependency`);
        }
      }
      for (const name of [...anywhere].sort()) {
        if (name !== manifest.name && !forTests.has(name)) {
          offenders.push(`${manifest.name}: tests import ${name}, which is declared nowhere`);
        }
      }
      for (const name of [...forTests].sort()) {
        if (name.startsWith('@empire/') && !anywhere.has(name)) {
          offenders.push(`${manifest.name}: declares ${name}, which nothing imports`);
        }
      }
    }

    // Every workspace member with sources, or this passes by having read none.
    expect(checked).toBeGreaterThanOrEqual(14);
    expect(offenders).toEqual([]);
  });

  it('launders a type only where a keyed registry dispatches', () => {
    // `x!` is forbidden everywhere in this repository and `x as never` was allowed
    // everywhere, and the two are the same move: telling the compiler to stop
    // asking. The difference is that one of them has a legitimate home.
    //
    // A registry stores `Handler<Kind>` and is registered with
    // `Handler<'destroy'>`, which is not assignable to it — a handler is
    // contravariant in its payload. The key correlation that makes the pair sound
    // is the one thing TypeScript cannot express through a lookup, so the cast at
    // the dispatch is paying for the cast at the registration. Saying it properly
    // needs a mapped-type trick, which is the clever metaprogramming this
    // repository would rather not read.
    //
    // So it stays, and it stays *here*: in the modules that own an extension
    // point. Anywhere else it is silencing a real type error, and one of them was
    // — a ruler handed `{ width, height }` to `idx` through `as never`, and `idx`
    // takes `{ width: number }`, so the cast was paying for nothing at all.
    const owners = [
      'battle-engine/src/resources.ts',
      'battle-engine/src/scenario.ts',
      'battle-engine/src/action-system.ts',
      'battle-engine/src/ai-objectives.ts',
      'battle-engine/src/objective-system.ts',
      'battle-engine/src/hit-effects.ts',
      'battle-engine/src/plugins/default.ts',
      'campaign-engine/src/nodes.ts',
      'campaign-engine/src/rules.ts',
      'game-ui/src/ui/event-presentation.ts',
    ];
    const offenders = [...everyPackageSource(), ...appSources(), ...toolSources()].flatMap((file) => {
      const name = relative(packagesRoot, file);
      if (owners.includes(name)) return [];
      const code = stripComments(readFileSync(file, 'utf8'));
      return / as never\b/.test(code) ? [name] : [];
    });

    expect(offenders).toEqual([]);
  });

  it('decides how dark the ground goes in one place', () => {
    // Thirteen ground shadows were written out by hand across four art modules,
    // every one an ellipse filled `PAL.ink`, at seven different opacities between
    // 0.12 and 0.34. Nobody chose 0.22 over 0.24 for a reason — it is one idiom
    // typed thirteen times with the strength jittered, and the jitter is what made
    // it look like a decision. Three named weights now, and the *size* stays the
    // caller's because that is real information: a keep's footing is not a
    // soldier's.
    //
    // The tell is a shadow's two halves together — the ink and a transparency.
    // A portrait's eye is also an ink ellipse and is opaque, so it is not caught
    // by accident and needs no exemption.
    const owner = join(packagesRoot, 'game-ui', 'src', 'art', 'shading.ts');
    const offenders = everyPackageSource().flatMap((file) => {
      if (file === owner) return [];
      const code = stripComments(readFileSync(file, 'utf8'));
      return /<ellipse[^>]*fill="\$\{PAL\.ink\}"[^>]*opacity=/.test(code)
        ? [relative(packagesRoot, file)]
        : [];
    });

    expect(readFileSync(owner, 'utf8')).toContain('WEIGHT_ALPHA');
    expect(offenders).toEqual([]);
  });
});
