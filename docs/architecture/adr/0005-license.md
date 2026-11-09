# ADR-0005: MIT license for the repository

- **Status**: Accepted
- **Date**: 2026-11-09
- **Deciders**: Paul

## Context

- The code will be published in a public GitHub repository; without a license, nobody may legally use, modify or contribute to it.
- It is a personal project: the goal is that it is used, self-hosted and improved, not to protect a commercial product.
- All main dependencies (Angular, Angular Material, .NET, EF Core, Npgsql) use permissive licenses, so no option is ruled out by compatibility.

## Decision

1. The repository is released under the **MIT license**, in a `LICENSE` file at the repository root.
2. Contributions are accepted under the **same license** (inbound = outbound): no separate contributor agreement.

## Consequences

**Positive**
- The best-known and simplest license: no friction for users, companies or contributors.
- No extra files or obligations beyond keeping the copyright and license notice.

**Negative**
- Anyone may build a closed or commercial product, including a hosted service, on this code without sharing changes.
- No explicit patent grant (considered irrelevant for this project).
- Relicensing later is possible for new versions, but code already released under MIT stays MIT.

**Follow-ups**
- `[DevOps]` Add the `LICENSE` file at the repository root with the copyright holder's full name and year.
- `[DevOps]` License section in the root `README.md`; a `CONTRIBUTING.md` stating that contributions are made under the MIT license.
- Third-party code or assets copied into the repository must keep their own license notices.

## Alternatives considered

**Apache 2.0** — Rejected. Permissive like MIT, with an explicit patent grant and contribution terms, but more verbose (NOTICE file) for a benefit that matters little here.

**AGPL-3.0** — Rejected. It would force anyone offering a modified version as a web service to publish their code, protecting against closed forks, but it discourages companies and some contributors, and later dual licensing would require agreements with every contributor. Plain GPL was not considered because it does not apply to software used only over a network.
