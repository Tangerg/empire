const fs = require('node:fs');
const path = require('node:path');
const output = process.argv[2];
if (!output) throw new Error('Pass an output directory');
fs.mkdirSync(output, { recursive: true });
function canonical(value, at = []) {
  if (Array.isArray(value)) return value.map((entry, index) => canonical(entry, [...at, String(index)]));
  if (value && typeof value === 'object') return Object.entries(value)
    .filter(([key, entry]) => entry !== undefined && !(at.length === 0 && key === 'levelName')
      && !(at.length === 2 && at[0] === 'players' && /^\d+$/.test(at[1]) && key === 'controller'))
    .sort(([left], [right]) => left < right ? -1 : left > right ? 1 : 0)
    .map(([key, entry]) => [key, canonical(entry, [...at, key])]);
  return value;
}
function fnv(text) {
  let hash = 0x811c9dc5;
  for (let index = 0; index < text.length; index++) hash = Math.imul(hash ^ text.charCodeAt(index), 0x01000193);
  return hash >>> 0;
}
const hash = value => fnv(JSON.stringify(canonical(value))).toString(16).padStart(8, '0');
const bytes = Buffer.alloc(8);
const numbers = [];
function addNumber(value) {
  if (!Number.isFinite(value)) return;
  bytes.writeDoubleLE(value);
  numbers.push(`${bytes.readBigUInt64LE().toString(16)}\t${JSON.stringify(value)}\t${hash({ number: value })}`);
}
for (const value of [0, -0, 1, -1, 0.1, 0.35, 1e-7, 1e-6, 1e20, 1e21, 1e23,
  1.0000000000000001e18, Number.MIN_VALUE, Number.MAX_VALUE, Math.PI, 9007199254740992]) addNumber(value);
for (let exponent = -324; exponent <= 308; exponent++) {
  for (const multiplier of [1, 1.23456789, 9.999999999999998]) {
    const value = multiplier * 10 ** exponent;
    addNumber(value); addNumber(-value);
    if (Number.isFinite(value) && value > 0) {
      bytes.writeDoubleLE(value);
      const bits = bytes.readBigUInt64LE();
      for (const offset of [-1n, 1n]) { bytes.writeBigUInt64LE(bits + offset); addNumber(bytes.readDoubleLE()); }
    }
  }
}
let bits = 0x9e3779b97f4a7c15n;
const mask = (1n << 64n) - 1n;
for (let index = 0; index < 250000; index++) {
  bits ^= bits << 13n; bits &= mask; bits ^= bits >> 7n; bits ^= bits << 17n; bits &= mask;
  bytes.writeBigUInt64LE(bits); addNumber(bytes.readDoubleLE());
}
fs.writeFileSync(path.join(output, 'numbers.tsv'), numbers.join('\n') + '\n');
const strings = [];
function addString(value) { strings.push(JSON.stringify({ value, hash: hash(value) })); }
for (let unit = 0; unit <= 65535; unit++) addString({ s: String.fromCharCode(unit) });
for (const s of ['灰旗😀', '\ud800X\udc00', '\ud800\udc00', '\u2028\u2029', '\0\b\f\n\r\t"\\/']) addString({ s });
for (const value of [{}, [], { 2: 'two', 10: 'ten' },
  { levelName: 'ignored', players: [{ controller: 'human', nested: { controller: 'AI' } }], nested: { levelName: 'B' }, controller: 'root' },
  { players: { '00': { controller: 'human' }, '٠': { controller: 'human' }, '1x': { controller: 'AI' } } },
  { '😀': 1, '\ue000': 2 }, { a: [null, true, false, {}, []] }]) addString(value);
fs.writeFileSync(path.join(output, 'strings.jsonl'), strings.join('\n') + '\n');
const random = [];
for (const seed of [0, 1, 2147483648, 4294967295, -1, -1.75, 4294967296, 1e30]) {
  for (const stream of ['combat', 'ai', '灰旗', '😀', '']) {
    for (const counter of [0, 1, 4294967295, 4294967296, 4294967297, 9007199254740991, 9007199254740992, -1, 1.75]) {
      let z = ((Math.trunc(seed) >>> 0) ^ fnv(stream) ^ Math.imul(counter, 0x85ebca6b)) >>> 0;
      z = (z + 0x9e3779b9) >>> 0; z ^= z >>> 16; z = Math.imul(z, 0x21f0aaad) >>> 0;
      z ^= z >>> 15; z = Math.imul(z, 0x735a2d97) >>> 0; z ^= z >>> 15;
      random.push(JSON.stringify({ seed, stream, counter, value: (z >>> 0) / 4294967296, nextCounter: counter + 1 }));
    }
  }
}
fs.writeFileSync(path.join(output, 'rng.jsonl'), random.join('\n') + '\n');
console.log(`Generated ${numbers.length} numeric, ${strings.length} UTF-16/path, ${random.length} RNG reference vectors.`);
