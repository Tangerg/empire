import type {
  CareerDef,
  FormationDef,
  MoveCosts,
  StatusDef,
  StructureDef,
  StructureTypeId,
  TerrainDef,
  TerrainOverlayDef,
  UnitDef,
  WeaponDef,
} from './types';

export function defineFormation(
  def: Partial<FormationDef> & Pick<FormationDef, 'id' | 'name'>,
): FormationDef {
  return {
    attackMultiplier: 1,
    defenseDelta: 0,
    movementDelta: 0,
    minimumAdjacentAllies: 0,
    tags: [],
    ...def,
  };
}

export function defineCareer(
  def: Partial<CareerDef> & Pick<CareerDef, 'id' | 'name' | 'unitType'>,
): CareerDef {
  return {
    tier: 0,
    from: [],
    minimumRank: 0,
    minimumMastery: 0,
    costs: [],
    masteryThreshold: 100,
    masteryAbilities: [],
    tags: [],
    ...def,
  };
}

/** Small authoring helpers. They fill schema defaults but never register data. */
export function defineWeapon(
  def: Partial<WeaponDef> & Pick<WeaponDef, 'id' | 'name' | 'power' | 'damageType'>,
): WeaponDef {
  return {
    minRange: 1,
    maxRange: 1,
    moveAndAttack: true,
    lineOfSight: 'none',
    area: 'single',
    canCounter: true,
    cooldown: 0,
    resources: {},
    resourceRequirements: [],
    resourceCosts: [],
    bonuses: [],
    hitEffects: [],
    tags: [],
    ...def,
  };
}

export function defineUnit(
  def: Partial<UnitDef> & Pick<UnitDef, 'id' | 'name' | 'weapons' | 'movementClass' | 'armorClass' | 'value' | 'recruitCosts'>,
  weapons: ReadonlyMap<string, WeaponDef>,
): UnitDef {
  const primary = weapons.get(def.weapons[0]);
  if (!primary) throw new Error(`unit "${def.id}" references missing primary weapon "${def.weapons[0]}"`);
  return {
    maxHp: 100,
    defense: 0,
    movement: 3,
    resources: {},
    vision: 3,
    abilities: ['attack', 'capture', 'wait'],
    defaultReaction: 'counter',
    formations: [],
    tags: [],
    blurb: '',
    ...def,
  };
}

export function defineTerrain(
  def: Partial<TerrainDef> & { id: string; name: string; cost: MoveCosts },
): TerrainDef {
  return {
    defense: 0,
    vision: 0,
    opaque: false,
    cover: 'none',
    obstructionHeight: 0,
    capturable: false,
    ownerTurnGrants: [],
    heal: 0,
    produces: [],
    hq: false,
    tags: [],
    ...def,
  };
}

export function defineStatus(
  def: Partial<StatusDef> & { id: string; name: string },
): StatusDef {
  return {
    stackMode: 'refresh',
    maxStacks: 1,
    modifiers: {},
    blockedAbilityTags: [],
    tags: [],
    ...def,
  };
}

export function defineStructure(
  def: Partial<StructureDef> & { id: StructureTypeId; name: string },
): StructureDef {
  return {
    maxHp: 100,
    defense: 0.1,
    blocksMovement: true,
    blocksVision: false,
    cover: 'none',
    obstructionHeight: 0,
    repairable: true,
    targetable: true,
    tags: [],
    ...def,
  };
}

export function defineTerrainOverlay(
  def: Partial<TerrainOverlayDef> & { id: string; name: string },
): TerrainOverlayDef {
  return {
    movementCostDelta: 0,
    blockedMovementClasses: [],
    defenseDelta: 0,
    visionDelta: 0,
    healDelta: 0,
    tags: [],
    ...def,
  };
}
