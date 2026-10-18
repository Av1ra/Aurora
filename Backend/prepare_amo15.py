"""Prepare an isolated AMO15 glass candidate. Does not install or modify game files.

Only the audited rigid AMO15 stream layouts are accepted. Vertex attributes,
node transforms, bounds and animations are retained byte-for-byte. The common
body atlas is split into opaque and window draw groups, with matching face-cache
permutation. Runtime transparent queue integration is still required.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct

import numpy as np
from PIL import Image


def require(value, message):
    if not value:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def intersects_window(uv, alpha):
    """Conservative group selection; the eventual shader still clips coverage."""
    height, width = alpha.shape
    triangle = uv * np.array([width, height])
    lo = np.maximum(np.floor(triangle.min(axis=0)).astype(int) - 1, 0)
    hi = np.minimum(np.ceil(triangle.max(axis=0)).astype(int) + 1,
                    [width - 1, height - 1])
    yy, xx = np.where(alpha[lo[1]:hi[1]+1, lo[0]:hi[0]+1] < 128)
    if not len(xx):
        return False
    px, py = xx + lo[0] + .5, yy + lo[1] + .5
    signs = [(triangle[(i+1) % 3, 0]-triangle[i, 0])*(py-triangle[i, 1]) -
             (triangle[(i+1) % 3, 1]-triangle[i, 1])*(px-triangle[i, 0])
             for i in range(3)]
    # Zero-area UVs cannot cover a window texel centre.
    edge_a, edge_b = triangle[1]-triangle[0], triangle[2]-triangle[0]
    area = edge_a[0]*edge_b[1]-edge_a[1]*edge_b[0]
    if abs(float(area)) < 1e-8:
        return False
    return bool((((signs[0] >= 0) & (signs[1] >= 0) & (signs[2] >= 0)) |
                 ((signs[0] <= 0) & (signs[1] <= 0) & (signs[2] <= 0))).any())


def split_mesh(data, alpha):
    u32 = lambda offset: struct.unpack_from('<I', data, offset)[0]
    require(data[:8] in (b'B3DMH\x0010', b'fromObj\0') and
            u32(16) == len(data), 'NMF header')
    require(u32(8) == 1 and data[20:84].split(b'\0')[0] == b'AMO_Covered',
            'not the single-material AMO15 fixture')
    count = u32(12)
    require(count in (1, 4), 'unexpected AMO15 node count')
    header = bytearray(data[:84])
    struct.pack_into('<I', header, 8, 2)
    header.extend(b'AMO_Glass'.ljust(64, b'\0'))
    result = header
    offset = 84
    report = []
    for node in range(count):
        require(offset+260 <= len(data), 'truncated node')
        node_type, size = struct.unpack_from('<II', data, offset)
        require(node_type == 0 and offset+size <= len(data), 'node bounds/type')
        lod = offset+232
        lod_size, nv, ni, groups, skin, cpu, gpu = struct.unpack_from('<7I', data, lod)
        require(u32(offset+228) == 1 and not skin and not gpu and groups == 1,
                'unsupported native hierarchy/LOD/skin/groups')
        require(cpu in (0x40139, 0x4013a) and ni % 3 == 0 and 0 < nv <= 65536,
                'unaudited vertex stream')
        ix_offset = lod+28
        indices = np.frombuffer(data, dtype='<u2', count=ni, offset=ix_offset).reshape(-1, 3)
        require(int(indices.max()) < nv, 'invalid vertex index')
        attributes = ix_offset+ni*2
        # Positions + normal/tangent/bitangent, then UV0. No attributes change.
        uv = np.frombuffer(data, dtype='<f4', count=nv*2,
                           offset=attributes+nv*48).reshape(-1, 2)
        require(np.isfinite(uv).all() and (uv >= 0).all() and (uv <= 1).all(),
                'AMO15 UV range')
        faces = attributes+nv*56
        group_offset = faces+(ni//3)*40
        require(group_offset+12 == offset+size == lod+lod_size, 'stream lengths')
        require(struct.unpack_from('<IIhh', data, group_offset) == (0, ni, 0, 0),
                'unexpected native draw group')
        windows = [i for i, triangle in enumerate(indices)
                   if intersects_window(uv[triangle], alpha)]
        window_set = set(windows)
        solids = [i for i in range(len(indices)) if i not in window_set]
        block = bytearray(data[offset:offset+size])
        if windows:
            require(node == 0, 'wheel geometry unexpectedly covers window mask')
            permutation = solids+windows
            ix_start = ix_offset-offset
            reordered = indices[permutation].astype('<u2').tobytes()
            block[ix_start:ix_start+ni*2] = reordered
            cache = np.frombuffer(data, dtype=np.uint8, count=len(indices)*40,
                                  offset=faces).reshape(-1, 40)
            block[faces-offset:group_offset-offset] = cache[permutation].tobytes()
            block[group_offset-offset:] = struct.pack('<IIhhIIhh', 0, len(solids)*3,
                                                      0, 0, len(solids)*3,
                                                      len(windows)*3, 1, 0)
            struct.pack_into('<I', block, 4, size+12)
            struct.pack_into('<I', block, 232, lod_size+12)
            struct.pack_into('<I', block, 232+12, 2)
            # Invert the permutation to prove every triangle and face-cache
            # record, including winding, is retained exactly once.
            inverse = np.argsort(permutation)
            require(np.array_equal(np.frombuffer(reordered, dtype='<u2').reshape(-1, 3)[inverse], indices), 'index conservation')
            require(np.array_equal(cache[permutation][inverse], cache), 'face-cache conservation')
            require(block[attributes-offset:faces-offset] == data[attributes:faces],
                    'vertex attributes changed')
            require(block[8:232] == data[offset+8:offset+232], 'node metadata changed')
        else:
            require(bytes(block) == data[offset:offset+size], 'opaque node changed')
        report.append(dict(node=node, triangles=len(indices), windows=len(windows)))
        result.extend(block)
        offset += size
    require(offset == len(data), 'trailing streams')
    require(sum(item['windows'] for item in report) > 0, 'no window geometry')
    struct.pack_into('<I', result, 16, len(result))
    return bytes(result), report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-root', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    source = args.game_root.resolve()/'media_soviet/dlc3/vehicles/_Covered_AMO15'
    output = args.output.resolve()
    require(not output.exists(), 'output must be a new isolated directory')
    require(not output.is_relative_to((args.game_root/'mods').resolve()) and
            not output.is_relative_to((args.game_root/'media_soviet').resolve()),
            'cannot write candidate into installed game assets')
    images = [np.asarray(Image.open(source/f'diffuse{i}.dds').convert('RGBA'))[:, :, 3]
              for i in range(1, 7)]
    require(all(np.array_equal(images[0], image) for image in images[1:]),
            'paint variants do not share the same window mask')
    require(set(np.unique(images[0]).tolist()) == {0, 255},
            'AMO15 requires the audited binary native window mask')
    prepared, evidence = {}, []
    for filename in ('main.nmf', 'lod1.nmf', 'lod2.nmf'):
        original = (source/filename).read_bytes()
        candidate, nodes = split_mesh(original, images[0])
        prepared[filename] = candidate
        evidence.append(dict(file=filename, before=digest(original),
                             after=digest(candidate), nodes=nodes))
    for filename in ['material.mtl']+[f'material_{i}.mtl' for i in range(1, 6)]:
        original = (source/filename).read_bytes()
        # Mirror the paint's own maps. The future explicit AMO_Glass rule owns
        # transmission; a native alpha mask must not turn all paint transparent.
        text = original.decode('ascii')
        require(text.count('$SUBMATERIAL AMO_Covered') == 1 and '$END' in text,
                'unexpected AMO15 material')
        glass = text.replace('$SUBMATERIAL AMO_Covered', '$SUBMATERIAL AMO_Glass')
        prepared[filename] = (text.replace('$END', '')+'\n'+glass).encode('ascii')
    output.mkdir(parents=True)
    for filename, content in prepared.items():
        (output/filename).write_bytes(content)
    (output/'validation.json').write_text(json.dumps(dict(
        status='CANDIDATE_ONLY_RUNTIME_QUEUE_NOT_CONNECTED_DO_NOT_INSTALL',
        source=str(source), source_unchanged=True, models=evidence,
        alpha_sha256=digest(images[0].tobytes())), indent=2), encoding='utf-8')
    for item in evidence:
        require(digest((source/item['file']).read_bytes()) == item['before'],
                'source was changed during preparation')
    print(json.dumps(evidence, indent=2))
    print('PASS isolated window-group candidate; NOT installed; runtime integration pending')


if __name__ == '__main__':
    main()
