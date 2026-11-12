# Documentation

Index of the project documentation. Each macro category matches a conversation topic of the project and has its own folder, with a `README.md` overview and one file per micro category.

| Macro category | Folder | Status |
|---|---|---|
| Architecture | [architecture/](architecture/README.md) | Initial decisions complete |
| Database & domain | [database/](database/README.md) | Implemented (MVP data model) |
| Backend API | [backend/](backend/README.md) | In progress — foundation, error handling, authentication, accounts, categories and transactions done; transfers next |
| Frontend | [frontend/](frontend/README.md) | In progress |
| DevOps | [devops/](devops/README.md) | In progress — repository skeleton and local environment done, CI next |
| Features | `features/` | Not started (after MVP) |

Folders become links as soon as their `README.md` exists.

## Document structure

Every document follows the same minimal structure:

1. **Scope** — what the document covers
2. **Current state** — how it works today
3. **Decisions** — choices made and why (linking the ADR when there is one)
4. **Tests** — how what the document describes is verified
5. **Scalability** — what changes with more users, more data or new features
6. **To do / open questions**
7. **Last updated** — date

Documents written before the **Tests** section was introduced receive it at their next update.

Important architectural decisions are recorded as ADRs in [architecture/adr/](architecture/README.md#architecture-decision-records).

---
_Last updated: 2026-11-12_
