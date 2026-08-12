#!/bin/bash
# Registers the srlily.i18n plugins in ETS2LA's InstalledPluginManifest.json.
# This is required for the subfolder layout (Plugins/<pluginId>/<pluginId>.dll),
# which is otherwise only used by the plugin catalogue.
#
# Usage: ./RegisterPlugins.sh [ETS2LA root]
#   ETS2LA root = the folder containing 'Plugins' and 'Libraries'
#                 (Linux: ~/.local/share/ETS2LA). Defaults to the current directory.

set -euo pipefail

ROOT="${1:-$PWD}"
CONFIG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/ETS2LA"
MANIFEST="$CONFIG_DIR/InstalledPluginManifest.json"

LIB_DLL="$ROOT/Libraries/srlily.i18n.library/srlily.i18n.library.dll"
PLUGIN_DLL="$ROOT/Plugins/srlily.i18n/srlily.i18n.dll"

if [ ! -f "$LIB_DLL" ] || [ ! -f "$PLUGIN_DLL" ]; then
  echo "Error: plugin DLLs not found in $ROOT" >&2
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "Error: python3 is required" >&2
  exit 1
fi

mkdir -p "$CONFIG_DIR"

python3 - "$MANIFEST" "$LIB_DLL" "$PLUGIN_DLL" << 'EOF'
import json
import sys

manifest_file, lib_dll, plugin_dll = sys.argv[1], sys.argv[2], sys.argv[3]

entries = {
    "srlily.i18n.library": {
        "Id": "srlily.i18n.library",
        "Version": "1.1.3",
        "DllPath": lib_dll,
        "Dependencies": [],
        "Type": 1,
    },
    "srlily.i18n": {
        "Id": "srlily.i18n",
        "Version": "1.1.3",
        "DllPath": plugin_dll,
        "Dependencies": ["srlily.i18n.library"],
        "Type": 0,
    },
}

try:
    with open(manifest_file, encoding="utf-8") as f:
        manifest = json.load(f)
except (FileNotFoundError, json.JSONDecodeError):
    manifest = {}

installed = manifest.get("InstalledPlugins", [])
by_id = {p.get("Id"): p for p in installed}
for plugin_id, entry in entries.items():
    if plugin_id in by_id:
        print(f"Updated  {plugin_id}")
    else:
        print(f"Added    {plugin_id}")
    by_id[plugin_id] = entry

manifest["InstalledPlugins"] = list(by_id.values())
with open(manifest_file, "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2, ensure_ascii=False)
    f.write("\n")

print(f"\nManifest updated: {manifest_file}")
print("Restart ETS2LA for the plugins to be discovered.")
EOF
