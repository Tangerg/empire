/**
 * The order two ids go in, on every machine.
 *
 * Seven places in the rules broke a tie with `left.localeCompare(right)`, and a
 * locale is not part of a ruleset. `localeCompare` collates by the host's ICU data
 * and default locale: punctuation is where locales disagree most, and these ids are
 * full of it — `c01.knight-lance`, `formation-line`, `common.heal`. A Node built
 * without ICU falls back to code-unit order, which is a *third* answer.
 *
 * What those ties decided is not cosmetic. Which weapon a support attack picks and
 * which the best-damage search keeps are both this tiebreak, so damage depends on
 * it. So does the order handlers run in, the career a unit starts with, and the
 * content manifest a save is checked against.
 *
 * Code-unit order, then: the one order every JavaScript engine agrees on, and what
 * a machine name deserves. Replacing the collation moved the replay digest by
 * nothing at all — on *this* machine's locale ICU and code units agreed about every
 * tie the campaign actually reaches, which is exactly why the risk was invisible.
 *
 * Not for anything a player reads: a list of Chinese names wants a collation, and
 * that belongs in the presentation layer where a locale is a real question.
 */
export const byId = (left: string, right: string): number =>
  (left < right ? -1 : left > right ? 1 : 0);
