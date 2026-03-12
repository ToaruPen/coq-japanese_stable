#!/usr/bin/env bash
# Double-click this file to launch Caves of Qud under Rosetta 2.
# Delegates to scripts/launch_rosetta.sh for all logic.

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec "${SCRIPT_DIR}/scripts/launch_rosetta.sh"
