import { PAL } from './palette';

/**
 * How full something is, drawn on the tile it stands on.
 *
 * A bar over a cell is not a hand-drawn tile: the player reads it as a
 * *measurement*, so two of them on the same board have to agree. Four existed,
 * and they did not.
 *
 * The HUD's bar and the unit's bar coloured three bands at 0.6 and 0.3; the
 * generic structure bar and the campaign's coloured two at 0.5, so a structure
 * at four tenths was red while a unit beside it at four tenths was amber. The two
 * structure bars also sat 0.6 units higher than the unit bar and were 0.6 thinner
 * — drift, not design. And the campaign's wrote three colours that appear nowhere
 * in the palette: `#66b873`, `#d85c4c` and `#201914`, one of them a near-miss of
 * `PAL.ink`.
 *
 * So: one colour rule, one geometry. The band edges and the geometry are the ones
 * the unit bar and the HUD already used, because those were the two that agreed.
 */

/** The band a fraction falls in: healthy, hurt, or nearly gone. */
export const gaugeColor = (ratio: number): string =>
  ratio > 0.6 ? PAL.hpGood : ratio > 0.3 ? PAL.hpMid : PAL.hpLow;

/** A bar across the foot of one cell, in tile units. */
export const tileGaugeBar = (ratio: number): string =>
  `<rect x="5" y="27.6" width="22" height="3.6" rx="1.8" fill="${PAL.ink}" opacity="0.65"/>
           <rect x="5.6" y="28.2" width="${(20.8 * Math.max(0, Math.min(1, ratio))).toFixed(2)}" height="2.4" rx="1.2" fill="${gaugeColor(ratio)}"/>`;
