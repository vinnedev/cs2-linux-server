#!/bin/bash
set -e

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PLUGINS_SRC="$ROOT_DIR/plugins_source"
PLUGINS_DEST="$ROOT_DIR/components/csgo/addons/counterstrikesharp/plugins"

PLUGINS=(
    "cs2-autojoin:AutojoinPlugin.csproj:AutojoinPlugin"
    "cs2-instadefuse:InstadefusePlugin.csproj:InstadefusePlugin"
    "cs2-instaplant:InstaplantPlugin.csproj:InstaplantPlugin"
    "cs2-clutch-announce:ClutchAnnouncePlugin.csproj:ClutchAnnouncePlugin"
    "cs2-retakes:RetakesPlugin/RetakesPlugin.csproj:RetakesPlugin"
    "cs2-inventory-simulator-plugin:InventorySimulator.csproj:InventorySimulator"
)

get_dest_path() {
    local name="$1"

    case "$name" in
        RetakesPlugin|InstadefusePlugin|ClutchAnnouncePlugin)
            echo "$PLUGINS_DEST/disabled/$name"
            ;;
        *)
            echo "$PLUGINS_DEST/$name"
            ;;
    esac
}

clean_dest_path() {
    local name="$1"
    local dest_path="$2"
    local preserved_file=""

    if [ -d "$dest_path" ]; then
        if [ "$name" = "RetakesPlugin" ] && [ -f "$dest_path/retakes_config.json" ]; then
            preserved_file="$(mktemp)"
            cp "$dest_path/retakes_config.json" "$preserved_file"
        fi

        rm -rf "$dest_path"
    fi

    mkdir -p "$dest_path"

    if [ -n "$preserved_file" ]; then
        mv "$preserved_file" "$dest_path/retakes_config.json"
    fi
}

copy_plugin_assets() {
    local dir="$1"
    local src_path="$2"
    local dest_path="$3"

    case "$dir" in
        cs2-retakes)
            if [ -d "$src_path/RetakesPlugin/lang" ]; then
                mkdir -p "$dest_path/lang"
                cp -R "$src_path/RetakesPlugin/lang/." "$dest_path/lang/"
            fi

            if [ -d "$src_path/RetakesPlugin/map_config" ]; then
                mkdir -p "$dest_path/map_config"
                cp -R "$src_path/RetakesPlugin/map_config/." "$dest_path/map_config/"
            fi
            ;;
        *)
            if [ -d "$src_path/lang" ]; then
                mkdir -p "$dest_path/lang"
                cp -R "$src_path/lang/." "$dest_path/lang/"
            fi
            ;;
    esac
}

build_plugin() {
    local dir="$1"
    local csproj="$2"
    local name="$3"
    local src_path="$PLUGINS_SRC/$dir"
    local project_path="$src_path/$csproj"
    local dest_path
    dest_path="$(get_dest_path "$name")"

    if [ ! -f "$project_path" ]; then
        echo "SKIP: $project_path not found"
        return
    fi

    echo "Building $name..."
    dotnet build "$project_path" -c Release -o "$src_path/build/$name" --no-restore 2>/dev/null || \
    dotnet build "$project_path" -c Release -o "$src_path/build/$name"

    clean_dest_path "$name" "$dest_path"
    cp -R "$src_path/build/$name/." "$dest_path/"
    copy_plugin_assets "$dir" "$src_path" "$dest_path"

    echo "Deployed $name -> $dest_path"
}

restore_all() {
    echo "Restoring NuGet packages..."
    for entry in "${PLUGINS[@]}"; do
        IFS=':' read -r dir csproj name <<< "$entry"
        local project_path="$PLUGINS_SRC/$dir/$csproj"
        [ -f "$project_path" ] && dotnet restore "$project_path" 2>/dev/null || true
    done
}

TARGET="${1:-all}"

if [ "$TARGET" = "all" ]; then
    restore_all
    for entry in "${PLUGINS[@]}"; do
        IFS=':' read -r dir csproj name <<< "$entry"
        build_plugin "$dir" "$csproj" "$name"
    done
    echo "All plugins built and deployed."
elif [ "$TARGET" = "watch" ]; then
    PLUGIN="${2:-}"
    if [ -z "$PLUGIN" ]; then
        echo "Usage: $0 watch <plugin-dir>"
        echo "Available: ${PLUGINS[*]}"
        exit 1
    fi
    for entry in "${PLUGINS[@]}"; do
        IFS=':' read -r dir csproj name <<< "$entry"
        if [ "$dir" = "$PLUGIN" ]; then
            dest_path="$(get_dest_path "$name")"
            mkdir -p "$dest_path"
            echo "Watching $dir -> $dest_path"
            dotnet watch build --project "$PLUGINS_SRC/$dir/$csproj" --property:OutDir="$dest_path"
            exit 0
        fi
    done
    echo "Plugin '$PLUGIN' not found."
    exit 1
else
    for entry in "${PLUGINS[@]}"; do
        IFS=':' read -r dir csproj name <<< "$entry"
        if [ "$dir" = "$TARGET" ]; then
            dotnet restore "$PLUGINS_SRC/$dir/$csproj" 2>/dev/null || true
            build_plugin "$dir" "$csproj" "$name"
            exit 0
        fi
    done
    echo "Plugin '$TARGET' not found."
    echo "Available:"
    for entry in "${PLUGINS[@]}"; do
        IFS=':' read -r dir csproj _ <<< "$entry"
        echo "  $dir"
    done
    exit 1
fi
