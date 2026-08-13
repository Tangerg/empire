import { describe, expect, it } from 'vitest';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';

const coreRoot = join(import.meta.dirname, '..');
const packagesRoot = join(coreRoot, '..', '..');

function runtimeTypeScriptFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const path = join(directory, entry);
    if (entry === '__tests__') return [];
    return statSync(path).isDirectory()
      ? runtimeTypeScriptFiles(path)
      : entry.endsWith('.ts')
        ? [path]
        : [];
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
      'GlobalContentCatalog',
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
