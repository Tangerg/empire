import { describe, expect, it } from 'vitest';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';

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
  it('never defaults a dependency parameter to a global singleton', () => {
    const globals = [
      'TEST_CONTENT',
      'DefaultBattleResources',
      'DefaultCombatModifierPipeline',
      'DefaultBattleRuleServices',
      'CoreTacticalSpace',
      'StatusBehaviors',
      'ObjectiveHandlers',
      'DefaultRankProgression',
      'WeaponHitEffectHandlers',
      'ScenarioConditionHandlers',
      'ScenarioEffectHandlers',
      'DefaultAiObjectiveAdvisors',
      'DefaultAbilityAiEvaluators',
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

  it('has removed every ambient content entry point', () => {
    const forbidden = [
      'GlobalContentCatalog',
      'GlobalContentPacks',
      'installContentPacks',
      'DefaultBattleRuleServices',
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

describe('behaviour has an owner', () => {
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

  it('never relabels a caught error as a refused order', () => {
    // Wrapping a collaborator in `try/catch` and calling `fail(error.message)`
    // presents genuine defects to the player as "that move is not allowed".
    // Collaborators raise `IllegalActionError` themselves instead.
    const pattern = /catch\s*\([^)]*\)\s*\{[^}]*\bfail\(/;
    const offenders = runtimeTypeScriptFiles(coreRoot).flatMap((file) =>
      pattern.test(readFileSync(file, 'utf8')) ? [relative(coreRoot, file)] : []);

    expect(offenders).toEqual([]);
  });

  it('never lets a caught error decide a rule', () => {
    // Sibling of the guard above. `catch { return false }` answers a question
    // with an exception it never looked at: a weapon on cooldown and a weapon
    // the content never defined come back as the same quiet "no", and the
    // second one stops being findable. Asking and committing are different
    // acts — the query returns null, the command throws.
    //
    // A catch that reports (binds the error, sets a status, rethrows) is fine;
    // one that silently produces a value is not.
    const offenders: string[] = [];
    const sources = [
      ...runtimeTypeScriptFiles(coreRoot),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'game-ui', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'campaign-engine', 'src')),
      ...runtimeTypeScriptFiles(join(packagesRoot, 'editor', 'src')),
    ];
    for (const file of sources) {
      // Comments are stripped first: a doc comment that *quotes* the mistake it
      // is warning about is not the mistake, and the guard flagged this very
      // file's explanation of itself before the strip went in.
      const source = readFileSync(file, 'utf8')
        .replace(/\/\*[\s\S]*?\*\//g, '')
        .replace(/(^|[^:])\/\/[^\n]*/g, '$1');
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
        const body = source.slice(match.index, end);
        if (/\breturn\b/.test(body)) {
          offenders.push(`${relative(packagesRoot, file)}: unbound catch returns a value`);
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

  it('takes dependencies first and the event channel last', () => {
    // One rule, not two: what it needs, what it acts on, where it reports.
    // "single content trailing, several services leading" was two rules, and
    // seventeen of the forty-two emitting functions had drifted between them.
    //
    // Scoped to functions that emit: that is the family that mutates a battle
    // and the family the drift happened in. Pure queries are left alone.
    const dependencies = ['content', 'rules', 'resources', 'progression', 'policy', 'space', 'handlers'];
    const offenders: string[] = [];
    for (const file of runtimeTypeScriptFiles(coreRoot)) {
      const source = readFileSync(file, 'utf8');
      for (const match of source.matchAll(/export function (\w+)/g)) {
        const names = parametersOf(source, match.index + match[0].length)
          .map((parameter) => parameter.split(':')[0].trim().replace(/[?=].*$/, '').trim());
        if (!names.includes('emit')) continue;
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
        if (problems.length > 0) offenders.push(`${relative(coreRoot, file)}#${match[1]}: ${problems.join('; ')}`);
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
      const source = readFileSync(file, 'utf8');
      for (const match of source.matchAll(/export function (\w+)/g)) {
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
