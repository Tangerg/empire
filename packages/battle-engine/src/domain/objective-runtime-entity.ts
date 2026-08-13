import type { ObjectiveOutcome, ObjectiveRuntime, ObjectiveStatus } from '../types';

/** Rich lifecycle façade for one objective's serialisable runtime state. */
export class ObjectiveRuntimeEntity {
  constructor(readonly state: ObjectiveRuntime) {}

  get id(): string {
    return this.state.id;
  }

  get status(): ObjectiveStatus {
    return this.state.status;
  }

  get hidden(): boolean {
    return this.state.hidden;
  }

  changeStatus(status: ObjectiveStatus): void {
    this.state.status = status;
  }

  changeVisibility(hidden: boolean): void {
    this.state.hidden = hidden;
  }

  /** Only an active objective may be resolved by automatic evaluation. */
  resolve(outcome: ObjectiveOutcome): boolean {
    if (outcome === 'pending' || this.state.status !== 'active') return false;
    this.state.status = outcome === 'success' ? 'completed' : 'failed';
    return true;
  }
}
