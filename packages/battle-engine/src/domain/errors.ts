/** Raised when a domain entity is asked to violate a battle invariant. */
export class DomainInvariantError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'DomainInvariantError';
  }
}
