/**
 * The level document: how it is written, read, and upgraded.
 *
 * This was one module called `mapio`, which was neither only about maps nor
 * about IO: it held terrain serialisation, the runtime map builder, the schema
 * migrations, the blank-level factory, and a four-hundred-line validator. Five
 * subjects under a name that described none of them.
 *
 * Linting a document is deliberately *not* here. A level is only playable under
 * a ruleset, so the linter needs the composed rules — while this layer is what
 * creating a state is built out of, one level below them.
 */
export * from './issues';
export * from './declarations';
export * from './defaults';
export * from './schema';
export * from './map';
