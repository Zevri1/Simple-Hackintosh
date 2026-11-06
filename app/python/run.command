#!/bin/zsh
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PYTHON_BIN=""

for candidate in python3.12 python3.11 python3.10 python3; do
    if command -v "$candidate" >/dev/null 2>&1; then
        if "$candidate" -c 'import sys; raise SystemExit(0 if sys.version_info >= (3, 10) else 1)' >/dev/null 2>&1; then
            PYTHON_BIN="$(command -v "$candidate")"
            break
        fi
    fi
done

if [ -z "$PYTHON_BIN" ]; then
    echo "Python 3.10 or newer was not found."
    echo "Install Python 3 from https://www.python.org/downloads/macos/"
    echo
    if [ -z "${SIMPLE_HACKINTOSH_NO_PAUSE:-}" ]; then
        printf "Press Return to close this window..."
        read -r _response
    fi
    exit 2
fi

"$PYTHON_BIN" "$SCRIPT_DIR/simple_hackintosh.py" "$@"
STATUS=$?

if [ -z "${SIMPLE_HACKINTOSH_NO_PAUSE:-}" ]; then
    echo
    printf "Finished with exit code %s. Press Return to close this window..." "$STATUS"
    read -r _response
fi

exit "$STATUS"
