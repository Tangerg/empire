/**
 * Raised when a stored document cannot be read by this ruleset.
 *
 * The third answer, and the one that was missing. A save written months ago
 * against plugins that have since moved on is neither of the pair below: the
 * caller did nothing wrong, and no order was refused — the *file* is the input,
 * and rejecting it is the correct outcome. It was thrown as a bare `Error`, so
 * the shell that offers 「读取存档」 had to catch everything and report every
 * defect underneath as "your save is unreadable".
 */
export class StoredDocumentError extends Error {
  constructor(message: string, options?: ErrorOptions) {
    super(message, options);
    this.name = 'StoredDocumentError';
  }
}

/** Raised when a domain entity is asked to violate a battle invariant. */
export class DomainInvariantError extends Error {
  constructor(message: string, options?: ErrorOptions) {
    super(message, options);
    this.name = 'DomainInvariantError';
  }
}

/**
 * Raised when the rules refuse an order.
 *
 * This belongs beside the invariant error rather than in the action pipeline:
 * refusing an order is a domain decision, and the collaborator that detects the
 * problem is the one that should say so. While this type lived in the action
 * layer, collaborators could only throw plain `Error`s, so every handler wrapped
 * them in `try/catch` and relabelled *anything* thrown — including genuine
 * defects — as an illegal move.
 *
 * The two are a deliberate pair, and the distinction is whose fault it is:
 * `IllegalActionError` means the order was wrong (recoverable, user-facing),
 * `DomainInvariantError` means the caller was wrong (a defect to fix).
 */
export class IllegalActionError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'IllegalActionError';
  }
}
