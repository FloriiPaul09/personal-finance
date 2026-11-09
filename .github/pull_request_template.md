## What and why

<!-- One or two sentences. Link the issue if there is one. -->

## Definition of Done

- [ ] Title follows `type(area/module): description` (e.g. `feat(backend/accounts): add create endpoint`)
- [ ] Business rules in `Domain` have unit tests (normal and edge cases)
- [ ] Every new or changed endpoint has integration tests: success, `401`, validation error, isolation between users
- [ ] Shared Angular components and services with logic have Vitest tests
- [ ] E2E test updated if a critical flow changed (login, add expense on phone, transfer, dashboard)
- [ ] Bug fix comes with a test that reproduces the bug
- [ ] Documentation of the touched areas updated, including the Tests section
