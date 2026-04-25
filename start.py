#!/usr/bin/env python3
from __future__ import annotations

import os
import signal
import subprocess
import sys
import threading
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "scripts"))

from _common import (  # noqa: E402
    export_env,
    is_docker,
    load_env_file,
    log,
    project_root,
    run,
    sudo_prefix,
    which,
)


DEFAULTS = {
    "PORT": "27015",
    "IP": "0.0.0.0",
    "MAXPLAYERS": "10",
    "API_KEY": "",
    "STEAM_ACCOUNT": "",
    "LAN": "0",
    "SERVER_PASSWORD": "",
    "RCON_PASSWORD": "12345678",
    "EXEC": "autoexec.cfg",
}

FIXED_TICKRATE = "128"
PLUGIN_EXEC_CFG = "cs2-retakes/retakes.cfg"


def load_environment(root: Path) -> None:
    env_file = root / ".env"
    if env_file.is_file():
        log.info(f"Loading environment from {env_file}")
        export_env(load_env_file(env_file))
    else:
        log.warn(".env not found; falling back to process environment")

    for key, value in DEFAULTS.items():
        os.environ.setdefault(key, value)


def open_firewall_ports(port: str) -> None:
    if is_docker():
        log.info("Docker environment detected; skipping UFW setup")
        return
    ufw = which("ufw")
    if not ufw:
        log.warn("UFW not installed; skipping firewall configuration")
        return
    sudo = sudo_prefix()
    try:
        result = run(sudo + [ufw, "status"], capture=True, check=False)
        current = result.stdout or ""
    except Exception as exc:
        log.warn(f"Unable to query UFW status: {exc}")
        return

    for p in [f"{port}/tcp", f"{port}/udp", "27020/tcp", "27020/udp"]:
        if p in current:
            log.ok(f"Firewall already allows {p}")
            continue
        with log.task(f"Opening firewall port {p}"):
            run(sudo + [ufw, "allow", p], check=False)


def build_cs2_command(root: Path, cs2_bin: Path) -> list[str]:
    env = os.environ
    cmd = [
        str(cs2_bin),
        "-game", "csgo",
        "-dedicated",
        "-console",
        "-usercon",
        "+game_type", "0",
        "+game_mode", "0",
        "+mapgroup", "mg_active",
        "+map", "de_mirage",
        "-port", env["PORT"],
        "-ip", env["IP"],
        "+net_public_adr", env["IP"],
        "-tickrate", FIXED_TICKRATE,
        "+sv_visiblemaxplayers", env["MAXPLAYERS"],
        "-authkey", env.get("API_KEY", ""),
        "+sv_setsteamaccount", env.get("STEAM_ACCOUNT", ""),
        "+sv_lan", env["LAN"],
        "+sv_password", env.get("SERVER_PASSWORD", ""),
        "+rcon_password", env.get("RCON_PASSWORD", "12345678"),
        "+exec", env.get("EXEC", "autoexec.cfg"),
    ]

    startup_exec = env.get("EXEC", "autoexec.cfg").removeprefix("cfg/").strip()
    if startup_exec != PLUGIN_EXEC_CFG:
        cmd.extend(["+exec", PLUGIN_EXEC_CFG])

    return cmd


def main() -> None:
    root = project_root()
    log.section("CS2 server launcher")
    log.info(f"Project root: {root}")
    log.info(f"Running in Docker: {is_docker()}")

    load_environment(root)

    default_cs2 = root / "server" / "game" / "bin" / "linuxsteamrt64" / "cs2"
    cs2_bin = Path(os.environ.get("CS2_PATH") or default_cs2)
    if not cs2_bin.is_file():
        log.error(f"CS2 binary not found at {cs2_bin}. Run ./install.py first.")
        sys.exit(1)
    if not os.access(cs2_bin, os.X_OK):
        os.chmod(cs2_bin, 0o755)

    log.section("Firewall")
    open_firewall_ports(os.environ["PORT"])

    game_bin = cs2_bin.parent
    ld_paths = [
        str(game_bin),
        str(root / "steamcmd" / "linux64"),
        os.environ.get("LD_LIBRARY_PATH", ""),
    ]
    os.environ["LD_LIBRARY_PATH"] = ":".join(p for p in ld_paths if p)
    log.info(f"LD_LIBRARY_PATH={os.environ['LD_LIBRARY_PATH']}")

    cmd = build_cs2_command(root, cs2_bin)
    port = os.environ["PORT"]
    log.section(f"Starting CS2 on {os.environ['IP']}:{port}")
    log.info(f"Binary: {cs2_bin}")
    log.debug("cmd: " + " ".join(cmd))

    server_root = root / "server"
    proc = subprocess.Popen(cmd, cwd=server_root, stdin=subprocess.PIPE, bufsize=0)

    def forward(signum, _frame):
        log.warn(f"Forwarding signal {signum} to CS2 process {proc.pid}")
        proc.send_signal(signum)

    signal.signal(signal.SIGINT, forward)
    signal.signal(signal.SIGTERM, forward)

    def pipe_stdin() -> None:
        try:
            for line in sys.stdin:
                if proc.poll() is not None or proc.stdin is None:
                    break
                try:
                    proc.stdin.write(line.encode())
                    proc.stdin.flush()
                except (BrokenPipeError, OSError):
                    break
        except Exception as exc:
            log.warn(f"stdin forwarder stopped: {exc}")

    if sys.stdin and sys.stdin.isatty():
        log.info("Console interativo ativo — digite comandos do servidor (ex.: status, meta list)")
        threading.Thread(target=pipe_stdin, daemon=True).start()
    else:
        log.info("stdin não é TTY; pulando console interativo")

    rc = proc.wait()
    if rc == 0:
        log.ok("CS2 exited cleanly")
    else:
        log.error(f"CS2 exited with status {rc}")
    sys.exit(rc)


if __name__ == "__main__":
    main()
