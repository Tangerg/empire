import type { ArmorClassDef, DamageMatchupDef, DamageTypeDef } from '../../core/types';

export const ANCIENT_EMPIRES_DAMAGE_TYPES: readonly DamageTypeDef[] = [
  { id: 'slash', name: '斩击', tags: ['physical'] },
  { id: 'pierce', name: '穿刺', tags: ['physical'] },
  { id: 'blunt', name: '钝击', tags: ['physical'] },
  { id: 'magic', name: '魔法', tags: ['arcane'] },
];

export const ANCIENT_EMPIRES_ARMOR_CLASSES: readonly ArmorClassDef[] = [
  { id: 'unarmored', name: '无甲', tags: ['unarmored'] },
  { id: 'light', name: '轻甲', tags: ['armored'] },
  { id: 'heavy', name: '重甲', tags: ['armored', 'heavy'] },
  { id: 'flying', name: '飞行', tags: ['air'] },
];

const matrix: Record<string, Record<string, number>> = {
  slash: { unarmored: 1.2, light: 1.0, heavy: 0.7, flying: 0.9 },
  pierce: { unarmored: 1.0, light: 1.1, heavy: 0.8, flying: 1.1 },
  blunt: { unarmored: 1.0, light: 0.9, heavy: 1.3, flying: 0.7 },
  magic: { unarmored: 1.1, light: 1.0, heavy: 1.4, flying: 1.2 },
};

export const ANCIENT_EMPIRES_DAMAGE_MATCHUPS: readonly DamageMatchupDef[] =
  ANCIENT_EMPIRES_DAMAGE_TYPES.flatMap((damage) =>
    ANCIENT_EMPIRES_ARMOR_CLASSES.map((armor) => ({
      damageType: damage.id,
      armorClass: armor.id,
      multiplier: matrix[damage.id][armor.id],
    })),
  );
