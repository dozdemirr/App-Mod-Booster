#!/usr/bin/env bash
set -euo pipefail

MODE="${1:-app}"
if [[ "$MODE" == "chat" ]]; then
  bash ./deploy-with-chat.sh
else
  bash ./deploy.sh
fi
