"""Inspect PE icon resources as data and smoke-test the branded CLI backend."""
import ctypes
from ctypes import wintypes
from pathlib import Path
import struct
import subprocess
import sys
import tempfile

stage, setup = map(Path, sys.argv[1:])
kernel = ctypes.WinDLL('kernel32', use_last_error=True)
kernel.LoadLibraryExW.argtypes = [wintypes.LPCWSTR, ctypes.c_void_p, wintypes.DWORD]
kernel.LoadLibraryExW.restype = ctypes.c_void_p
kernel.FindResourceW.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p]
kernel.FindResourceW.restype = ctypes.c_void_p
kernel.LoadResource.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
kernel.LoadResource.restype = ctypes.c_void_p
kernel.LockResource.argtypes = [ctypes.c_void_p]
kernel.LockResource.restype = ctypes.c_void_p
kernel.SizeofResource.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
kernel.SizeofResource.restype = wintypes.DWORD
kernel.FreeLibrary.argtypes = [ctypes.c_void_p]

def resource(module, kind):
    found = kernel.FindResourceW(module, 1, kind)
    assert found, f'Icon resource {kind} missing'
    data = kernel.LockResource(kernel.LoadResource(module, found))
    return ctypes.string_at(data, kernel.SizeofResource(module, found))

files = sorted(stage.rglob('Aurora*.exe')) + [setup]
assert len(files) >= 7
for path in files:
    module = kernel.LoadLibraryExW(str(path.resolve()), None, 2)  # data, never execute
    assert module, path
    try:
        group, png = resource(module, 14), resource(module, 3)
        assert struct.unpack_from('<HHH', group) == (0, 1, 1), path
        assert png[:8] == b'\x89PNG\r\n\x1a\n', path
        assert struct.unpack_from('>II', png, 16) == (256, 256), path
    finally:
        kernel.FreeLibrary(module)
print(f'PASS embedded PNG executable icons: {len(files)} Aurora executables')
backend = stage / 'app/Editor/Backend/Aurora.ModelBackend.exe'
with tempfile.TemporaryDirectory(prefix='Aurora-brand-check-') as folder:
    missing = Path(folder) / 'missing-aurora-cli-request.json'
    result = subprocess.run([str(backend.resolve()), str(missing)], capture_output=True,
                            text=True, timeout=30, creationflags=0x08000000)
    assert result.returncode == 1 and missing.name in result.stderr, result.stderr
print('PASS branded Python CLI package still executes after resource embedding')
