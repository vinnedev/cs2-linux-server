# Vanilla Stack

This directory contains the local vanilla baseline for:

- `Metamod:Source`
- `CounterStrikeSharp`

It is applied by:

- `python3 install.py` after the CS2 dedicated server is installed
- `python3 setup.py` for an already-installed server

What this folder contains:

- `addons/` only

What this folder does not contain:

- custom plugins such as `AutojoinPlugin`
- generated logs
- runtime-generated config files

Custom plugins are built from `plugins_source/` and deployed after the vanilla stack is copied into `server/game/csgo/addons/`.
