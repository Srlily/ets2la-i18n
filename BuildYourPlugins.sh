#!/bin/bash

# Libraries and Plugins are in separate folders, so we need to loop through them both
for root in Libraries Plugins; do
  for d in "$root"/*; do
    [ -d "$d" ] || continue
    name="$(basename "$d")"
    if [ -f "$d/$name.csproj" ]; then
      dotnet build "$d/$name.csproj"
      # AssemblyName matches the plugin id (e.g. srlily.i18n), which is also the
      # install folder name. Fall back to the project folder name if unset.
      id="$(grep -oP '(?<=<AssemblyName>)[^<]+' "$d/$name.csproj" | head -1)"
      id="${id:-$name}"
      # Installed layout matches the plugin catalogue:
      #   <ETS2LA data>/Plugins/<pluginId>/<pluginId>.dll
      #   <ETS2LA data>/Libraries/<pluginId>/<pluginId>.dll
      out_dir="${XDG_DATA_HOME:-$HOME/.local/share}/ETS2LA/$root/$id"
      mkdir -p "$out_dir"
      rm -f "$out_dir/$id.dll" 2>/dev/null
      cp "$d/bin/Debug/net10.0/$id.dll" "$out_dir/$id.dll"
    fi
  done
done
