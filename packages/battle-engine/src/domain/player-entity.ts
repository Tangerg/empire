import type { PlayerState } from '../types';
import { DomainInvariantError } from './errors';
import { ObjectiveRuntimeEntity } from './objective-runtime-entity';

export class PlayerEntity {
  constructor(readonly state: PlayerState) {}

  defeat(): void {
    this.state.alive = false;
  }

  objective(id: string): ObjectiveRuntimeEntity {
    const runtime = this.state.objectiveStates[id];
    if (!runtime) throw new DomainInvariantError(`unknown objective "${id}" for player ${this.state.id}`);
    return new ObjectiveRuntimeEntity(runtime);
  }
}
