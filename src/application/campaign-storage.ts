import { createCampaignSave, DefaultCampaignSaveMigrator, type CampaignDefinition, type CampaignState } from '../campaign';

const keyFor = (definition: CampaignDefinition): string => `empire:campaign:${definition.id}:v${definition.version}`;

export function loadCampaignState(definition: CampaignDefinition): CampaignState | null {
  try {
    const raw = localStorage.getItem(keyFor(definition));
    if (!raw) return null;
    const save = DefaultCampaignSaveMigrator.load(JSON.parse(raw), definition);
    // Battle-local action history is deliberately not reconstructed. Saves are
    // therefore only written between battles, and a legacy pending save is ignored.
    return save.state.pendingBattle ? null : save.state;
  } catch {
    return null;
  }
}

export function saveCampaignState(definition: CampaignDefinition, state: CampaignState): void {
  if (state.pendingBattle) return;
  localStorage.setItem(keyFor(definition), JSON.stringify(createCampaignSave(definition, state)));
}

export function deleteCampaignState(definition: CampaignDefinition): void {
  localStorage.removeItem(keyFor(definition));
}
