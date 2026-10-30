"""Download + install round trips of the launcher in scratch folders (never a real game install).

usage: python tests/test_launcher.py [--exe <launcher exe>] [--rad-bink <the game's own RAD bink2w64.dll>]
For each game with a GitLab release: --fetch it, build a fake Legendary Edition + Archipelago folder, --install from the
downloaded payload, require --check to pass, --uninstall, and require every file to be byte-identical to before.
ME2/ME3 need a real RAD bink2w64.dll (the installer checks it is the game's own before adding the ASI loader); without
--rad-bink they are skipped. Also checks that --write-yaml writes every preset.
"""
import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
SRC = ROOT / "src" / "MassEffectAPLauncher"
EXE = SRC / "bin" / "Release" / "net10.0-windows" / "Mass Effect Archipelago Launcher.exe"
GAMES = {"ME1": "MassEffect1.exe", "ME2": "MassEffect2.exe", "ME3": "MassEffect3.exe"}


def tree(root: pathlib.Path) -> dict[str, str]:
    return {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() if p.is_file() else "<dir>"
            for p in sorted(root.rglob("*"))}


def write(path: pathlib.Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def run(exe: pathlib.Path, *args: str, ok: bool = True) -> str:
    result = subprocess.run([str(exe), *args], capture_output=True, text=True)
    if (result.returncode == 0) != ok:
        raise AssertionError(f"{args[0]} {args[1] if len(args) > 1 else ''}: exit {result.returncode}\n{result.stdout}{result.stderr}")
    return result.stdout + result.stderr


def round_trip(exe: pathlib.Path, game: str, rad: pathlib.Path | None, base: pathlib.Path) -> list[str]:
    latest = subprocess.run([str(exe), "--latest", game], capture_output=True, text=True)
    if latest.returncode != 0:
        print(f"  {game}: skipped ({latest.stdout.strip() or 'no GitLab release'})")
        return []
    if game != "ME1" and rad is None:
        print(f"  {game}: skipped (no --rad-bink)")
        return []
    payload = pathlib.Path(run(exe, "--fetch", game).strip().splitlines()[-1])
    le, ap, state = base / "LE", base / "AP", base / "state"
    win64 = le / "Game" / game / "Binaries" / "Win64"
    write(win64 / GAMES[game], b"exe")
    write(le / "Game" / game / "BioGame" / "CookedPCConsole" / "SFXGame.pcc", b"game")
    write(ap / "ArchipelagoLauncher.exe", b"launcher")
    if game == "ME1":
        # Mod Manager's one-time ASI step: a loader and the LE1 Autoload Enabler
        write(win64 / "bink2w64.dll", b"loader")
        write(win64 / "bink2w64_original.dll", b"rad")
        write(win64 / "ASI" / "LE1AutoloadEnabler-v1.asi", b"autoload")
        me = le / "Game" / "ME1"
        write(me / "BioGame" / "Movies" / "GLO_01_Relay.bik", b"relay video" * 100)
        write(me / "BioGame" / "Movies" / "UCW_Loading_Flyby.bik", b"flyby video" * 100)
        write(me / "BioGame" / "PCConsoleTOC.bin", b"vanilla toc")
        write(me / "Engine" / "Shaders" / "ShaderCache.upk", b"shaders")   # the LE1 game TOC also lists Engine\Shaders
    else:
        shutil.copy(rad, win64 / "bink2w64.dll")
    common = ["--game", str(le), "--ap", str(ap), "--payload", str(payload), "--state", str(state)]
    failures = []
    before = {**{"LE/" + k: v for k, v in tree(le).items()}, **{"AP/" + k: v for k, v in tree(ap).items()}}
    run(exe, "--install", game, *common)
    if (ap / "custom_worlds" / next(payload.glob("*.apworld")).name).read_bytes() != next(payload.glob("*.apworld")).read_bytes():
        failures.append(f"{game}: apworld not installed")
    if "ready to launch" not in run(exe, "--check", game, *common):
        failures.append(f"{game}: --check not ready after install")
    run(exe, "--install", game, *common)   # repair
    run(exe, "--uninstall", game, *common)
    after = {**{"LE/" + k: v for k, v in tree(le).items()}, **{"AP/" + k: v for k, v in tree(ap).items()}}
    if before != after:
        failures.append(f"{game}: uninstall left {sum(1 for k in before.keys() | after.keys() if before.get(k) != after.get(k))} difference(s)")
    print(f"  {game} download + install + repair + uninstall: {'ok' if not failures else 'FAILED'}")
    return failures


def yamls(exe: pathlib.Path, base: pathlib.Path) -> list[str]:
    failures = []
    for game in GAMES:
        schema = json.loads((SRC / f"options-{game.lower()}.json").read_text(encoding="utf-8"))
        for preset in ["Default", *schema["presets"]]:
            out = base / f"{game}-{preset}.yaml"
            run(exe, "--write-yaml", game, str(out), "Shepard", preset, "--payload", str(base / "none"))
            text = out.read_text(encoding="utf-8")
            if f"game: '{schema['game']}'" not in text or any(f"  {o['key']}:" not in text for g in schema["groups"] for o in g["options"]):
                failures.append(f"{game} {preset}: YAML incomplete")
    print(f"  YAML for every preset: {'ok' if not failures else 'FAILED'}")
    return failures


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--exe", default=EXE, type=pathlib.Path)
    parser.add_argument("--rad-bink", type=pathlib.Path)
    args = parser.parse_args()
    failures = []
    with tempfile.TemporaryDirectory(prefix="melauncher-") as scratch:
        base = pathlib.Path(scratch)
        failures += yamls(args.exe, base)
        for game in GAMES:
            failures += round_trip(args.exe, game, args.rad_bink, base / game)
    for f in failures:
        print("FAIL", f)
    print(f"{len(failures)} failure(s)")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
