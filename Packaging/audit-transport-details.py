"""Transport-only read-only inventory, grouped by native TYPE rather than folder."""
from pathlib import Path
import collections,csv,json,re,sys
media=Path(sys.argv[1]).resolve();out=Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
roots=['vehicles','trains','ships','airplanes','helicopters','cabins']
bases=[media]+sorted(p for p in media.iterdir() if p.is_dir() and re.fullmatch(r'(dlc|elc)\d+',p.name,re.I))
classes=collections.defaultdict(list);keys={};references=collections.Counter();read_count=0;special=[];materials=collections.defaultdict(list)
def tokens(line):return re.findall(r'"[^"\r\n]*"|[^\s]+',line)
def comment(line):
 quoted=False
 for i,char in enumerate(line):
  if char=='"':quoted=not quoted
  if not quoted and (i==0 or line[i-1].isspace()) and line.startswith(('//','--','#'),i):return line[:i]
 return line
def clean(text):
 text=re.sub(r'/\*.*?\*/','',text,flags=re.S)
 text='\n'.join(comment(l) for l in text.splitlines())
 end=re.search(r'(?im)^[\t ]*\$?end[\t ]*$',text)
 return text[:end.start()] if end else text
for base in bases:
 for root in roots:
  folder=base/root
  if not folder.is_dir():continue
  for path in sorted(folder.rglob('*.mtl')):
   for key in set(re.findall(r'^\s*\$([A-Z_0-9]+)\b',path.read_text(encoding='utf-8-sig',errors='replace'),re.M)):
    materials[key].append(path.relative_to(media).as_posix())
  for path in sorted(folder.rglob('script.ini')):
   read_count+=1;text=clean(path.read_text(encoding='utf-8-sig',errors='replace'));example=path.relative_to(media).as_posix();matches=list(re.finditer(r'^\s*\$([A-Z_0-9]+)[^\S\r\n]*([^\r\n]*)',text,re.M|re.I));fields=[]
   for i,m in enumerate(matches):
    key=m[1].upper()
    if key=='END':break
    body=text[m.end():matches[i+1].start() if i+1<len(matches) else len(text)]
    body=re.split(r'(?im)^\s*end\s*$',body)[0].strip();fields.append((key,m[2].strip(),body))
   native=next((value for key,value,body in fields if key=='TYPE'),'NO_TYPE');classes[native].append(example)
   seen=set()
   for key,value,body in fields:
    entry=keys.setdefault(key,{'occurrences':0,'files':0,'types':collections.Counter(),'examples':[],'shapes':collections.Counter()});entry['occurrences']+=1
    if key not in seen:entry['files']+=1;entry['types'][native]+=1;seen.add(key)
    if len(entry['examples'])<3 and example not in entry['examples']:entry['examples'].append(example)
    entry['shapes'][f'header:{len(tokens(value))};body-lines:{len(body.splitlines()) if body else 0}']+=1
    references.update(ext.lower() for ext in re.findall(r'\.(nmf|naf|mtl|dds|ini)\b',value+'\n'+body,re.I))
   skills=[(key,value) for key,value,body in fields if key.startswith('SKILL_')]
   flags=[key for key,value,body in fields if key.startswith(('SKILL_','TRAINGROUP_'))]
   if len(skills)>1:special.append({'kind':'multiple-skills','example':example,'skills':skills})
   for key,value in skills:
    if len(tokens(value))>1:special.append({'kind':'multi-argument-skill','example':example,'key':key,'arguments':len(tokens(value))})
result={'scope':'Only script.ini and MTL under six transport roots in base/DLC/ELC. Native TYPE determines class. Shapes are observed syntax, not a documented argument schema. No game execution.','script_count':read_count,'classes':{k:{'files':len(v),'examples':v[:3]} for k,v in sorted(classes.items())},'keys':dict(sorted(keys.items())),'reference_extensions':references,'special_cases':special,'material_keys':{k:{'files':len(v),'examples':v[:3]} for k,v in sorted(materials.items())}}
(out/'transport-details.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
with (out/'transport-directives.csv').open('w',encoding='utf-8',newline='') as stream:
 writer=csv.writer(stream);writer.writerow(['key','files','occurrences','native_types','example'])
 for key,value in sorted(keys.items()):writer.writerow([key,value['files'],value['occurrences'],'; '.join(value['types']),value['examples'][0]])
print(json.dumps({'script_count':read_count,'types':{k:len(v) for k,v in classes.items()},'keys':len(keys),'multi_skill':sum(x['kind']=='multiple-skills' for x in special),'multi_arg_skill':sum(x['kind']=='multi-argument-skill' for x in special),'reference_extensions':references,'material_keys':{k:len(v) for k,v in materials.items()}}))
