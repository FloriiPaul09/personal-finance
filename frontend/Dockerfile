# syntax=docker/dockerfile:1
# Image of the frontend: the production build of the Angular app served by nginx, which also proxies
# /api to the API container — docs/devops/docker.md, ADR-0032.
# Build from the repository root: docker build -t personal-finance-frontend frontend

# --- Build -----------------------------------------------------------------------------------------
# Same Node.js major as .nvmrc (checked in CI); Dependabot updates the patch.
FROM node:26.13.1-trixie-slim AS build
WORKDIR /app

# pnpm at the version of "packageManager" (Corepack is not bundled with Node.js 25 and later).
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
RUN npm install --global "$(node -p "require('./package.json').packageManager")" \
 && pnpm install --frozen-lockfile

COPY . .
RUN pnpm build

# --- Runtime ---------------------------------------------------------------------------------------
# Official NGINX image that runs as a non-root user and listens on 8080.
FROM nginx/nginx-unprivileged:1.30.8-alpine
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist/personal-finance/browser /usr/share/nginx/html
EXPOSE 8080
