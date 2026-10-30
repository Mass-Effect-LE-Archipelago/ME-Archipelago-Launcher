"""Build the single-file launcher into dist/ with its SHA-256.

usage: python tools/publish.py [version]      (default: the csproj <Version>)
Output: dist/Mass Effect Archipelago Launcher.exe (self-contained, win-x64) and dist/SHA256SUMS.
"""
import hashlib
import pathlib
import re
import shutil
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src" / "MassEffectAPLauncher" / "MassEffectAPLauncher.csproj"
EXE = "Mass Effect Archipelago Launcher.exe"


def main() -> int:
    version = sys.argv[1] if len(sys.argv) > 1 else re.search(r"<Version>([^<]+)</Version>", PROJECT.read_text()).group(1)
    dist = ROOT / "dist"
    if dist.exists():
        shutil.rmtree(dist)
    subprocess.run(["dotnet", "publish", str(PROJECT), "-c", "Release", "-r", "win-x64", "--self-contained", "true",
                    "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true",
                    f"-p:Version={version}", "-o", str(dist / "publish")], check=True)
    shutil.copy(dist / "publish" / EXE, dist / EXE)
    shutil.rmtree(dist / "publish")
    digest = hashlib.sha256((dist / EXE).read_bytes()).hexdigest()
    (dist / "SHA256SUMS").write_text(f"{digest}  {EXE}\n", newline="\n")
    print(f"{dist / EXE}  {version}  sha256 {digest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
