"""Independent rigid-stream fixtures exercise the real glass backend."""
import struct
import sys
from pathlib import Path
import numpy as np
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'Backend'))
from prepare_vehicle_glass import prepare_mesh

def fixture(flags):
    header=bytearray(b'fromObj\0'+struct.pack('<III',1,1,0)+b'Body'+bytes(60))
    matrix=struct.pack('<16f',*[1. if i%5==0 else 0. for i in range(16)])
    block=bytearray(struct.pack('<II',0,0)+b'Body'+bytes(60)+struct.pack('<HH',65535,0)+matrix*2+struct.pack('<6fI',0,0,0,1,1,0,1))
    block+=struct.pack('<7I',0,4,6,1,0,flags,0)+struct.pack('<6H',0,1,2,0,2,3)
    attributes=struct.pack('<12f',0,0,0,1,0,0,1,1,0,0,1,0)+struct.pack('<12f',*([0,0,1]*4))
    if flags!=0x108: attributes+=bytes(4*24)
    attributes+=struct.pack('<8f',0,0,1,0,1,1,0,1)
    block+=attributes
    if flags!=0x108: block+=bytes(range(80))
    block+=struct.pack('<IIHH',0,6,0,0)
    struct.pack_into('<I',block,4,len(block)+4)
    struct.pack_into('<I',block,232,len(block)-232+8)
    header+=block;struct.pack_into('<I',header,16,len(header))
    return bytes(header),attributes

for flags in (0x108,0x40139,0x4013a):
    original,attributes=fixture(flags)
    for alpha in (np.full((8,8),127,dtype=np.uint8), np.tile(np.array([0]*4+[127]*4,dtype=np.uint8),(8,1))):
        result,name,report=prepare_mesh(original,'Body',alpha)
        node=20+2*64;ni=struct.unpack_from('<I',result,node+240)[0];attr=node+260+ni*2
        assert result[node+8:node+232]==original[84+8:84+232], 'transforms/bounds changed'
        assert result[attr:attr+len(attributes)]==attributes, 'vertex/normal/UV streams changed'
        assert struct.unpack_from('<I',result,16)[0]==len(result) and sum(r['window_triangles'] for r in report)>0
    print(f'PASS OBJ/native alpha glass {flags:#x}: geometry, UV, transforms and size retained')
