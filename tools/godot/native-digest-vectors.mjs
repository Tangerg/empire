import { writeFileSync } from 'node:fs';

// Independent ECMAScript specification of the native all-field digest contract.
// This is fixture authoring only, never part of the game runtime.
function canonical(value) {
  if (Array.isArray(value)) return value.map(canonical);
  if (value !== null && typeof value === 'object')
    return Object.keys(value).sort().map(key => [key, canonical(value[key])]);
  return value;
}
function digest(value) {
  const input = JSON.stringify(canonical(value));
  let hash = 0x811c9dc5;
  for (let i = 0; i < input.length; i++)
    hash = Math.imul(hash ^ input.charCodeAt(i), 0x01000193) >>> 0;
  return hash.toString(16).padStart(8, '0');
}
const values = [
  { text: '灰旗😀\u0000\n\t"\\', lone: '\ud800', numbers: [0, -0, 1e-7, 1e-6, 1e20, 1e21, Number.MAX_VALUE] },
  { '😀': 1, '\ue000': 2, levelName: 'included', players: [{ controller: 'human', id: 1 }], nested: { levelName: 'included' } },
  { '😀': 1, '\ue000': 2, levelName: 'included', players: [{ controller: 'ai', id: 1 }], nested: { levelName: 'included' } },
  { '😀': 1, '\ue000': 2, levelName: 'changed', players: [{ controller: 'human', id: 1 }], nested: { levelName: 'included' } },
];
writeFileSync('godot/tests/native/digest-vectors.json', JSON.stringify(values.map(value => ({ value, hash: digest(value) })), null, 2) + '\n');
