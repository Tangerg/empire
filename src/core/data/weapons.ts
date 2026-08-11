import { Registry } from '../registry';
import type { WeaponDef, WeaponId } from '../types';

export const Weapons = new Registry<WeaponDef>('weapon');

export const weaponDef = (id: WeaponId): WeaponDef => Weapons.get(id);
