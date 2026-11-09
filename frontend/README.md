# Frontend

Angular application of Personal Finance. Setup and commands for the whole repository are in the [root README](../README.md).

| Command | What it does |
|---|---|
| `pnpm install` | Install dependencies (pnpm 11, Node.js version in `.nvmrc`) |
| `pnpm start` | Dev server on http://localhost:4200, `/api` proxied to http://localhost:5080 |
| `pnpm test` | Unit tests with Vitest (watch mode); `pnpm test --watch=false` for a single run |
| `pnpm build` | Production build in `dist/` |

Documentation: [docs/frontend/](../docs/frontend/README.md).
