#!/usr/bin/env bash
# Stop anything on the app ports, then run AracParki.Web with hot reload (HTTPS).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WEB="$ROOT/src/AracParki.Web"
PORTS=(7133 5245)

free_port() {
  local port="$1"
  local pids
  pids="$(lsof -nP -iTCP:"$port" -sTCP:LISTEN -t 2>/dev/null || true)"
  if [[ -z "$pids" ]]; then
    echo "Port $port: free"
    return 0
  fi

  echo "Port $port: stopping PID(s) $pids"
  # shellcheck disable=SC2086
  kill $pids 2>/dev/null || true
  sleep 0.4

  pids="$(lsof -nP -iTCP:"$port" -sTCP:LISTEN -t 2>/dev/null || true)"
  if [[ -n "$pids" ]]; then
    echo "Port $port: force-killing PID(s) $pids"
    # shellcheck disable=SC2086
    kill -9 $pids 2>/dev/null || true
  fi
}

for port in "${PORTS[@]}"; do
  free_port "$port"
done

cd "$WEB"
echo "Starting: dotnet watch (https → https://localhost:7133)"
exec dotnet watch --launch-profile https
