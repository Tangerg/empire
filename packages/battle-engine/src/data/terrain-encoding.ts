import { DomainInvariantError } from '../domain/errors';
import type { TerrainId } from '../types';

/** Mutable only through ContentPackInstaller during application composition. */
export class TerrainEncodingRegistry {
  private readonly byCharacter = new Map<string, TerrainId>();
  private readonly byTerrain = new Map<TerrainId, string>();
  private defaultTerrainId: TerrainId | null = null;
  private sealed = false;

  register(entries: Readonly<Record<string, TerrainId>>, defaultTerrain?: TerrainId): void {
    if (this.sealed) throw new DomainInvariantError('terrain encoding registry is sealed after composition');
    const incomingCharacters = new Set<string>();
    const incomingTerrains = new Set<TerrainId>();
    for (const [character, terrain] of Object.entries(entries)) {
      if ([...character].length !== 1) throw new DomainInvariantError(`terrain character must contain exactly one code point: "${character}"`);
      if (this.byCharacter.has(character) || incomingCharacters.has(character)) {
        throw new DomainInvariantError(`terrain character already registered: "${character}"`);
      }
      if (this.byTerrain.has(terrain) || incomingTerrains.has(terrain)) {
        throw new DomainInvariantError(`terrain already has a character: "${terrain}"`);
      }
      incomingCharacters.add(character);
      incomingTerrains.add(terrain);
    }
    if (defaultTerrain !== undefined && this.defaultTerrainId !== null && this.defaultTerrainId !== defaultTerrain) {
      throw new DomainInvariantError(`default terrain already registered: "${this.defaultTerrainId}"`);
    }
    for (const [character, terrain] of Object.entries(entries)) {
      this.byCharacter.set(character, terrain);
      this.byTerrain.set(terrain, character);
    }
    if (defaultTerrain !== undefined) this.defaultTerrainId = defaultTerrain;
  }

  terrain(character: string): TerrainId | undefined {
    return this.byCharacter.get(character);
  }

  character(terrain: TerrainId): string | undefined {
    return this.byTerrain.get(terrain);
  }

  get defaultTerrain(): TerrainId {
    if (this.defaultTerrainId === null) throw new DomainInvariantError('no default terrain installed; install a content pack first');
    return this.defaultTerrainId;
  }

  /**
   * The legend character a blank terrain row is written with.
   *
   * The catalog's answer to "what is this game's blank ground", which used to
   * be the literal `'.'` in the blank-level factory and the literal `'plain'`
   * in three editor tools — so a story paved with sand had no way to say so.
   */
  get defaultCharacter(): string {
    const character = this.character(this.defaultTerrain);
    if (character === undefined) {
      throw new DomainInvariantError(`default terrain "${this.defaultTerrain}" was registered without a character`);
    }
    return character;
  }

  hasCharacter(character: string): boolean {
    return this.byCharacter.has(character);
  }

  hasTerrain(terrain: TerrainId): boolean {
    return this.byTerrain.has(terrain);
  }

  currentDefaultTerrain(): TerrainId | null {
    return this.defaultTerrainId;
  }

  clone(): TerrainEncodingRegistry {
    const copy = new TerrainEncodingRegistry();
    copy.register(Object.fromEntries(this.byCharacter), this.defaultTerrainId ?? undefined);
    return copy;
  }

  seal(): this {
    this.sealed = true;
    return this;
  }
}
