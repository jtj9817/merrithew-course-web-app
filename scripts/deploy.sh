#!/usr/bin/env bash
#
# deploy.sh — validate the host, then build and start the Course Inquiry
# Dashboard container(s) via Docker Compose.
#
# The stack is a single ASP.NET Core process (Web API + Razor shell + React
# island over SQLite) described by ../docker-compose.yml. On the shared VPS the
# published port stays bound to loopback (127.0.0.1:8088) and a Forge-managed
# nginx site reverse-proxies to it and terminates TLS; container lifecycle is
# owned here (restart: unless-stopped), so this script is the entrypoint for a
# fresh deploy or a redeploy.
#
# It runs a set of preflight checks against the environment before touching the
# stack, brings it up detached, and verifies the app answers on /health.
#
# Usage:
#   scripts/deploy.sh [options]
#
# Options:
#   -n, --no-build   Start using the existing image; skip the image rebuild.
#   -s, --seed-dev   Enable the dev scenario-seeding endpoints for this run
#                    (DEV_SCENARIO_SEEDING=true). Never use on the VPS.
#   -t, --timeout N  Seconds to wait for the health check (default: 90).
#   -h, --help       Show this help and exit.
#
# Exit codes:
#   0  success
#   1  usage error
#   2  required tooling missing (docker / docker compose)
#   3  docker daemon not reachable
#   4  required project files missing
#   5  build / start failed
#   6  container started but health check did not pass

set -Eeuo pipefail

# --- Configuration ---------------------------------------------------------

# Resolve the repo root from this script's location so the script works no
# matter the caller's current directory.
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd -P)"
REPO_ROOT="$(cd -- "${SCRIPT_DIR}/.." >/dev/null 2>&1 && pwd -P)"

COMPOSE_FILE="${REPO_ROOT}/docker-compose.yml"
DOCKERFILE="${REPO_ROOT}/Dockerfile"
SERVICE="app"

# Defaults, overridable by flags.
DO_BUILD=1
SEED_DEV=0
HEALTH_TIMEOUT=90

# --- Logging ---------------------------------------------------------------

if [[ -t 1 ]]; then
  C_RESET=$'\033[0m'; C_INFO=$'\033[0;34m'; C_OK=$'\033[0;32m'
  C_WARN=$'\033[0;33m'; C_ERR=$'\033[0;31m'
else
  C_RESET=''; C_INFO=''; C_OK=''; C_WARN=''; C_ERR=''
fi

info()  { printf '%s[ deploy ]%s %s\n' "$C_INFO" "$C_RESET" "$*"; }
ok()    { printf '%s[   ok   ]%s %s\n' "$C_OK"   "$C_RESET" "$*"; }
warn()  { printf '%s[  warn  ]%s %s\n' "$C_WARN" "$C_RESET" "$*" >&2; }
err()   { printf '%s[ error  ]%s %s\n' "$C_ERR"  "$C_RESET" "$*" >&2; }

# die <exit-code> <message...>
die() { local code="$1"; shift; err "$*"; exit "$code"; }

# Catch anything that fails unexpectedly (a command we did not guard with an
# explicit check). Guarded failures call die() directly with a clear message.
on_error() {
  local rc="$1" line="$2"
  err "Unexpected failure (exit ${rc}) at line ${line}. Deploy did not complete."
  exit "$rc"
}
trap 'on_error "$?" "$LINENO"' ERR

# --- Argument parsing ------------------------------------------------------

usage() {
  # Print the header comment block (from line 3 to the first non-comment line,
  # i.e. the blank line before `set`) as help text, stripping the '# ' prefix.
  awk 'NR>=3 { if ($0 ~ /^#/) { sub(/^# ?/, ""); print } else { exit } }' "${BASH_SOURCE[0]}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -n|--no-build) DO_BUILD=0; shift ;;
    -s|--seed-dev) SEED_DEV=1; shift ;;
    -t|--timeout)
      [[ $# -ge 2 ]] || die 1 "Option $1 requires a value (seconds)."
      HEALTH_TIMEOUT="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) err "Unknown option: $1"; echo; usage; exit 1 ;;
  esac
done

[[ "$HEALTH_TIMEOUT" =~ ^[0-9]+$ && "$HEALTH_TIMEOUT" -gt 0 ]] \
  || die 1 "--timeout must be a positive integer (got '${HEALTH_TIMEOUT}')."

# --- Compose command detection ---------------------------------------------

# Prefer the Compose v2 plugin (`docker compose`); fall back to the legacy
# standalone binary (`docker-compose`) so this works on older hosts.
COMPOSE=()
detect_compose() {
  if docker compose version >/dev/null 2>&1; then
    COMPOSE=(docker compose)
  elif command -v docker-compose >/dev/null 2>&1; then
    COMPOSE=(docker-compose)
  else
    die 2 "Docker Compose not found. Install the Compose v2 plugin ('docker compose') or the 'docker-compose' binary."
  fi
}

# --- Preflight checks ------------------------------------------------------

preflight() {
  info "Checking the environment…"

  # 1. Docker CLI present.
  command -v docker >/dev/null 2>&1 \
    || die 2 "'docker' is not installed or not on PATH. Install Docker Engine first."

  # 2. Docker daemon reachable (also catches a permissions problem: user not in
  #    the 'docker' group). `docker info` is the canonical liveness probe.
  if ! docker info >/dev/null 2>&1; then
    die 3 "Cannot reach the Docker daemon. Is it running, and can this user access it (in the 'docker' group, or run with sudo)?"
  fi

  # 3. Compose available.
  detect_compose

  # 4. Required project files present.
  [[ -f "$COMPOSE_FILE" ]] || die 4 "Compose file not found: ${COMPOSE_FILE}"
  [[ -f "$DOCKERFILE"   ]] || die 4 "Dockerfile not found: ${DOCKERFILE}"

  # 5. Compose file parses cleanly before we act on it.
  if ! "${COMPOSE[@]}" -f "$COMPOSE_FILE" config >/dev/null 2>&1; then
    err "docker-compose.yml failed to validate:"
    "${COMPOSE[@]}" -f "$COMPOSE_FILE" config >/dev/null || true
    die 4 "Fix the compose configuration and re-run."
  fi

  # 6. Host port availability (informational). The compose file binds the app
  #    to loopback; if something *other* than our own container already holds
  #    the port, `compose up` will fail — surface that early as a warning.
  local host_port
  host_port="$(grep -oE '127\.0\.0\.1:[0-9]+:' "$COMPOSE_FILE" | head -n1 | cut -d: -f2 || true)"
  host_port="${host_port:-8088}"
  HOST_PORT="$host_port"
  if port_in_use "$host_port" && ! stack_owns_port; then
    warn "Port 127.0.0.1:${host_port} is already in use by another process; 'compose up' may fail to bind it."
  fi

  # 7. Free disk space (informational). A clean multi-stage build pulls the
  #    .NET SDK + Node images and can need a few GB.
  local avail_kb
  avail_kb="$(df -Pk "$REPO_ROOT" 2>/dev/null | awk 'NR==2 {print $4}' || true)"
  if [[ -n "${avail_kb:-}" && "$avail_kb" =~ ^[0-9]+$ && "$avail_kb" -lt 2097152 ]]; then
    warn "Low free disk space ($((avail_kb / 1024)) MB) on the build filesystem; the image build may fail."
  fi

  ok "Environment looks good (compose: '${COMPOSE[*]}', host port: ${HOST_PORT})."
}

# True if a local TCP port is currently listening. Tries ss, then lsof, then a
# bash /dev/tcp probe; if none can tell, returns false (skip the warning).
port_in_use() {
  local port="$1"
  if command -v ss >/dev/null 2>&1; then
    ss -ltn 2>/dev/null | awk '{print $4}' | grep -qE "[:.]${port}\$"
  elif command -v lsof >/dev/null 2>&1; then
    lsof -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1
  else
    (exec 3<>"/dev/tcp/127.0.0.1/${port}") >/dev/null 2>&1
  fi
}

# True if our own compose service already has a running container (a redeploy),
# in which case it legitimately holds the host port.
stack_owns_port() {
  local ids
  ids="$("${COMPOSE[@]}" -f "$COMPOSE_FILE" ps -q "$SERVICE" 2>/dev/null || true)"
  [[ -n "$ids" ]]
}

# --- Deploy ----------------------------------------------------------------

deploy() {
  local -a up=(up --detach --remove-orphans)
  ((DO_BUILD)) && up+=(--build)

  if ((SEED_DEV)); then
    warn "Dev scenario-seeding is ENABLED for this run (DEV_SCENARIO_SEEDING=true). Do not use on the VPS."
    export DEV_SCENARIO_SEEDING=true
  fi

  info "Starting the stack ($( ((DO_BUILD)) && echo 'building image, ' )detached)…"
  if ! "${COMPOSE[@]}" -f "$COMPOSE_FILE" "${up[@]}"; then
    die 5 "'compose up' failed. See the output above for the cause."
  fi
  ok "Container(s) started."
}

# --- Verify ----------------------------------------------------------------

verify() {
  # First confirm the service container is actually running (not exited/crashed).
  local state
  state="$("${COMPOSE[@]}" -f "$COMPOSE_FILE" ps --format '{{.State}}' "$SERVICE" 2>/dev/null | head -n1 || true)"
  if [[ "$state" != "running" ]]; then
    err "Service '${SERVICE}' is not running (state: '${state:-unknown}'). Recent logs:"
    "${COMPOSE[@]}" -f "$COMPOSE_FILE" logs --tail 40 "$SERVICE" || true
    die 6 "Container did not stay up."
  fi

  # Then poll the app's /health endpoint on the published loopback port.
  local url="http://127.0.0.1:${HOST_PORT}/health"
  local probe=()
  if command -v curl >/dev/null 2>&1; then
    probe=(curl -fsS --max-time 3 -o /dev/null "$url")
  elif command -v wget >/dev/null 2>&1; then
    probe=(wget -q -T 3 -O /dev/null "$url")
  else
    warn "Neither curl nor wget is available; skipping the HTTP health check. Container is running."
    return 0
  fi

  info "Waiting for ${url} (up to ${HEALTH_TIMEOUT}s)…"
  local deadline=$(( SECONDS + HEALTH_TIMEOUT ))
  until "${probe[@]}" >/dev/null 2>&1; do
    if (( SECONDS >= deadline )); then
      err "Health check did not pass within ${HEALTH_TIMEOUT}s. Recent logs:"
      "${COMPOSE[@]}" -f "$COMPOSE_FILE" logs --tail 40 "$SERVICE" || true
      die 6 "App is not healthy at ${url}."
    fi
    # Bail out early if the container died while we were waiting.
    state="$("${COMPOSE[@]}" -f "$COMPOSE_FILE" ps --format '{{.State}}' "$SERVICE" 2>/dev/null | head -n1 || true)"
    if [[ "$state" != "running" ]]; then
      err "Service '${SERVICE}' stopped while starting (state: '${state:-unknown}'). Recent logs:"
      "${COMPOSE[@]}" -f "$COMPOSE_FILE" logs --tail 40 "$SERVICE" || true
      die 6 "Container exited during startup."
    fi
    sleep 2
  done

  ok "Health check passed."
}

# --- Main ------------------------------------------------------------------

main() {
  cd "$REPO_ROOT"
  preflight
  deploy
  verify
  ok "Deploy complete. App is live at http://127.0.0.1:${HOST_PORT} (health: /health)."
  info "Follow logs with: ${COMPOSE[*]} -f docker-compose.yml logs -f ${SERVICE}"
}

main "$@"
