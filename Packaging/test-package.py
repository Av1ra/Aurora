"""Check the adjacent archive installer without installing or launching the app."""
import pathlib
import shutil
import subprocess
import sys
import zipfile
import tempfile

source = pathlib.Path(sys.argv[1]).resolve()
folder = pathlib.Path(tempfile.mkdtemp(prefix="Aurora-package-check-"))
setup = folder / "Aurora-Setup.exe"
archive = folder / "Aurora-0.2-win-x64.zip"
shutil.copy2(source / setup.name, setup)
shutil.copy2(source / archive.name, archive)
assert sorted(x.name for x in source.glob("*.zip")) == ["Aurora-0.2-win-x64.zip"], "Duplicate ZIP archives"
with zipfile.ZipFile(archive) as bundle:
    names = set(bundle.namelist())
    assert {"Aurora Launcher.exe", "Aurora Editor.exe", "app/Editor/acode.lnchr", "app/Editor/Aurora.Editor.Module.dll"} <= names
    assert "Aurora.exe" not in names and "Aurora.Editor.exe" not in names
    assert not any(name.lower().endswith(".zip") for name in names)
print("PASS exactly one archive contains Launcher and Editor with shared runtime")
assert subprocess.run([str(setup), "--unpack-test"], timeout=60).returncode == 0
print("PASS matching adjacent ZIP verified and extracted")
with archive.open("r+b") as output:
    output.write(b"tampered")
assert subprocess.run([str(setup), "--unpack-test"], timeout=30).returncode == 1
print("PASS tampered adjacent ZIP rejected")
archive.rename(folder / "archive-removed-for-test.zip")
assert subprocess.run([str(setup), "--unpack-test"], timeout=30).returncode == 1
print("PASS missing adjacent ZIP rejected; AURORA_ADJACENT_PACKAGE_PASS")

