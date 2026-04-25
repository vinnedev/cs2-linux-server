# AGENTS

## Purpose

This repository no longer assumes a pure vanilla server.

The current operational model is:

- CS2 server files are installed into `server/`
- the local vanilla mod stack is stored in `stack/vanilla/`
- `install.py` applies the stack automatically on first install
- `setup.py` reapplies the stack later without reinstalling the server
- custom plugins are built from `plugins_source/` and deployed separately

## Source Of Truth

Use these paths as the source of truth:

- `stack/vanilla/addons/` for vanilla `Metamod:Source` + `CounterStrikeSharp`
- `plugins_source/` for custom plugin code
- `server/game/csgo/addons/` for runtime deployed state

Do not treat `server/game/csgo/addons/` as the canonical editable source for the vanilla stack.

## Expected Workflows

First-time machine/bootstrap:

1. run `python3 install.py`
2. this installs CS2, patches `gameinfo.gi`, applies `stack/vanilla`, creates the configured `cfg/<EXEC>`, and deploys plugins

Existing server refresh:

1. run `python3 setup.py`
2. this reapplies `stack/vanilla` and rebuilds/redeploys plugins

Server start:

1. run `python3 start.py`

## Editing Rules

- If changing the baseline Metamod/CounterStrikeSharp payload, update `stack/vanilla/`.
- If changing plugin behavior, edit `plugins_source/`.
- If changing install behavior, prefer `scripts/mod_stack.py`, `install.py`, and `setup.py`.
- Keep `stack/vanilla/` free of custom plugins, generated logs, and runtime-generated config files.
- Preserve the `gameinfo.gi` patch behavior that injects `Game csgo/addons/metamod`.

## Validation

A healthy runtime boot should show:

- Metamod loads
- CounterStrikeSharp initializes
- the plugin loader loads `AutojoinPlugin`

If boot still fails after those lines, the remaining issue is likely server networking, token/config, or environment related rather than plugin load.
