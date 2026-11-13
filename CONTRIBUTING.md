# Contributing

Thanks for your interest in Personal Finance. This guide explains how to set up the project, how changes are proposed and what a change needs before it is merged.

## License of contributions

The project is released under the [MIT license](LICENSE). By opening a pull request you agree that your contribution is released under the same license. There is no separate contributor agreement.

## Set up

Follow the quick start in the [README](README.md#quick-start-development). The full guide, with ports, configuration and troubleshooting, is [docs/devops/local-development.md](docs/devops/local-development.md).

## Workflow

The repository follows **GitHub Flow** ([docs/devops/repository-workflow.md](docs/devops/repository-workflow.md)):

1. Create a short-lived branch from `main`, e.g. `feat/backend-create-account` or `fix/frontend-date-shift`.
2. Commit small, focused changes.
3. Open a pull request to `main` and fill in the checklist of the template.
4. The pull request is merged with **squash merge** once all checks are green and the review is approved.

`main` is protected: nobody pushes to it directly, the maintainer included.

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/) with the area and, when it applies, the module as scope:

```
<type>(<area>[/<module>]): <description>
```

| Part | Values |
|---|---|
| type | `feat`, `fix`, `test`, `refactor`, `docs`, `chore`, `perf`, `build`, `ci` |
| area | `backend`, `frontend`, `devops`, `docs` |
| module | `accounts`, `categories`, `transactions`, `budgets`, `auth` (the backend modules; new modules extend the list) |

Examples:

```
feat(backend/accounts): add create account endpoint
fix(frontend/transactions): keep the date when the time zone changes
chore(devops): pin PostgreSQL 18.6
docs(architecture): add ADR-0008
```

With squash merge the pull request title becomes the commit on `main`, so the title must follow the same format.

## Code style

Formatting rules are in [.editorconfig](.editorconfig) (C# and TypeScript) and `frontend/.prettierrc`. Line endings are LF everywhere ([.gitattributes](.gitattributes)). Code, commits and technical documentation are in English; the user interface is in Italian.

## Tests

Every change ships with its tests. The minimum required is the [Definition of Done](docs/architecture/testing-strategy.md#minimum-required), in short:

- business rules in `Domain`: unit tests for normal and edge cases;
- every endpoint: integration tests on a real PostgreSQL for success, `401`, validation error and isolation between users;
- shared Angular components and services with logic: Vitest tests;
- bug fixes: a test that reproduces the bug.

Run them with `dotnet test` in `backend/` (Docker running) and `pnpm test` in `frontend/`. In `frontend/`, `pnpm lint` must pass too: it also checks that features do not import each other ([docs/frontend/app-structure.md](docs/frontend/app-structure.md#boundaries)).

## Documentation

Behavior changes update the documentation in [docs/](docs/README.md) in the same pull request. Important architectural decisions get an ADR.

## Dependencies

Dependabot opens grouped pull requests every week. Angular majors are upgraded with `ng update`, PostgreSQL majors with a planned data migration: neither comes from the bot.
