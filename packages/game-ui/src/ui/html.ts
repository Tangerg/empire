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
