// Rules for pull request titles, which become the squash commits of main (ADR-0009, CONTRIBUTING.md).
//
//   <type>(<area>[/<module>]): <description>
//
// Try a title locally (from .github/commitlint, after `pnpm install`):
//   echo "feat(backend/accounts): add create endpoint" | pnpm lint-title

/** Commit types listed in CONTRIBUTING.md. */
export const TYPES = ['feat', 'fix', 'test', 'refactor', 'docs', 'chore', 'perf', 'build', 'ci'];

/** Areas of the repository. */
export const AREAS = ['backend', 'frontend', 'devops', 'docs'];

/** Modules of the backend; frontend features use the same names. A new module extends this list. */
export const MODULES = ['accounts', 'auth', 'budgets', 'categories', 'dashboard', 'transactions', 'transfers'];

/** Areas that can be narrowed to a module. */
const AREAS_WITH_MODULES = ['backend', 'frontend'];

const SCOPE = new RegExp(
  `^(?:(?:${AREAS.join('|')})|(?:${AREAS_WITH_MODULES.join('|')})/(?:${MODULES.join('|')}))$`,
);

export default {
  extends: ['@commitlint/config-conventional'],
  plugins: [
    {
      rules: {
        // commitlint's scope-enum splits scopes at "/", so it cannot tell "backend/accounts" from
        // "accounts/backend": the whole scope is matched here instead.
        'scope-area-module': ({ scope }) => [
          !scope || SCOPE.test(scope),
          `scope must be an area (${AREAS.join(', ')}) or ${AREAS_WITH_MODULES.join('/')} followed by ` +
            `"/<module>" (${MODULES.join(', ')})`,
        ],
      },
    },
  ],
  rules: {
    'type-enum': [2, 'always', TYPES],
    'scope-empty': [2, 'never'],
    'scope-enum': [0],
    'scope-area-module': [2, 'always'],
  },
  helpUrl: 'https://github.com/FloriiPaul09/personal-finance/blob/main/CONTRIBUTING.md#commit-messages',
};
