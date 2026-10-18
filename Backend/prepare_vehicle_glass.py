"""Prepare vehicle glass as an isolated, reversible PBR installation package.

Original files are read only. glass.dds uses the model UVs: A=0 outside windows;
A=1..255 is window opacity, RGB is tint. Native diffuse alpha is never changed.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import sys

import numpy as np
from PIL import Image

from prepare_amo15 import intersects_window, require


def sha(data):
    return hashlib.sha256(data).hexdigest()


def classify_uv(coords, alpha, window, outside):
    """Match native wrap sampling, including negative/repeated UV coordinates."""
    require(np.isfinite(coords).all(), 'Nonfinite selected UV coordinates')
    lo, hi = np.floor(coords.min(axis=0)).astype(int), np.floor(coords.max(axis=0)).astype(int)
    require(np.prod(hi-lo+1) <= 64, 'A selected triangle spans more than 64 UV tiles')
    samples = np.concatenate((coords, (coords+np.roll(coords, 1, axis=0))*.5,
                              coords.mean(axis=0)[None, :]))
    # Degenerate UV triangles still sample a real texture point or line.
    edge_a, edge_b = coords[1]-coords[0], coords[2]-coords[0]
    area = edge_a[0]*edge_b[1]-edge_a[1]*edge_b[0]
    if abs(float(area)) < 1e-8:
        count = min(8192, max(2, int(np.max(np.ptp(coords, axis=0))*max(alpha.shape))*2+2))
        samples = np.concatenate([coords[i]+(coords[(i+1)%3]-coords[i])*
                                  np.linspace(0, 1, count)[:, None] for i in range(3)])
    pixels = np.floor(np.mod(samples, 1)*[alpha.shape[1], alpha.shape[0]]).astype(int)
    covered = alpha[pixels[:, 1], pixels[:, 0]] > 0
    hit, solid = bool(covered.any()), bool((~covered).any())
    for y in range(lo[1], hi[1]+1):
        for x in range(lo[0], hi[0]+1):
            local = coords-[x, y]
            if (local.max(axis=0) < 0).any() or (local.min(axis=0) > 1).any():
                continue
            hit = hit or intersects_window(local, window)
            solid = solid or intersects_window(local, outside)
    return hit, hit and not solid


def material_names(data):
    require(len(data) >= 20, 'NMF header is truncated')
    count, nodes, size = struct.unpack_from('<III', data, 8)
    require(data[:8] in (b'B3DMH\x0010', b'fromObj\0') and size == len(data),
            'Unsupported NMF signature/length')
    require(0 < count < 512 and 0 < nodes <= 2048 and 20+count*64 <= size,
            'Invalid NMF material/node table')
    names = []
    for i in range(count):
        entry = data[20+i*64:84+i*64]
        require(b'\0' in entry, 'Unterminated material name')
        names.append(entry.split(b'\0')[0].decode('ascii'))
    return names


def prepare_mesh(data, material, alpha):
    names = material_names(data)
    require(material in names, 'Selected material is absent from this NMF')
    require(not any(n.startswith('PBR_Glass_') for n in names),
            'Use the original NMF, not an already prepared glass model')
    source_id = names.index(material)
    glass_name = 'PBR_Glass_'+str(source_id)
    glass_id = len(names)
    nodes = struct.unpack_from('<I', data, 12)[0]
    offset = 20+len(names)*64
    result = bytearray(data[:offset])
    result.extend(glass_name.encode('ascii').ljust(64, b'\0'))
    struct.pack_into('<I', result, 8, glass_id+1)
    window = np.where(alpha > 0, 0, 255).astype(np.uint8)
    outside = np.where(alpha == 0, 0, 255).astype(np.uint8)
    classified = {}
    report = []
    for node in range(nodes):
        require(offset+260 <= len(data), 'Truncated NMF node')
        kind, stored_size = struct.unpack_from('<II', data, offset)
        require(kind == 0, 'External/animated node layout needs a dedicated adapter')
        end = offset+stored_size
        def boundary_ok(at):
            if node+1 == nodes:
                return at == len(data)
            return (at+72 <= len(data) and
                    struct.unpack_from('<I', data, at)[0] <= 3 and
                    b'\0' in data[at+8:at+72] and data[at+8] >= 32)
        if not boundary_ok(end):
            end -= 4
        require(boundary_ok(end), 'Unrecognized NMF node boundary')
        name = data[offset+8:offset+72].split(b'\0')[0].decode('ascii')
        lod = offset+232
        lod_size, nv, ni, ng, skin, cpu, gpu = struct.unpack_from('<7I', data, lod)
        require(struct.unpack_from('<I', data, offset+228)[0] == 1 and
                not skin and not gpu and cpu in (0x40139, 0x4013a),
                'Only verified rigid position/normal/tangent/UV layouts are supported')
        require(0 < nv <= 65536 and ni > 0 and ni % 3 == 0 and 0 < ng <= 512,
                'Invalid native vertex/index/group counts')
        ix = lod+28
        attributes = ix+ni*2
        faces = attributes+nv*56
        groups_at = faces+(ni//3)*40
        require(groups_at+ng*12 == end and lod+lod_size in (end, end+4, end+8),
                'Unsupported trailing vertex/face streams')
        indices = np.frombuffer(data, '<u2', count=ni, offset=ix).reshape(-1, 3)
        require(int(indices.max()) < nv, 'Out-of-range vertex index')
        uv = np.frombuffer(data, '<f4', count=nv*2,
                           offset=attributes+nv*48).reshape(-1, 2)
        cache = np.frombuffer(data, np.uint8, count=(ni//3)*40,
                              offset=faces).reshape(-1, 40)
        permutation, groups, consumed = [], [], 0
        selected = pure = mixed = 0
        for group in range(ng):
            first, count, mat, bones = struct.unpack_from('<IIHH', data, groups_at+12*group)
            require(first == consumed and count % 3 == 0 and first+count <= ni and
                    mat < len(names) and bones == 0, 'Invalid native draw groups')
            triangles = list(range(first//3, (first+count)//3))
            solids, windows = [], []
            for triangle in triangles:
                if mat != source_id or name.lower().startswith('tire'):
                    solids.append(triangle)
                    continue
                coords = uv[indices[triangle]]
                key = coords.tobytes()
                if key not in classified:
                    classified[key] = classify_uv(coords, alpha, window, outside)
                hit, fully_glass = classified[key]
                if not hit:
                    solids.append(triangle)
                    continue
                windows.append(triangle)
                if fully_glass:
                    pure += 1
                else:
                    # Preserve the opaque part of a mixed triangle. The solid
                    # shader opens only the separate mask's window pixels.
                    solids.append(triangle)
                    mixed += 1
            for triangles_out, destination in ((solids, mat), (windows, glass_id)):
                if triangles_out:
                    groups.append((len(permutation)*3, len(triangles_out)*3, destination, 0))
                    permutation.extend(triangles_out)
            selected += len(windows)
            consumed += count
        require(consumed == ni, 'Native groups do not cover the complete mesh')
        require(set(permutation) == set(range(ni//3)), 'Triangle loss during glass preparation')
        block = bytearray(data[offset:ix])
        block.extend(indices[permutation].astype('<u2').tobytes())
        block.extend(data[attributes:faces])
        block.extend(cache[permutation].tobytes())
        for group in groups:
            block.extend(struct.pack('<IIHH', *group))
        delta = len(block)-(end-offset)
        struct.pack_into('<I', block, 4, stored_size+delta)
        struct.pack_into('<I', block, 232, lod_size+delta)
        struct.pack_into('<I', block, 240, len(permutation)*3)
        struct.pack_into('<I', block, 244, len(groups))
        require(block[8:232] == data[offset+8:offset+232], 'Node transform/bounds changed')
        result.extend(block)
        report.append(dict(node=name, original_triangles=ni//3,
                           window_triangles=selected, pure=pure, mixed=mixed))
        offset = end
    require(offset == len(data), 'Trailing NMF chunks are unsupported')
    struct.pack_into('<I', result, 16, len(result))
    return bytes(result), glass_name, report


def prepare_material(data, material, glass_name):
    # Preserve source encoding and every existing paint/light/texture directive.
    pattern = rb'(?m)^\s*\$SUBMATERIAL\s+([^\r\n]+)'
    starts = list(re.finditer(pattern, data))
    matches = [(i, m) for i, m in enumerate(starts)
               if m.group(1).strip().decode('ascii') == material]
    require(len(matches) == 1, 'MTL must contain exactly one selected submaterial')
    i, match = matches[0]
    end = starts[i+1].start() if i+1 < len(starts) else len(data)
    block = data[match.start():end]
    require(b'$END' in data, 'MTL terminator is missing')
    glass = block.replace(match.group(0).strip(), b'$SUBMATERIAL '+glass_name.encode('ascii'), 1)
    # Native files use either a single final END or one END per material.
    body = data[:data.rfind(b'$END')]+data[data.rfind(b'$END')+4:]
    return body.rstrip()+b'\n\n'+glass.replace(b'$END', b'').rstrip()+b'\n$END\n'


def build_package(game, model, mask, material, output):
    game, model, mask, output = [Path(p).resolve() for p in (game, model, mask, output)]
    require(not output.exists(), 'Choose a new output folder')
    require(not output.is_relative_to(game/'media_soviet') and
            not output.is_relative_to(game/'mods'), 'The package output must be outside installed assets')
    relative = model.relative_to(game)
    if relative.parts[0].lower() == 'mods':
        relative = Path(*relative.parts[1:])
    require(relative.parts[0].lower() == 'media_soviet' and 'vehicles' in relative.parts,
            'Select a vehicle folder under this game media_soviet or mods/media_soviet')
    require((game/'C3DDLL64.dll').is_file(), 'Select the game installation folder')
    require(material.lower() not in ('default', '*') and
            not any(c in material for c in '{}\r\n"\\'), 'Unsupported material identifier')
    rgba = np.asarray(Image.open(mask).convert('RGBA'))
    require(mask.suffix.lower() == '.dds' and np.any(rgba[:, :, 3] > 0),
            'glass.dds must have nonzero alpha where windows are painted')
    require(np.any(rgba[:, :, 3] == 0), 'Outside the windows glass.dds alpha must be zero')
    prepared, evidence, originals = {}, [], {}
    meshes = sorted(model.glob('*.nmf'))
    require(any(p.name.lower() == 'main.nmf' for p in meshes), 'main.nmf is missing')
    glass_name = None
    for path in meshes:
        original = path.read_bytes()
        if material not in material_names(original):
            continue
        candidate, name, report = prepare_mesh(original, material, rgba[:, :, 3])
        require(glass_name in (None, name), 'NMF material tables differ across LODs')
        glass_name = name
        prepared[path.name] = candidate
        originals[path] = sha(original)
        evidence.append(dict(file=path.name, before=sha(original), after=sha(candidate), nodes=report))
    require(glass_name and any(n['window_triangles'] for e in evidence for n in e['nodes']),
            'The painted mask does not cover this material geometry')
    materials = sorted(model.glob('*.mtl'))
    for path in materials:
        if path.name.lower().endswith('.pbr.mtl') or path.name.lower() == 'pbr.mtl':
            continue
        data = path.read_bytes()
        idents = re.findall(rb'(?m)^\s*\$SUBMATERIAL[ \t]+([^\r\n]+)', data)
        if material.encode('ascii') not in [i.strip() for i in idents]:
            continue
        prepared[path.name] = prepare_material(data, material, glass_name)
        originals[path] = sha(data)
        for companion in (path.with_suffix('.pbr'), path.with_name(path.stem+'.pbr.mtl')):
            require(not companion.exists(), 'Existing per-paint PBR profile needs an explicit merge: '+companion.name)
    require(any(n.endswith('.mtl') for n in prepared), 'No matching native MTL found')
    existing = model/'pbr.mtl'
    defaults = game/'mods/pbr-defaults.mtl'
    profile = (existing.read_bytes() if existing.exists() else
               defaults.read_bytes() if defaults.exists() else
               b'{workflow = painter}\n{roughness_factor = 0.65}\n')
    require(b'{glass' not in profile.lower(), 'This model already has a glass profile; merge it explicitly')
    profile = b'{preserve_native_diffuse = true}\n'+profile.rstrip()+b'\n'
    profile += ('\n{material '+material+'\n{glass_mask = glass.dds}\n}\n'+
                '{material '+glass_name+'\n{glass = glass.dds}\n{opacity = 1}\n'+
                '{roughness_factor = 0.15}\n{metallic_factor = 0}\n}\n').encode('ascii')
    prepared['pbr.mtl'] = profile
    prepared['glass.dds'] = mask.read_bytes()
    # Recheck inputs before writing any candidate files.
    require(all(sha(p.read_bytes()) == value for p, value in originals.items()),
            'A source model changed during preparation')
    installer = ((Path(sys._MEIPASS)/'Installer') if getattr(sys, 'frozen', False)
                 else Path(__file__).resolve().parent.parent/'Installer')
    support = {name: (installer/name).read_bytes()
               for name in ('Install-Pbr.ps1', 'Install.cmd', 'Restore.cmd')}
    guide = ((Path(sys._MEIPASS)/'README_RU.txt') if getattr(sys, 'frozen', False)
             else Path(__file__).resolve().parent/'README_RU.txt')
    support['README_RU.txt'] = guide.read_bytes()
    output.mkdir(parents=True)
    records = []
    for name, data in prepared.items():
        destination = Path('mods')/relative/name
        target = output/'payload'/destination
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        records.append(dict(relative=destination.as_posix(), sha256=sha(data)))
    for name, data in support.items():
        (output/name).write_bytes(data)
    manifest = dict(version=1, status='experimental', required_features=['glass_dds_v1'],
                    c3d_sha256=sha((game/'C3DDLL64.dll').read_bytes()), files=records)
    (output/'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    (output/'validation.json').write_text(json.dumps(dict(models=evidence,
        native_diffuse_unchanged=True, source_unchanged=True,
        mixed_triangles=sum(n['mixed'] for e in evidence for n in e['nodes']),
        limitation='Mixed triangles keep opaque-part geometry in the local shadow caster'), indent=2), encoding='utf-8')
    return output, evidence


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('game', 'model', 'mask', 'material', 'output'):
        parser.add_argument('--'+name, required=True)
    args = parser.parse_args()
    output, evidence = build_package(**vars(args))
    print(json.dumps(dict(package=str(output), models=evidence), indent=2))


if __name__ == '__main__':
    main()
