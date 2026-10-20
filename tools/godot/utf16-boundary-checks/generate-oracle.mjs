import { writeFileSync } from 'node:fs';
import { hashState } from '../../../packages/battle-engine/src/replay';
import { SplitMixRandom, createRandomState } from '../../../packages/battle-engine/src/random';
const strings = ['', 'plain', '\ud800', '\udfff', '\ud800\ud800', '\udfff\ud800', '\ud800X\udfff', '骰子💥', 'a\0b', '\u2028\u2029', '\ufffd', '\ud800\udc00', '\udbff\udfff', '\\\ud800', '"\udfff', '\\\\\ud800', '\ud800\\ud800'];
for (let unit = 0; unit < 0x20; unit++) strings.push(String.fromCharCode(unit));
for (let unit = 0xd800; unit <= 0xdfff; unit++) strings.push(String.fromCharCode(unit));
const vectors = strings.map((stream) => {
  const state = { random: createRandomState(0x5820f37a), payload: { [stream]: stream }, values: [stream], number: 9007199254740993 };
  const initial = JSON.stringify(state);
  const draws = Array.from({ length: 8 }, () => SplitMixRandom.next(state, stream));
  return { stream, initial, after: JSON.stringify(state), hash: hashState(state), draws };
});
const json = ['{"b":1,"0":2,"10":3,"2":4,"4294967295":5,"01":6,"a":7}',
  '{"duplicate":1,"duplicate":2,"numeric":9007199254740993}',
  '[0,-0,1e0,1e-7,1e20,1e21,2.2250738585072014e-308,5e-324,1.7976931348623157e308,true,false,null]']
  .map(input => ({ input, output: JSON.stringify(JSON.parse(input)), hash: hashState(JSON.parse(input)) }));
const output = process.env.EMPIRE_UTF16_ORACLE_OUTPUT;
if (!output) throw new Error('Set EMPIRE_UTF16_ORACLE_OUTPUT');
writeFileSync(output, JSON.stringify({ vectors, json }));
console.log(`UTF-16 oracle: ${vectors.length} code-unit strings and ${json.length} numeric/property-order cases`);
