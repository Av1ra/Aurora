"""Owner-local archive, including PRIVATE Graphics sources. Never upload its output automatically."""
import argparse
import hashlib
import io
import json
import re
import subprocess
import sys
import tempfile
import zipfile
from datetime import datetime
from pathlib import Path, PurePosixPath

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--graphics',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
root=Path(__file__).resolve().parents[1]
repo=root.parents[1]
snapshot=json.loads((root/'snapshot.json').read_text(encoding='utf-8-sig'))
subprocess.run([sys.executable,str(root/'scripts/Verify-Handoff.py')],check=True)
def git(*parts,cwd=repo):
    return subprocess.check_output(['git',*parts],cwd=cwd)
def sha(data):return hashlib.sha256(data).hexdigest()
def permitted(name,data):
    p=PurePosixPath(name)
    if p.is_absolute() or '..' in p.parts or any(part in {'.git','bin','obj','__pycache__'} for part in p.parts):
        raise ValueError(f'Unexpected archive path: {name}')
    if p.suffix.lower() in {'.pk8','.pfx','.key'} or p.name in {'.env','.git-credentials'}:
        raise ValueError(f'Credential filename rejected: {name}')
    if re.search(rb'-----BEGIN (?:EC |RSA |ENCRYPTED )?PRIVATE KEY-----',data):
        raise ValueError(f'Private key rejected: {name}')
records=[]
args.output.parent.mkdir(parents=True,exist_ok=True)
pending=args.output.with_name(args.output.name+'.writing')
if pending.exists():raise SystemExit(f'Pending output already exists: {pending}')
with tempfile.TemporaryDirectory(prefix='Aurora-handoff-') as temp:
    bundle=Path(temp)/'Aurora-history.bundle'
    subprocess.run(['git','bundle','create',str(bundle),'main'],cwd=repo,check=True,capture_output=True)
    graphics_bytes=git('archive','--format=zip',snapshot['Graphics']['Revision'],
        'SovietRepublic/MODS/PBR.dll','WORKFLOW',cwd=args.graphics)
    with zipfile.ZipFile(pending,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as output:
        def add(name,data):
            permitted(name,data)
            output.writestr(name,data)
            records.append({'Path':name,'Bytes':len(data),'Sha256':sha(data)})
        # Preserve committed bytes, independent of the source checkout's autocrlf cache.
        with zipfile.ZipFile(io.BytesIO(git('archive','--format=zip','HEAD'))) as aurora:
            for entry in aurora.infolist():
                if entry.is_dir():continue
                data=aurora.read(entry)
                if entry.filename=='handoff/2026-10-22/package/Aurora-0.2-win-x64.zip':
                    data=(repo/entry.filename).read_bytes()
                add('Aurora/'+entry.filename,data)
        with zipfile.ZipFile(io.BytesIO(graphics_bytes)) as graphics:
            for entry in graphics.infolist():
                if not entry.is_dir():add('GameData/'+entry.filename,graphics.read(entry))
        add('Aurora-history.bundle',bundle.read_bytes())
        provenance={'Created':datetime.now().astimezone().isoformat(),'AuroraCommit':git('rev-parse','HEAD').decode().strip(),
            'GraphicsCommit':snapshot['Graphics']['Revision'],'GraphicsScope':['SovietRepublic/MODS/PBR.dll','WORKFLOW'],
            'InstalledRuntimeSource':snapshot['Aurora']['InstalledSource'],'NoPrivateSigningKey':True}
        add('PROVENANCE.json',(json.dumps(provenance,ensure_ascii=False,indent=2)+'\n').encode())
        readme='''# RESAURO: перенос Aurora 0.2

Aurora/ — полное дерево исходников и handoff, настоящий установленный ZIP, Setup, PSD и картинки.
GameData/ — закрытая PBR-подсистема владельца и WORKFLOW, без посторонних проектов GameData.
PROVENANCE.json — точные SHA. FILES_SHA256.json — контрольные суммы каждого файла, кроме самого списка.
Aurora-history.bundle — Git-история main. Закрытые publisher keys и SDK не включены.

Начать с Aurora/handoff/2026-10-22/README.md и 12_NEXT_AGENT_PROMPT.md.
Сначала проверить хеши файлов и zip.testzip(). Архив содержит private Graphics source; не публиковать автоматически.

Для продолжения с Git-историей в PowerShell:
  $env:GIT_LFS_SKIP_SMUDGE='1'
  git clone --config core.autocrlf=false --branch main ./Aurora-history.bundle ./Aurora-work
  Remove-Item Env:GIT_LFS_SKIP_SMUDGE
  Copy-Item ./Aurora/* ./Aurora-work/ -Recurse -Force
  Copy-Item ./Aurora/.gitattributes ./Aurora-work/ -Force
  Copy-Item ./Aurora/.gitignore ./Aurora-work/ -Force
  Copy-Item ./Aurora/.github ./Aurora-work/ -Recurse -Force
  git -C ./Aurora-work remote set-url origin https://github.com/Av1ra/Aurora.git
  git -C ./Aurora-work status --short
Папка Aurora-work должна создаваться новой командой clone; не копировать поверх другого проекта.
Файлы LFS берутся из Aurora/, поэтому для первоначального восстановления сеть не нужна.

Сборка: Windows x64, .NET 10, VS C++/SDK, Python numpy/Pillow/PyInstaller; подробнее 07_BUILD_TEST_INSTALL.md.
В новой Aurora-work: ./build.ps1 -Development. Private Graphics исходники собираются отдельно.
Готовая установка: Aurora/handoff/2026-10-22/package/Aurora-Setup.exe рядом с соответствующим ZIP.
Закрыть приложения перед установкой. RESAURO.zip не является installer payload.
'''
        add('README_RU.md',readme.encode('utf-8'))
        output.writestr('FILES_SHA256.json',json.dumps({'Schema':1,'Files':records},ensure_ascii=False,indent=2)+'\n')
    with zipfile.ZipFile(pending) as archive:
        if archive.testzip() is not None:raise ValueError('ZIP CRC verification failed')
        manifest=json.loads(archive.read('FILES_SHA256.json'))
        if len(archive.namelist())!=len(manifest['Files'])+1:raise ValueError('Manifest coverage mismatch')
        for row in manifest['Files']:
            data=archive.read(row['Path'])
            if len(data)!=row['Bytes'] or sha(data)!=row['Sha256']:raise ValueError(f"Archive SHA256 mismatch: {row['Path']}")
    if args.output.exists():
        backup=args.output.with_name(args.output.stem+'-backup-'+datetime.now().strftime('%Y%m%d-%H%M%S')+args.output.suffix)
        if backup.exists():raise ValueError(f'Backup already exists: {backup}')
        args.output.rename(backup)
    pending.rename(args.output)
    hash_path=args.output.with_suffix('.zip.sha256')
    hash_path.write_text(sha(args.output.read_bytes())+'  '+args.output.name+'\n',encoding='ascii')
    print(json.dumps({'Archive':str(args.output),'Bytes':args.output.stat().st_size,'Files':len(records)+1,
        'Sha256':hash_path.read_text().split()[0],'Verified':True},ensure_ascii=False))
