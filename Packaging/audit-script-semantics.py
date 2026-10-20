from pathlib import Path
import json,re,sys
media=Path(sys.argv[1])
data=json.loads(Path('docs/audit-data/game-capabilities.json').read_text(encoding='utf-8'))
categories=['vehicles','trains','ships','airplanes','helicopters','cabins']
paths=set()
for category in categories:
 for base in [media]+[p for p in media.iterdir() if p.is_dir() and re.fullmatch(r'(dlc|elc)\d+',p.name)]:
  if (base/category).is_dir():paths.update((base/category).rglob('script.ini'))
infinite=[];multiple=[];strings=[]
for p in sorted(paths):
 text=p.read_text(encoding='utf-8-sig',errors='replace');name=str(p.relative_to(media)).replace('\\','/')
 years=re.search(r'^\s*\$AVAILABLE\s+(\d+)\s+(\d+)',text,re.M)
 if years and int(years[2])>2100:infinite.append({'path':name,'years':years[0].strip()})
 resources=re.findall(r'^\s*\$RESOURCE_ALLOW_ONLY\s+([^\r\n]+)',text,re.M)
 if len(resources)>1:multiple.append({'path':name,'resources':resources})
 if re.search(r'^\s*\$COUNTRY_STR\b',text,re.M):strings.append(name)
result={'scriptCount':len(paths),'endYearAbove2100':{'count':len(infinite),'examples':infinite[:4]},'multipleAllowedResources':{'count':len(multiple),'examples':multiple[:4]},'stringCountries':strings,'dollarEndFiles':sum(data['keys'][c].get('END',{}).get('files',0) for c in categories)}
Path('docs/audit-data/script-semantics.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(result))
