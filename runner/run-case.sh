#!/bin/sh
set -eu

WORKSPACE=/workspace
INPUT_TEXT="${INPUT_TEXT:-}"
EXPECTED_OUTPUT="${EXPECTED_OUTPUT:-}"
RUN_TIMEOUT_SECONDS="${RUN_TIMEOUT_SECONDS:-3}"

mkdir -p "$WORKSPACE"
cp -R /input/. "$WORKSPACE/"
find "$WORKSPACE" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
PROJECT_FILE="$(find "$WORKSPACE" -type f -name '*.csproj' -print -quit)"
test -n "$PROJECT_FILE"
cd "$(dirname "$PROJECT_FILE")"

if ! dotnet restore --disable-parallel --ignore-failed-sources --nologo; then
  echo "SMARTSCANNER_RESULT=RESTORE_FAILED"
  exit 20
fi

if ! dotnet build --no-restore --nologo; then
  echo "SMARTSCANNER_RESULT=BUILD_FAILED"
  exit 21
fi

set +e
ACTUAL_OUTPUT="$(printf '%b' "$INPUT_TEXT" | timeout --kill-after=1s "${RUN_TIMEOUT_SECONDS}s" dotnet run --no-build --no-restore 2>&1)"
RUN_EXIT_CODE=$?
set -e

printf '%s\n' "$ACTUAL_OUTPUT"

if [ "$RUN_EXIT_CODE" -eq 124 ]; then
  echo "SMARTSCANNER_RESULT=PROGRAM_TIMEOUT"
  exit 42
fi

if [ "$RUN_EXIT_CODE" -ne 0 ]; then
  echo "SMARTSCANNER_RESULT=PROGRAM_CRASHED"
  exit 43
fi

if [ "$ACTUAL_OUTPUT" != "$EXPECTED_OUTPUT" ]; then
  echo "SMARTSCANNER_RESULT=TEST_OUTPUT_MISMATCH"
  exit 44
fi

echo "SMARTSCANNER_RESULT=PASSED"
