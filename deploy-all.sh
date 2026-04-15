#!/usr/bin/env bash
set -euo pipefail

TARGET=${1:-app}

case "$TARGET" in
  app)
    bash deploy.sh
    ;;
  chat)
    bash deploy-with-chat.sh
    ;;
  *)
    echo "Usage: bash deploy-all.sh [app|chat]"
    exit 1
    ;;
esac
