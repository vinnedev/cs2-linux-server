from __future__ import annotations

import json
import os
import sys
from pathlib import Path

from _common import ensure_dir, fail, log, project_root, run, which


ROOT = project_root()
STACK_ROOT = ROOT / "stack" / "vanilla"
STACK_ADDONS = STACK_ROOT / "addons"
EXPECTED_STACK_FILES = [
    "metamod.vdf",
    "metamod_x64.vdf",
    "metamod/counterstrikesharp.vdf",
    "metamod/bin/linuxsteamrt64/metamod.2.cs2.so",
    "counterstrikesharp/bin/linuxsteamrt64/counterstrikesharp.so",
    "counterstrikesharp/dotnet/dotnet",
    "counterstrikesharp/api/CounterStrikeSharp.API.runtimeconfig.json",
]
METAMOD_VDF = '"Plugin"\n{\n\t"file"\t"addons/metamod/bin/server"\n}\n'
METAMOD_X64_VDF = '"Plugin"\n{\n\t"file"\t"addons/metamod/bin/linux64/server"\n}\n'
CSS_VDF = (
    '"Metamod Plugin"\n'
    "{\n"
    '\t"alias"\t"counterstrikesharp"\n'
    '\t"file"\t"addons/counterstrikesharp/bin/linuxsteamrt64/counterstrikesharp"\n'
    "}\n"
)
METAPLUGINS_INI = (
    ";If your plugin came with a .vdf file, you do not need to use this file.\n"
    ";\n"
    ";List one plugin per line.  Each line should contain the path to the plugin's binary.\n"
    ";Any line starting with a ';' character is a comment line, and is ignored.\n"
    ";\n"
    ";You do not need to include the _i486.so or .dll part of the file name.  Example:\n"
    "; addons/sourcemod/bin/sourcemod_mm\n"
    ";You may also put an alias in front of the file, for example:\n"
    "; sm addons/sourcemod/bin/sourcemod_mm\n"
    ';Will allow you to use "meta load sm" from the console.\n'
    ";\n"
    ";********* LIST PLUGINS BELOW ***********\n"
)


def patch_gameinfo(gameinfo: Path) -> None:
    text = gameinfo.read_text()
    if "Game csgo/addons/metamod" in text or "Game\tcsgo/addons/metamod" in text:
        log.ok("gameinfo.gi already patched for Metamod")
        return

    markers = [
        "Game_LowViolence\tcsgo_lv // Perfect World content override",
        "Game_LowViolence\t\tcsgo_lv // Perfect World content override",
        "Game_LowViolence\t\tcsgo_lv",
        "Game_LowViolence\tcsgo_lv",
    ]

    for marker in markers:
        if marker in text:
            text = text.replace(marker, marker + "\n\n\t\t\tGame csgo/addons/metamod", 1)
            gameinfo.write_text(text)
            log.ok(f"Patched {gameinfo}")
            return

    fail(f"Could not find SearchPaths insertion point in {gameinfo}")


def validate_local_stack_source() -> None:
    if not STACK_ADDONS.is_dir():
        fail(f"Local vanilla stack not found at {STACK_ADDONS}")

    missing = [rel for rel in EXPECTED_STACK_FILES if not (STACK_ADDONS / rel).exists()]
    if missing:
        fail(
            "Local vanilla stack is incomplete. Missing files:\n"
            + "\n".join(f"- {path}" for path in missing)
        )


def apply_local_stack(csgo_dir: Path) -> None:
    validate_local_stack_source()
    ensure_dir(csgo_dir / "addons")
    run(["cp", "-a", f"{STACK_ADDONS}/.", str(csgo_dir / "addons")])
    normalize_runtime_stack(csgo_dir)
    fix_exec_bits(csgo_dir)
    validate_runtime_stack(csgo_dir)
    log.ok("Applied local Metamod + CounterStrikeSharp stack")


def write_if_different(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_text() == content:
        return
    path.write_text(content)


def normalize_runtime_stack(csgo_dir: Path) -> None:
    addons = csgo_dir / "addons"
    write_if_different(addons / "metamod.vdf", METAMOD_VDF)
    write_if_different(addons / "metamod_x64.vdf", METAMOD_X64_VDF)
    write_if_different(addons / "metamod" / "counterstrikesharp.vdf", CSS_VDF)
    write_if_different(addons / "metamod" / "metaplugins.ini", METAPLUGINS_INI)
    ensure_css_core_config(addons / "counterstrikesharp" / "configs")


def ensure_css_core_config(configs_dir: Path) -> None:
    core_path = configs_dir / "core.json"
    core_example_path = configs_dir / "core.example.json"

    if core_path.exists():
        try:
            core = json.loads(core_path.read_text())
        except json.JSONDecodeError:
            core = {}
    elif core_example_path.exists():
        core = json.loads(core_example_path.read_text())
    else:
        core = {}

    changed = False

    if core.get("FollowCS2ServerGuidelines") is not False:
        core["FollowCS2ServerGuidelines"] = False
        changed = True

    if core.get("ServerLanguage") != "pt-BR":
        core["ServerLanguage"] = "pt-BR"
        changed = True

    if not changed:
        return

    core_path.parent.mkdir(parents=True, exist_ok=True)
    core_path.write_text(json.dumps(core, indent=4) + "\n")
    log.ok(f"Configured {core_path} with FollowCS2ServerGuidelines=false and ServerLanguage=pt-BR")


def fix_exec_bits(csgo_dir: Path) -> None:
    css_root = csgo_dir / "addons" / "counterstrikesharp"
    if not css_root.exists():
        return

    dotnet_host = css_root / "dotnet" / "dotnet"
    if dotnet_host.exists():
        dotnet_host.chmod(0o755)

    for path in css_root.rglob("*.so"):
        path.chmod(0o755)

    for path in (csgo_dir / "addons" / "metamod").rglob("*.so"):
        path.chmod(0o755)


def validate_runtime_stack(csgo_dir: Path) -> None:
    addons = csgo_dir / "addons"
    missing = [rel for rel in EXPECTED_STACK_FILES if not (addons / rel).exists()]
    if missing:
        fail(
            "Runtime Metamod/CounterStrikeSharp stack is incomplete after apply_local_stack(). Missing files:\n"
            + "\n".join(f"- {path}" for path in missing)
        )

    css_vdf = addons / "metamod" / "counterstrikesharp.vdf"
    if css_vdf.read_text() != CSS_VDF:
        fail(f"Unexpected CounterStrikeSharp VDF contents at {css_vdf}")

    game_bin = csgo_dir.parent / "bin" / "linuxsteamrt64"
    required_game_libs = ["libtier0.so", "libengine2.so"]
    missing_libs = [name for name in required_game_libs if not (game_bin / name).exists()]
    if missing_libs:
        fail(
            "Required CS2 Linux runtime libraries are missing:\n"
            + "\n".join(f"- {name}" for name in missing_libs)
        )


def ensure_exec_cfg(csgo_dir: Path, exec_name: str) -> None:
    if not exec_name:
        exec_name = "autoexec.cfg"

    cfg_name = exec_name if exec_name.endswith(".cfg") else f"{exec_name}.cfg"
    cfg_path = csgo_dir / "cfg" / cfg_name
    ensure_cfg_file(cfg_path, "// Created automatically by install.py\n")

    retakes_cfg_path = csgo_dir / "cfg" / "cs2-retakes" / "retakes.cfg"
    ensure_cfg_file(
        retakes_cfg_path,
        "// Created automatically by install.py for RetakesPlugin startup override.\n",
    )


def ensure_cfg_file(path: Path, content: str) -> None:
    if path.exists():
        return

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content)
    log.ok(f"Created missing {path.relative_to(path.parents[2])}")


def maybe_build_plugins() -> None:
    if os.environ.get("BUILD_PLUGINS", "1") == "0":
        log.info("Skipping plugin build because BUILD_PLUGINS=0")
        return
    if not which("dotnet"):
        log.warn("dotnet not found; skipping plugin build")
        return

    run([sys.executable, str(ROOT / "scripts" / "build_plugins.py"), "all"])
