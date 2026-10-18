import tempfile,subprocess,zipfile,hashlib,pathlib,json
root=pathlib.Path(tempfile.mkdtemp(prefix='Aurora-native-tests-'))
import sys
exe=pathlib.Path(sys.argv[1]).resolve()
src=root/'source'; dst=root/'Другая папка'; src.mkdir();dst.mkdir()
(src/'app').mkdir();(src/'Aurora.exe').write_bytes(b'new');(src/'keep.txt').write_text('new');(dst/'keep.txt').write_text('old');(dst/'custom.txt').write_text('user');(dst/'legacy.dll').write_bytes(b'old');(dst/'changed.dll').write_bytes(b'user')
(src/'app'/'retired-files.tsv').write_text('legacy.dll\t'+hashlib.sha256(b'old').hexdigest()+'\nchanged.dll\t'+hashlib.sha256(b'old').hexdigest()+'\nkeep.txt\t'+hashlib.sha256(b'new').hexdigest()+'\n')
marker=pathlib.Path.home()/'AppData/Local/Aurora/install-root.txt'; before=marker.read_bytes() if marker.exists() else None
assert subprocess.run([exe,'--install-test',src,dst]).returncode==0
assert (dst/'keep.txt').read_text()=='new' and (dst/'custom.txt').read_text()=='user' and not (dst/'legacy.dll').exists() and (dst/'changed.dll').read_bytes()==b'user'
assert (marker.read_bytes() if marker.exists() else None)==before
print('PASS custom Unicode folder, exact retirement, unknown files, no fixture global preference writes')
for name,path,code in [('valid','Assets/test.txt',0),('traversal','../outside.txt',1),('absolute','/outside.txt',1),('drive','C:/outside.txt',1),('backslash','..\\outside.txt',1)]:
 z=root/(name+'.zip')
 with zipfile.ZipFile(z,'w') as f:f.writestr(path,b'test')
 assert subprocess.run([exe,'--validate-zip',z]).returncode==code,name
print('PASS ZIP traversal/absolute/drive/backslash rejection')
# Locked destination forces rollback of earlier replacements.
import ctypes
kernel=ctypes.WinDLL('kernel32',use_last_error=True); kernel.CreateFileW.restype=ctypes.c_void_p
h=kernel.CreateFileW(str(dst/'keep.txt'),0x80000000,1,None,3,0,None)
assert h not in (None,ctypes.c_void_p(-1).value)
(src/'Aurora.exe').write_bytes(b'newer')
assert subprocess.run([exe,'--install-test',src,dst]).returncode==1
kernel.CloseHandle(ctypes.c_void_p(h))
assert (dst/'Aurora.exe').read_bytes()==b'new'
print('PASS locked file rollback; AURORA_NATIVE_INSTALLER_PASS')
print(root)


