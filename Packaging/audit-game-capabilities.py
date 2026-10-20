"""Read-only native-data inventory for the editor capability audit (no saves/mods)."""
from pathlib import Path
import collections,csv,json,re,sys
media=Path(sys.argv[1]);output=Path(sys.argv[2]);output.mkdir(parents=True,exist_ok=True)
roots=['vehicles','trains','ships','airplanes','helicopters','cabins','buildings','buildings_types','buildingeditor','buildingeditoringame','resources','containers','trees','fences','paths','particleeffect','sounds','weathers','research','scenarios','campaign1','campaign2']
bases=[media]+sorted(p for p in media.iterdir() if p.is_dir() and re.fullmatch(r'(?:dlc|elc)\d+',p.name,re.I))
stats={};directives=collections.defaultdict(lambda:collections.defaultdict(list));errors=[];material_keys=collections.defaultdict(list);dds_formats=collections.Counter()
for base in bases:
 for root in roots:
  folder=base/root
  if not folder.is_dir():continue
  category=root;counter=stats.setdefault(category,collections.Counter())
  for path in sorted(folder.rglob('*')):
   if not path.is_file():continue
   counter['files']+=1;counter[path.suffix.lower()]+=1
   if path.name.lower()=='script.ini':counter['script_ini']+=1
   if path.suffix.lower()=='.dds':
    with path.open('rb') as image:header=image.read(148)
    if header[:4]==b'DDS ':
     kind=header[84:88].decode('ascii',errors='replace').replace('\x00','') or 'uncompressed'
     if kind=='DX10' and len(header)>=132:kind+=':'+str(int.from_bytes(header[128:132],'little'))
     dds_formats[kind]+=1
   if path.suffix.lower()=='.mtl':
    for key in set(re.findall(r'^\s*\$([A-Z_0-9]+)\b',path.read_text(encoding='utf-8-sig',errors='replace'),re.M)):material_keys[key].append(str(path.relative_to(media)).replace('\\','/'))
   if path.suffix.lower()!='.ini':continue
   try:text=path.read_text(encoding='utf-8-sig')
   except UnicodeDecodeError:text=path.read_text(encoding='cp1251',errors='replace')
   except OSError as exc:errors.append({'path':str(path.relative_to(media)),'error':str(exc)});continue
   counter['ini_read']+=1;seen=set()
   for line in text.splitlines():
    if re.fullmatch(r'\s*end\s*(?://.*)?',line,re.I):break
    match=re.match(r'\s*\$([A-Z_0-9]+)\b\s*(.*)',line,re.I)
    if not match:continue
    key=match[1].upper();counter['directives']+=1
    if key not in seen:directives[category][key].append(str(path.relative_to(media)).replace('\\','/'));seen.add(key)
result={'scope':'Installed base game plus local dlc/elc roots; excludes saves, Workshop and executables. Counts are data evidence, not proof of runtime semantics.','categories':stats,'read_errors':errors,'dds_formats':dds_formats,'material_keys':{k:{'files':len(v),'examples':v[:3]} for k,v in sorted(material_keys.items())},'keys':{category:{key:{'files':len(paths),'examples':paths[:3]} for key,paths in sorted(keys.items())} for category,keys in sorted(directives.items())}}
(output/'game-capabilities.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
with (output/'game-directives.csv').open('w',newline='',encoding='utf-8') as stream:
 writer=csv.writer(stream);writer.writerow(['category','directive','file_count','example'])
 for category,keys in sorted(directives.items()):
  for key,paths in sorted(keys.items()):writer.writerow([category,key,len(paths),paths[0]])
print(json.dumps({'categories':{k:{'ini':v['ini_read'],'nmf':v['.nmf'],'keys':len(directives[k])} for k,v in stats.items()},'ini_total':sum(v['ini_read'] for v in stats.values()),'unique_keys':len(set(k for values in directives.values() for k in values)),'read_errors':len(errors)},ensure_ascii=False))
