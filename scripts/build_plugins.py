#!/usr/bin/env python3
from __future__ import annotations

import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _common import copytree_merge, ensure_dir, log, project_root, require_binary, run


@dataclass(frozen=True)
class Plugin:
    directory: str
    csproj: str
    name: str


PLUGINS: list[Plugin] = [
    Plugin("cs2-instadefuse", "InstadefusePlugin.csproj", "InstadefusePlugin"),
    Plugin("cs2-instaplant", "InstaplantPlugin.csproj", "InstaplantPlugin"),
    Plugin("cs2-clutch-announce", "ClutchAnnouncePlugin.csproj", "ClutchAnnouncePlugin"),
    Plugin("cs2-retakes", "RetakesPlugin/RetakesPlugin.csproj", "RetakesPlugin"),
    Plugin("cs2-css-inventory-simulator", "InventorySimulator.csproj", "InventorySimulator"),
]

DISABLED_NAMES: set[str] = set()
ROOT = project_root()
SRC_ROOT = ROOT / "plugins_source"
DEST_ROOT = ROOT / "server" / "game" / "csgo" / "addons" / "counterstrikesharp" / "plugins"


def dest_for(plugin: Plugin) -> Path:
    if plugin.name in DISABLED_NAMES:
        return DEST_ROOT / "disabled" / plugin.name
    return DEST_ROOT / plugin.name


def clean_dest(plugin: Plugin, dest: Path) -> None:
    preserved: Path | None = None
    if plugin.name == "RetakesPlugin":
        cfg = dest / "retakes_config.json"
        if cfg.is_file():
            tmp = Path(tempfile.mkstemp(prefix="retakes_cfg_", suffix=".json")[1])
            shutil.copy2(cfg, tmp)
            preserved = tmp
            log.info(f"Preserved {cfg}")

    if dest.exists():
        shutil.rmtree(dest)
    ensure_dir(dest)

    if preserved:
        shutil.move(str(preserved), dest / "retakes_config.json")


def copy_assets(plugin: Plugin, src: Path, dest: Path) -> None:
    if plugin.directory == "cs2-retakes":
        lang = src / "RetakesPlugin" / "lang"
        maps = src / "RetakesPlugin" / "map_config"
        if lang.is_dir():
            copytree_merge(lang, dest / "lang")
        if maps.is_dir():
            copytree_merge(maps, dest / "map_config")
        return

    if plugin.directory == "cs2-css-inventory-simulator":
        lang = src / "source" / "InventorySimulator" / "lang"
        if lang.is_dir():
            copytree_merge(lang, dest / "lang")

        gamedata = src / "gamedata"
        if gamedata.is_dir():
            copytree_merge(gamedata, DEST_ROOT.parent / "gamedata")
        return

    lang = src / "lang"
    if lang.is_dir():
        copytree_merge(lang, dest / "lang")


def dotnet_restore(project_path: Path) -> None:
    if not project_path.is_file():
        return
    run(["dotnet", "restore", str(project_path)], check=False, stream_prefix=project_path.stem)


def build_plugin(plugin: Plugin) -> bool:
    src = SRC_ROOT / plugin.directory
    project = src / plugin.csproj
    if not project.is_file():
        log.warn(f"SKIP {plugin.name}: {project} not found")
        return False

    dest = dest_for(plugin)
    out = src / "build" / plugin.name

    log.section(f"Building {plugin.name}")
    try:
        with log.task(f"dotnet build {plugin.name} (no-restore)"):
            run([
                "dotnet", "build", str(project), "-c", "Release",
                "-o", str(out), "--no-restore",
            ], stream_prefix=plugin.name)
    except subprocess.CalledProcessError:
        log.warn(f"{plugin.name} first attempt failed — retrying with restore")
        with log.task(f"dotnet build {plugin.name}"):
            run([
                "dotnet", "build", str(project), "-c", "Release", "-o", str(out),
            ], stream_prefix=plugin.name)

    with log.task(f"Deploying {plugin.name} -> {dest}"):
        clean_dest(plugin, dest)
        copytree_merge(out, dest)
        copy_assets(plugin, src, dest)

    log.ok(f"{plugin.name} deployed")
    return True


def build_all() -> None:
    require_binary("dotnet", hint="Install .NET 8.0 SDK (see runtime_net_install.py).")
    log.section("Restoring NuGet packages")
    for p in PLUGINS:
        dotnet_restore(SRC_ROOT / p.directory / p.csproj)

    built = 0
    for p in PLUGINS:
        if build_plugin(p):
            built += 1
    log.ok(f"Build finished: {built}/{len(PLUGINS)} plugins deployed")


def watch_plugin(directory: str) -> None:
    require_binary("dotnet")
    for p in PLUGINS:
        if p.directory == directory:
            dest = dest_for(p)
            ensure_dir(dest)
            log.section(f"Watching {p.directory} -> {dest}")
            run([
                "dotnet", "watch", "build",
                "--project", str(SRC_ROOT / p.directory / p.csproj),
                f"--property:OutDir={dest}",
            ], stream_prefix=p.name)
            return
    log.error(f"Plugin '{directory}' not found")
    sys.exit(1)


def build_single(directory: str) -> None:
    require_binary("dotnet")
    for p in PLUGINS:
        if p.directory == directory:
            dotnet_restore(SRC_ROOT / p.directory / p.csproj)
            build_plugin(p)
            return
    log.error(f"Plugin '{directory}' not found")
    log.info("Available:")
    for p in PLUGINS:
        log.info(f"  {p.directory}")
    sys.exit(1)


def main() -> None:
    argv = sys.argv[1:]
    target = argv[0] if argv else "all"

    if target == "all":
        build_all()
    elif target == "watch":
        if len(argv) < 2:
            log.error("Usage: build_plugins.py watch <plugin-dir>")
            log.info("Available:")
            for p in PLUGINS:
                log.info(f"  {p.directory}")
            sys.exit(1)
        watch_plugin(argv[1])
    else:
        build_single(target)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        log.warn("Interrupted")
        sys.exit(130)
