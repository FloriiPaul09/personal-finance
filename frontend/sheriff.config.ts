import { SheriffConfig } from '@softarc/sheriff-core';

/**
 * Module boundaries of the app, checked by ESLint (`pnpm lint`).
 * A module is a folder with an index.ts: other modules import only that file.
 * See docs/frontend/app-structure.md#boundaries.
 */
export const config: SheriffConfig = {
  entryFile: 'src/main.ts',
  modules: {
    'src/app': {
      core: 'core',
      // Own barrel so @angular/forms stays out of the initial bundle (app-structure.md#boundaries).
      'core/forms': 'core',
      // Own barrel: components with Material's select, used only by lazy pages.
      'core/selectors': 'core',
      // Test data shaped like the API's answers (unit tests only).
      'core/testing': 'core',
      'shared/<shared>': 'shared',
      'features/<feature>': 'feature',
    },
  },
  depRules: {
    // The app itself (main.ts, app.*): wires everything together.
    root: ['core', 'shared', 'feature'],
    // Singletons and layout: may use shared code, never a feature.
    core: ['core', 'shared'],
    // Reusable UI and utilities: know nothing about the app.
    shared: 'shared',
    // A feature uses core and shared, never another feature (each feature is its own module).
    feature: ['core', 'shared'],
    // Barrels without a known tag are not allowed to exist unnoticed.
    noTag: [],
  },
};
