const HTML_ENTITIES: Readonly<Record<string, string>> = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
};

/** Escapes untrusted text before inserting it into template-based UI markup. */
export const escapeHtml = (value: string): string =>
  value.replace(/[&<>"']/g, (character) => HTML_ENTITIES[character]);

/**
 * The element this application mounts into, or a refusal naming what is missing.
 *
 * Four shells did this, and they did it three ways: two held the element after
 * checking for it, and two wrote `document.getElementById('app')!` — an assertion
 * that the page contains what the bundle assumes. When it does not, the first is
 * "missing #app" at startup and the second is a `TypeError` about a property of
 * `null`, thrown from wherever the element is first touched.
 *
 * The guard that forbids that assertion reads the packages, so the only two
 * copies of it left in the repository were in the shells it does not read.
 */
export function requireMountPoint(id: string): HTMLElement {
  const element = document.getElementById(id);
  if (!element) throw new Error(`cannot mount: this page has no #${id} element`);
  return element;
}
