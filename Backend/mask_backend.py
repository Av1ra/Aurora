"""Verified rigid NMF alpha-mask split. No installation or original writes."""
import json
from pathlib import Path
import sys
import numpy as np
from PIL import Image
from prepare_vehicle_glass import prepare_mesh

def main():
    request=json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
    output=Path(request['Output']).resolve()
    alpha=np.asarray(Image.open(request['Mask']).convert('RGBA'))[:,:,3]
    prepared=[]
    for filename in request['Models']:
        source=Path(filename).resolve()
        if source.parent==output:
            raise ValueError('Output must differ from source')
        data,name,report=prepare_mesh(source.read_bytes(),request['Material'],alpha)
        if not sum(n['window_triangles'] for n in report):
            raise ValueError('Mask selects no window triangles in '+source.name)
        if prepared and prepared[0][2]!=name:
            raise ValueError('LOD material tables differ; align the material slots first')
        prepared.append((source.name,data,name,report))
    for filename,data,name,report in prepared:
        (output/filename).write_bytes(data)
    (output/'glass-split-report.json').write_text(json.dumps([{'File':n,'Nodes':r}for n,_,_,r in prepared],indent=2))
    print(prepared[0][2])

if __name__=='__main__':
    try: main()
    except Exception as error:
        print(str(error),file=sys.stderr)
        sys.exit(1)
