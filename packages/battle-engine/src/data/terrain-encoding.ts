import type { TerrainId } from '../types';

/** Mutable only through ContentPackInstaller during application composition. */
export class TerrainEncodingRegistry {
  private readonly byCharacter = new Map<string, TerrainId>();
  private readonly byTerrain = new Map<TerrainId, string>();
  private fallback: TerrainId | null = null;

  register(entries: Readonly<Record<string, TerrainId>>, defaultTerrain?: TerrainId): void {
    const incomingCharacters = new Set<string>();
    const incomingTerrains = new Set<TerrainId>();
    for (const [character, terrain] of Object.entries(entries)) {
      if ([...character].length !== 1) throw new Error(`terrain character must contain exactly one code point: "${character}"`);
      if (this.byCharacter.has(character) || incomingCharacters.has(character)) {
        throw new Error(`terrain character already registered: "${character}"`);
      }
      if (this.byTerrain.has(terrain) || incomingTerrains.has(terrain)) {
        throw new Error(`terrain already has a character: "${terrain}"`);
      }
      incomingCharacters.add(character);
      incomingTerrains.add(terrain);
    }
    if (defaultTerrain !== undefined && this.fallback !== null && this.fallback !== defaultTerrain) {
      throw new Error(`default terrain already registered: "${this.fallback}"`);
    }
    for (const [character, terrain] of Object.entries(entries)) {
      this.byCharacter.set(character, terrain);
      this.byTerrain.set(terrain, character);
    }
    if (defaultTerrain !== undefined) this.fallback = defaultTerrain;
  }

  terrain(character: string): TerrainId | undefined {
    return this.byCharacter.get(character);
  }

  character(terrain: TerrainId): string | undefined {
    return this.byTerrain.get(terrain);
  }

  get defaultTerrain(): TerrainId {
    if (this.fallback === null) throw new Error('no default terrain installed; install a content pack first');
    return this.fallback;
  }

  characters(): Record<string, TerrainId> {
    return Object.fromEntries(this.byCharacter);
  }

  terrains(): Record<TerrainId, string> {
    return Object.fromEntries(this.byTerrain);
  }

  hasCharacter(character: string): boolean {
    return this.byCharacter.has(character);
  }

  hasTerrain(terrain: TerrainId): boolean {
    return this.byTerrain.has(terrain);
  }

  currentDefaultTerrain(): TerrainId | null {
    return this.fallback;
  }

  clone(): TerrainEncodingRegistry {
    const copy = new TerrainEncodingRegistry();
    copy.register(this.characters(), this.fallback ?? undefined);
    return copy;
  }
}

