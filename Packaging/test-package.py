"""Check the adjacent archive installer without installing or launching the app."""
import pathlib
import shutil
import subprocess
import sys
import tempfile

source = pathlib.Path(sys.argv[1]).resolve()
folder = pathlib.Path(tempfile.mkdtemp(prefix="Aurora-package-check-"))
setup = folder / "Aurora-Setup.exe"
archive = folder / "Aurora-0.2-Full-win-x64.zip"
shutil.copy2(source / setup.name, setup)
shutil.copy2(source / archive.name, archive)
assert subprocess.run([str(setup), "--unpack-test"], timeout=60).returncode == 0
print("PASS matching adjacent ZIP verified and extracted")
with archive.open("r+b") as output:
    output.write(b"tampered")
assert subprocess.run([str(setup), "--unpack-test"], timeout=30).returncode == 1
print("PASS tampered adjacent ZIP rejected")
archive.rename(folder / "archive-removed-for-test.zip")
assert subprocess.run([str(setup), "--unpack-test"], timeout=30).returncode == 1
print("PASS missing adjacent ZIP rejected; AURORA_ADJACENT_PACKAGE_PASS")

