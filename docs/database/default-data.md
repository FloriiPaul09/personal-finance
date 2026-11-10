# Default data

## Scope

Data created automatically for a new user: the default categories. What they are, when and how they are created, and how they can evolve.

## Current state

Decided ([ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md)), **not implemented yet** (branch `feat/backend-default-categories`).

### When

Right after a user is created, in the same transaction:

- the initial user created at first startup from `Auth__InitialUser__*` ([authentication.md](../architecture/authentication.md));
- every future user created by registration.

The creation runs with a fixed current user set to the new user ([user-isolation.md](user-isolation.md#ownership-marker-and-current-user)), so the query filter, the interceptor and composite keys apply as for any other data.

### How

- The list lives in the domain (`Domain/Categories/DefaultCategories`), as plain data: name, kind, subcategories.
- An `Infrastructure` service creates the categories for a given user; the startup code of the initial user (`[Backend]`) and the future registration call it.
- Names are in Italian, like the user interface. They are user data: once created, the user renames, archives or adds categories freely.
- Running it twice for the same user does nothing (the user already has categories).

### Proposed list

| Kind | Category | Subcategories |
|---|---|---|
| Expense | Casa | Affitto o mutuo, Bollette, Manutenzione |
| Expense | Spesa | — |
| Expense | Trasporti | Carburante, Mezzi pubblici, Auto |
| Expense | Ristoranti e bar | — |
| Expense | Salute | — |
| Expense | Svago | — |
| Expense | Abbigliamento | — |
| Expense | Abbonamenti | — |
| Expense | Regali | — |
| Expense | Altro | — |
| Income | Stipendio | — |
| Income | Rimborsi | — |
| Income | Regali ricevuti | — |
| Income | Altro | — |

"Altro" exists for both kinds, so a quick entry from the phone always has a category ([domain-model.md](domain-model.md#income-and-expense)). The two "Altro" do not collide: uniqueness includes the kind ([conventions.md](conventions.md#constraints)).

## Decisions

| Decision | Reason | Reference |
|---|---|---|
| Per-user categories created with the user | Each user owns and edits them; isolation has no exceptions | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| No `HasData`, no global categories | The owner does not exist when migrations run; global rows would bypass isolation | [ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md) |
| Italian names | They are user data shown in the Italian interface | This document |

## Tests

- Unit test: the list is valid for the domain (one level, children with the parent's kind, unique names per parent).
- Integration tests (`Infrastructure.Tests`): a new user gets exactly the list; a second run adds nothing; two users get separate copies, invisible to each other.

## Scalability

- **Other languages**: choose the list by the user's locale (from registration or settings) when the interface supports more languages.
- **Templates**: users could pick a set (student, family, freelance) at registration.
- **New defaults**: changes to the list affect only users created afterwards; existing users keep their categories.

## To do / open questions

- Confirm the proposed list (Paul).
- `[Backend]` Call the service from the initial user startup and, later, from registration.

---
_Last updated: 2026-11-10_
