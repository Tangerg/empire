import type {
  GameEvent,
  PlayerState,
  ResourceAmount,
  ResourceId,
  ResourceSubject,
  ResourceSubjectKind,
  ResourceSubjectKindMap,
  ResourceSubjectRef,
  ResourceTransaction,
  Unit,
  WeaponId,
} from './types';
import { DomainInvariantError } from './domain/errors';
import { KeyedRegistry } from './registry';

export type { ResourceSubject, ResourceSubjectKind, ResourceSubjectKindMap, ResourceSubjectRef };

export interface ResourceSnapshot {
  /** null means the account is unlimited. */
  current: number | null;
  /** null means no upper bound. */
  capacity: number | null;
}

export interface ResourceAccount {
  read(): ResourceSnapshot;
  write(current: number): void;
}

export interface ResourceAdapter<S extends ResourceSubject = ResourceSubject> {
  readonly id: ResourceId;
  readonly name: string;
  readonly subjectKind: S['kind'];
  readonly integer: boolean;
  /** Relative planning value for AI comparisons; it never changes legality. */
  readonly aiWeight: number;
  account(subject: S): ResourceAccount | null;
}

/** Registry of independent resource ports. Storage layout remains an adapter concern. */
export class ResourceAdapterRegistry extends KeyedRegistry<ResourceId, ResourceAdapter> {
  constructor() {
    super('resource adapter');
  }

  protected keyOf(adapter: ResourceAdapter): ResourceId {
    return adapter.id;
  }

  override register<S extends ResourceSubject>(adapter: ResourceAdapter<S>): this {
    if (!adapter.id.trim()) throw new DomainInvariantError('resource id cannot be empty');
    return super.register(adapter as ResourceAdapter);
  }

  override replace<S extends ResourceSubject>(adapter: ResourceAdapter<S>): this {
    return super.replace(adapter as ResourceAdapter);
  }

  clone(): ResourceAdapterRegistry {
    return this.copyInto(new ResourceAdapterRegistry());
  }
}

/** What a caller has to hand when a transaction has to name its holder. */
export interface ResourceTransactionContext {
  player?: PlayerState;
  unit: Unit;
  weapon: WeaponId;
}

/**
 * One holder kind, and the two things every part of the engine asks about it:
 * build one from what the caller has, and name one in a log line.
 *
 * The family is open, but both questions used to be answered by closed lists —
 * a three-way `switch` here, a three-branch ternary in the weapon-cost path,
 * another in the battle log — so a plugin could declare a holder that no cost
 * could charge and no line could mention.
 */
export interface ResourceSubjectResolver<K extends ResourceSubjectKind = ResourceSubjectKind> {
  readonly kind: K;
  /** Builds the holder, or refuses when the context does not carry it. */
  subjectFor(context: ResourceTransactionContext, transaction: ResourceTransaction): ResourceSubjectKindMap[K];
  /** Projects it to ids, for events that outlive the objects they mention. */
  ref(subject: ResourceSubjectKindMap[K]): ResourceSubjectRef;
}

export class ResourceSubjectResolverRegistry extends KeyedRegistry<ResourceSubjectKind, ResourceSubjectResolver> {
  constructor() {
    super('resource subject');
  }

  protected keyOf(resolver: ResourceSubjectResolver): ResourceSubjectKind {
    return resolver.kind;
  }

  override register<K extends ResourceSubjectKind>(resolver: ResourceSubjectResolver<K>): this {
    return super.register(resolver as ResourceSubjectResolver);
  }

  override replace<K extends ResourceSubjectKind>(resolver: ResourceSubjectResolver<K>): this {
    return super.replace(resolver as ResourceSubjectResolver);
  }

  clone(): ResourceSubjectResolverRegistry {
    return this.copyInto(new ResourceSubjectResolverRegistry());
  }
}

export const DefaultResourceSubjects = new ResourceSubjectResolverRegistry()
  .register<'player'>({
    kind: 'player',
    subjectFor: (context, transaction) => {
      if (!context.player) throw new DomainInvariantError(`resource "${transaction.resource}" requires a player context`);
      return playerResource(context.player);
    },
    ref: ({ player }) => ({ kind: 'player', id: player.id }),
  })
  .register<'unit'>({
    kind: 'unit',
    subjectFor: (context) => unitResource(context.unit),
    ref: ({ unit }) => ({ kind: 'unit', id: unit.id }),
  })
  .register<'weapon'>({
    kind: 'weapon',
    subjectFor: (context) => weaponResource(context.unit, context.weapon),
    ref: ({ unit, weapon }) => ({ kind: 'weapon', id: unit.id, slot: weapon }),
  });
DefaultResourceSubjects.seal();

/**
 * Generic resource application service. It owns clamping and spending rules;
 * individual adapters only expose where one resource account is stored.
 */
export class BattleResourceSystem {
  constructor(
    readonly adapters: ResourceAdapterRegistry,
    readonly subjects: ResourceSubjectResolverRegistry,
  ) {}

  /** The holder a transaction is charged to, given what the caller has. */
  subjectFor(context: ResourceTransactionContext, transaction: ResourceTransaction): ResourceSubject {
    return this.subjects.get(transaction.subject).subjectFor(context, transaction as never) as ResourceSubject;
  }

  /** The same holder as a log line carries it. */
  refOf(subject: ResourceSubject): ResourceSubjectRef {
    return this.subjects.get(subject.kind).ref(subject as never);
  }

  /**
   * Announces a movement that has already happened.
   *
   * Six call sites read the new balance, decided whether it was worth saying,
   * and assembled the same event by hand — including the holder, which each of
   * them wrote out as a literal. Silent when nothing moved, or when the account
   * is unlimited and has no number to report.
   */
  announce(
    subject: ResourceSubject,
    id: ResourceId,
    amount: number,
    emit: (event: GameEvent) => void,
  ): void {
    const current = this.balance(id, subject);
    if (amount === 0 || current === null) return;
    emit({ type: 'resourceChanged', resource: id, subject: this.refOf(subject), amount, current });
  }

  hasAccount(id: ResourceId, subject: ResourceSubject): boolean {
    const adapter = this.adapters.get(id);
    return adapter.subjectKind === subject.kind && adapter.account(subject as never) !== null;
  }

  inspect(id: ResourceId, subject: ResourceSubject): ResourceSnapshot {
    return this.requireAccount(id, subject).account.read();
  }

  balance(id: ResourceId, subject: ResourceSubject): number | null {
    return this.inspect(id, subject).current;
  }

  canSpend(id: ResourceId, subject: ResourceSubject, requested: number): boolean {
    const current = this.balance(id, subject);
    const amount = this.normalize(this.adapters.get(id), requested);
    return current === null || current >= amount;
  }

  canAfford(costs: readonly ResourceAmount[], subject: ResourceSubject): boolean {
    return [...sumAmounts(costs)].every(([resource, amount]) =>
      this.canSpend(resource, subject, amount));
  }

  spendAll(costs: readonly ResourceAmount[], subject: ResourceSubject): ResourceAmount[] {
    const totals = [...sumAmounts(costs)];
    const missing = totals.find(([resource, amount]) => !this.canSpend(resource, subject, amount));
    if (missing) {
      throw new DomainInvariantError(`insufficient resource "${missing[0]}": need ${missing[1]}`);
    }
    for (const [resource, amount] of totals) this.spend(resource, subject, amount);
    return totals.map(([resource, amount]) => ({ resource, amount }));
  }

  planningValue(amounts: readonly ResourceAmount[]): number {
    return amounts.reduce(
      (total, amount) => total + amount.amount * this.adapters.get(amount.resource).aiWeight,
      0,
    );
  }

  credit(id: ResourceId, subject: ResourceSubject, requested: number): number {
    const { adapter, account } = this.requireAccount(id, subject);
    const amount = this.normalize(adapter, requested);
    const snapshot = account.read();
    if (snapshot.current === null || amount <= 0) return 0;
    const next = snapshot.capacity === null
      ? snapshot.current + amount
      : Math.min(snapshot.capacity, snapshot.current + amount);
    account.write(next);
    return next - snapshot.current;
  }

  spend(id: ResourceId, subject: ResourceSubject, requested: number): number {
    const { adapter, account } = this.requireAccount(id, subject);
    const amount = this.normalize(adapter, requested);
    const snapshot = account.read();
    if (snapshot.current === null || amount <= 0) return 0;
    if (snapshot.current < amount) {
      throw new DomainInvariantError(`insufficient resource "${id}": need ${amount}`);
    }
    account.write(snapshot.current - amount);
    return amount;
  }

  clone(): BattleResourceSystem {
    return new BattleResourceSystem(this.adapters.clone(), this.subjects.clone());
  }

  seal(): this {
    this.adapters.seal();
    this.subjects.seal();
    return this;
  }

  private requireAccount(
    id: ResourceId,
    subject: ResourceSubject,
  ): { adapter: ResourceAdapter; account: ResourceAccount } {
    const adapter = this.adapters.get(id);
    if (adapter.subjectKind !== subject.kind) {
      throw new DomainInvariantError(`resource "${id}" requires ${adapter.subjectKind} subject, received ${subject.kind}`);
    }
    const account = adapter.account(subject as never);
    if (!account) throw new DomainInvariantError(`resource "${id}" is not available for this ${subject.kind}`);
    return { adapter, account };
  }

  private normalize(adapter: ResourceAdapter, requested: number): number {
    if (!Number.isFinite(requested)) throw new DomainInvariantError(`resource "${adapter.id}" amount must be finite`);
    const nonnegative = Math.max(0, requested);
    return adapter.integer ? Math.round(nonnegative) : nonnegative;
  }
}

export const FUNDS_RESOURCE = 'funds';
export const COMMAND_POINTS_RESOURCE = 'command_points';
export const MOMENTUM_RESOURCE = 'momentum';
export const WEAPON_USES_RESOURCE = 'weapon_uses';

export const playerResource = (player: PlayerState): ResourceSubjectKindMap['player'] => ({
  kind: 'player',
  player,
});

export const unitResource = (unit: Unit): ResourceSubjectKindMap['unit'] => ({ kind: 'unit', unit });

export const weaponResource = (
  unit: Unit,
  weapon: WeaponId,
): ResourceSubjectKindMap['weapon'] => ({ kind: 'weapon', unit, weapon });

export function canAffordTransactions(
  resources: BattleResourceSystem,
  transactions: readonly ResourceTransaction[],
  context: ResourceTransactionContext,
): boolean {
  const totals = new Map<string, { transaction: ResourceTransaction; amount: number }>();
  for (const transaction of transactions) {
    const key = `${transaction.subject}\u0000${transaction.resource}`;
    const total = totals.get(key);
    if (total) total.amount += transaction.amount;
    else totals.set(key, { transaction, amount: transaction.amount });
  }
  return [...totals.values()].every(({ transaction, amount }) => {
    const subject = resources.subjectFor(context, transaction);
    return resources.hasAccount(transaction.resource, subject) &&
      resources.canSpend(transaction.resource, subject, amount);
  });
}

function sumAmounts(amounts: readonly ResourceAmount[]): Map<ResourceId, number> {
  const totals = new Map<ResourceId, number>();
  for (const amount of amounts) {
    totals.set(amount.resource, (totals.get(amount.resource) ?? 0) + amount.amount);
  }
  return totals;
}

function accountIn(accounts: Record<ResourceId, { current: number; capacity: number | null }>, id: ResourceId): ResourceAccount | null {
  const state = accounts[id];
  return state
    ? {
        read: () => ({ current: state.current, capacity: state.capacity }),
        write: (current) => { state.current = current; },
      }
    : null;
}

const fundsAdapter: ResourceAdapter<ResourceSubjectKindMap['player']> = {
    id: FUNDS_RESOURCE,
    name: '资金',
    subjectKind: 'player',
    integer: true,
    aiWeight: 1,
    account: ({ player }) => accountIn(player.resources, FUNDS_RESOURCE),
  };

const commandPointsAdapter: ResourceAdapter<ResourceSubjectKindMap['player']> = {
    id: COMMAND_POINTS_RESOURCE,
    name: '指挥点',
    subjectKind: 'player',
    integer: true,
    aiWeight: 8,
    account: ({ player }) => accountIn(player.resources, COMMAND_POINTS_RESOURCE),
  };

const momentumAdapter: ResourceAdapter<ResourceSubjectKindMap['unit']> = {
    id: MOMENTUM_RESOURCE,
    name: '气势',
    subjectKind: 'unit',
    integer: true,
    aiWeight: 2,
    account: ({ unit }) => accountIn(unit.resources, MOMENTUM_RESOURCE),
  };

const weaponUsesAdapter: ResourceAdapter<ResourceSubjectKindMap['weapon']> = {
    id: WEAPON_USES_RESOURCE,
    name: '武器次数',
    subjectKind: 'weapon',
    integer: true,
    aiWeight: 20,
    account: ({ unit, weapon }) => {
      const runtime = unit.weaponState[weapon];
      return runtime ? accountIn(runtime.resources, WEAPON_USES_RESOURCE) : null;
    },
  };

export const DefaultResourceAdapters = new ResourceAdapterRegistry()
  .register(fundsAdapter)
  .register(commandPointsAdapter)
  .register(momentumAdapter)
  .register(weaponUsesAdapter);
DefaultResourceAdapters.seal();

export const DefaultBattleResources = new BattleResourceSystem(DefaultResourceAdapters, DefaultResourceSubjects);
