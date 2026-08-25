/** A campaign command was incompatible with the state the player is in. */
export class CampaignActionError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'CampaignActionError';
  }
}

/** Authored campaign data or an internal caller violated a campaign invariant. */
export class CampaignInvariantError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'CampaignInvariantError';
  }
}
