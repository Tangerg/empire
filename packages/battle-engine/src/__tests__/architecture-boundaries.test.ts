import { describe, expect, it } from 'vitest';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative, sep } from 'node:path';

const coreRoot = join(import.meta.dirname, '..');
const packagesRoot = join(coreRoot, '..', '..');

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
 * Source with its string and template literals blanked out.
 *
 * Markup and prose are data here — the UI packages are mostly template literals
 * — and a guard about *code* that reads what the code prints is guarding the
 * wrong text.
 */
function stripStrings(source: string): string {
  return source
    .replace(/`(?:[^`\\]|\\.)*`/g, '``')
    .replace(/'(?:[^'\\\n]|\\.)*'/g, "''")
    .replace(/"(?:[^"\\\n]|\\.)*"/g, '""');
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
      'UnitDepartureHandlers', 'WeaponAreaShapes', 'UnitDirectives', 'WeaponHitEffectHandlers',
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
      'learnedAbilities', 'meta',
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
    // Scoped to the battle engine by reason, not by importance: this is where
    // the contract is declared and where `tryDispatch` and the two shell catches
    // consume it. `RangeError` stays allowed; it names a category of its own.
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) =>
      /throw new Error\(/.test(stripComments(readFileSync(file, 'utf8')))
        ? [relative(coreRoot, file)]
        : []);

    expect(offenders).toEqual([]);
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
    const allowed = ['damage.ts', 'unit-departure.ts', join('domain', 'battle-aggregate.ts')];
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
    const offenders = everyPackageSource().flatMap((file) => {
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
    const owners = ['battle-engine/src/tactical-grid.ts', 'game-ui/src/art/board-decorations.ts'];
    const offenders = everyPackageSource().flatMap((file) => {
      const name = relative(packagesRoot, file);
      if (owners.includes(name)) return [];
      const code = stripComments(readFileSync(file, 'utf8'));
      return /'north'[\s\S]{0,80}'(?:east|south|west)'/.test(code) ? [name] : [];
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
      'scenario.ts',
      'objective-system.ts',
      'types.ts',
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
      // A ladder, not a bag: the key is the version a step migrates *from*, the
      // entry is a bare function that cannot answer for its own key, and `load`
      // walks the rungs refusing gaps. Wrapping each step in an object purely to
      // satisfy `keyOf` would buy the shape and lose the meaning.
      'battle-engine/src/save-schema.ts',
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
          // inside a method body says nothing about the class it sits in.
          /\n[ \t]+(?:override )?(?:public |private |protected )?(?:register|define|add)\w*\([^)]*\)\s*[:{]/.test(body) &&
          /\b(?:get|tryGet|forHotkey|lookup)\w*\(/.test(body))
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
        // A trailing optional (a source id, a metadata bag) may follow `emit`;
        // another dependency may not.
        const afterEmit = names.slice(names.indexOf('emit') + 1);
        if (afterEmit.some((name) => dependencies.includes(name))) problems.push('dependency after emit');
        if (problems.length > 0) offenders.push(`${relative(packagesRoot, file)}#${match[1]}: ${problems.join('; ')}`);
      }
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
