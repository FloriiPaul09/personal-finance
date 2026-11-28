#!/usr/bin/env bash
# Smoke test of a running instance (containers of compose.app.yaml) through nginx — docs/devops/ci-cd.md.
#
#   bash .github/scripts/smoke-test.sh [base-url] [email] [password]
#
# Defaults: http://127.0.0.1:8080 and the initial user from the environment
# (Auth__InitialUser__Email / Auth__InitialUser__Password). Exits non-zero at the first failed check.
set -euo pipefail

base="${1:-http://127.0.0.1:8080}"
email="${2:-${Auth__InitialUser__Email:?pass the email of the initial user}}"
password="${3:-${Auth__InitialUser__Password:?pass the password of the initial user}}"
jar="$(mktemp)"
trap 'rm -f "$jar"' EXIT

fail() { echo "::error title=Smoke test::$*"; exit 1; }
ok() { echo "ok - $*"; }
status() { curl -s -o /dev/null -w '%{http_code}' "$@"; }

# The API applies migrations at startup: wait until nginx reaches it.
for _ in $(seq 1 60); do
  [ "$(curl -s "$base/api/health" || true)" = "Healthy" ] && break
  sleep 2
done
[ "$(curl -s "$base/api/health")" = "Healthy" ] || fail "GET /api/health through nginx is not Healthy"
ok "API healthy through nginx (migrations applied)"

curl -s "$base/" | grep -q "<app-root" || fail "GET / is not the Angular app"
curl -s "$base/accounts/new" | grep -q "<app-root" || fail "a route of the app does not fall back to index.html"
[ "$(status "$base/does-not-exist.js")" = "404" ] || fail "a missing file does not answer 404"
ok "app, route fallback and 404 for missing files"

headers="$(curl -s -D - -o /dev/null "$base/")"
for header in "X-Content-Type-Options: nosniff" "X-Frame-Options: DENY" "Cache-Control: no-cache"; do
  grep -qi "^$header" <<<"$headers" || fail "GET / lacks the header '$header'"
done
main_js="$(curl -s "$base/" | grep -o 'main-[A-Za-z0-9_-]*\.js' | head -1)"
curl -s -D - -o /dev/null "$base/$main_js" | grep -qi "immutable" || fail "$main_js is not cached as immutable"
ok "security and cache headers"

body="$(curl -s "$base/api/accounts")"
grep -q '"code":"auth.required"' <<<"$body" || fail "GET /api/accounts without a session is not 401 auth.required: $body"
[ "$(status "$base/openapi/v1.json")" = "404" ] || fail "the OpenAPI document is served in Production"
ok "API protected, no development tools in Production"

curl -s -c "$jar" -b "$jar" -o /dev/null "$base/api/auth/antiforgery"
token="$(awk '$6 == "XSRF-TOKEN" { print $7 }' "$jar")"
[ -n "$token" ] || fail "no XSRF-TOKEN cookie from /api/auth/antiforgery"
login="$(curl -s -c "$jar" -b "$jar" -H "X-XSRF-TOKEN: $token" -H "Content-Type: application/json" \
  -d "{\"email\":\"$email\",\"password\":\"$password\"}" -w ' %{http_code}' "$base/api/auth/login")"
[ "${login##* }" = "200" ] || fail "login of the initial user failed: $login"
grep -q "pf_session" "$jar" || fail "no session cookie after the login"
categories="$(curl -s -b "$jar" "$base/api/categories")"
grep -q '"items":\[{' <<<"$categories" || fail "the initial user has no default categories: $categories"
ok "login of the initial user, session cookie, default categories"

echo "Smoke test passed: $base"
