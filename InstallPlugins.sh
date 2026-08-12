#!/bin/bash
# One-command local install for testing:
#   1. copies the built DLLs into <root>/Plugins/ and <root>/Libraries/
#      (top level - the layout the plugin manager's manual scan expects)
#   2. removes earlier catalogue-style manifest entries to avoid double loading
#
# Usage: ./InstallPlugins.sh [ETS2LA root]
#   ETS2LA root = the folder containing 'Plugins' and 'Libraries'
#                 (Linux: ~/.local/share/ETS2LA). Defaults to the script's dir.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="${1:-$SCRIPT_DIR}"
DIST="$SCRIPT_DIR/dist"

if [ ! -d "$ROOT/Plugins" ] || [ ! -d "$ROOT/Libraries" ]; then
  echo "Error: $ROOT does not look like an ETS2LA root (no Plugins/Libraries folders)." >&2
  exit 1
fi

cp "$DIST/Plugins/srlily.i18n/srlily.i18n.dll" "$ROOT/Plugins/srlily.i18n.dll"
cp "$DIST/Libraries/srlily.i18n.library/srlily.i18n.library.dll" "$ROOT/Libraries/srlily.i18n.library.dll"
echo "Installed $ROOT/Plugins/srlily.i18n.dll"
echo "Installed $ROOT/Libraries/srlily.i18n.library.dll"

CONFIG_DIR="${XDG_CONFIG_HOME:-$HOME/.config}/ETS2LA"
MANIFEST="$CONFIG_DIR/InstalledPluginManifest.json"

if [ -f "$MANIFEST" ]; then
  python3 - "$MANIFEST" << 'EOF'
import json, sys
p = sys.argv[1]
with open(p, encoding="utf-8") as f:
    m = json.load(f)
plugins = m.get("InstalledPlugins", [])
before = len(plugins)
m["InstalledPlugins"] = [x for x in plugins if x.get("Id") not in ("srlily.i18n", "srlily.i18n.library")]
if len(m["InstalledPlugins"]) != before:
    with open(p, "w", encoding="utf-8") as f:
        json.dump(m, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"Removed {before - len(m['InstalledPlugins'])} manifest entries (avoid duplicate loading).")
EOF
fi

echo ""
echo "Done. Restart ETS2LA now, the plugins will be discovered."
echo "If they still do not appear, check ~/.local/share/ETS2LA/ets2la.log"
echo "for 'Loaded plugin' / 'Failed to load plugin' lines."