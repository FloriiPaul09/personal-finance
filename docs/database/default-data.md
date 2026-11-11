# Default data

## Scope

Data created automatically for a new user: the default categories. What they are, when and how they are created, and how they can evolve.

## Current state

Implemented ([ADR-0010](../architecture/adr/0010-data-model-and-user-isolation.md)): the list in `Domain/Categories/DefaultCategories.cs`, the service `DefaultCategoriesInitializer` in `Infrastructure/Persistence`, registered by `AddPersistence`. Calling it when a user is created is up to `[Backend]` (initial user and registration).

### When

Right after a user is created:

- the initial user created at first startup from `Auth__InitialUser__*` ([authentication.md](../architecture/authentication.md));
- every future user created by registration.

`DefaultCategoriesInitializer.EnsureForUserAsync(userId)` works in its own context acting for the new user (`FixedCurrentUser`, [user-isolation.md](user-isolation.md#ownership-marker-and-current-user)), so the query filter, the interceptor and composite keys apply as for any other data. It is not part of the transaction that creates the user: it is idempotent instead, so the startup code can simply call it again at every start for the initial user, and a failed run is repaired by the next one.

### How

- The list lives in the domain (`Domain/Categories/DefaultCategories`), as plain data: name, kind, subcategories.
- `DefaultCategories.CreateFor(userId)` builds the domain entities, parents before subcategories; `DefaultCategoriesInitializer` saves them.
- Names are in Italian, like the user interface. They are user data: once created, the user renames, archives or adds categories freely.
- A user who already has categories gets nothing: running it twice does nothing, and a user who deleted or archived defaults never gets them back. It returns whether it created the categories.

### List

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
| List confirmed by Paul on 2026-11-11 | — | This document |
| Idempotent, outside the user creation transaction | Simple for the startup of the initial user; a failure is repaired at the next start | This document |

## Tests

- Unit tests (`Domain.Tests/Categories/DefaultCategoriesTests`): 14 categories and 6 subcategories; subcategories have the kind of a top-level parent; names unique per kind and parent, as the database requires; "Altro" for both kinds; separate copies per user.
- Integration tests (`Infrastructure.Tests/Persistence/DefaultCategoriesTests`): a new user gets exactly the list; a second run adds nothing; a user with own categories gets no defaults; two users get separate copies, invisible to each other.

## Scalability

- **Other languages**: choose the list by the user's locale (from registration or settings) when the interface supports more languages.
- **Templates**: users could pick a set (student, family, freelance) at registration.
- **New defaults**: changes to the list affect only users created afterwards; existing users keep their categories.

## To do / open questions

- `[Backend]` Call `EnsureForUserAsync` after creating the initial user at startup and, later, after registration.

---
_Last updated: 2026-11-10_
