"""Derive a reload-ready GLB without changing the supplied M16 model or textures."""
from pathlib import Path
import collections
import copy
import json
import struct

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/_Project/Art/Items/m16_gun.glb"
OUTPUT = ROOT / "Assets/_Project/Art/Items/Generated/m16_reload.glb"
COMPONENT = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


def build():
    data = SOURCE.read_bytes()
    size = struct.unpack_from("<I", data, 12)[0]
    document = json.loads(data[20:20 + size])
    binary = bytearray(data[28 + size:28 + size + document["buffers"][0]["byteLength"]])

    def read(index):
        accessor = document["accessors"][index]
        view = document["bufferViews"][accessor["bufferView"]]
        code, width = COMPONENT[accessor["componentType"]]
        count = WIDTH[accessor["type"]]
        offset = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        stride = view.get("byteStride", width * count)
        return [struct.unpack_from("<" + code * count, binary, offset + i * stride) for i in range(accessor["count"])]

    positions, parents, welded = [], [], {}
    records = []

    def find(index):
        while parents[index] != index:
            parents[index] = parents[parents[index]]
            index = parents[index]
        return index

    def join(a, b):
        a, b = find(a), find(b)
        if a != b:
            parents[b] = a

    for mesh in document["meshes"]:
        for primitive in mesh["primitives"]:
            vertices = read(primitive["attributes"]["POSITION"])
            ids = []
            for vertex in vertices:
                key = tuple(round(value, 5) for value in vertex)
                if key not in welded:
                    welded[key] = len(parents)
                    parents.append(len(parents))
                    positions.append(vertex)
                ids.append(welded[key])
            indices = [value[0] for value in read(primitive["indices"])]
            for i in range(0, len(indices), 3):
                a, b, c = (ids[index] for index in indices[i:i + 3])
                join(a, b)
                join(b, c)
            records.append((mesh, primitive, ids, indices))

    groups = collections.defaultdict(list)
    for index, position in enumerate(positions):
        groups[find(index)].append(position)
    candidates = []
    for group, vertices in groups.items():
        low = [min(v[k] for v in vertices) for k in range(3)]
        high = [max(v[k] for v in vertices) for k in range(3)]
        # The forward, long rectangular component below the receiver; excludes the rear pistol grip.
        if len(vertices) > 1000 and .4 < low[0] < .7 and 2.3 < high[0] < 2.7 and low[2] < -3 and high[2] < 0:
            candidates.append(group)
    assert len(candidates) == 1, "Magazine component must be uniquely identified"
    magazine = candidates[0]

    def append(values, template, target):
        while len(binary) % 4:
            binary.append(0)
        offset = len(binary)
        code, _ = COMPONENT[template["componentType"]]
        for value in values:
            binary.extend(struct.pack("<" + code * len(value), *value))
        view = len(document["bufferViews"])
        document["bufferViews"].append({"buffer": 0, "byteOffset": offset, "byteLength": len(binary) - offset, "target": target})
        accessor = {"bufferView": view, "componentType": template["componentType"], "count": len(values), "type": template["type"]}
        if template.get("normalized"):
            accessor["normalized"] = True
        if template["type"] == "VEC3" and template["componentType"] == 5126:
            accessor["min"] = [min(v[k] for v in values) for k in range(3)]
            accessor["max"] = [max(v[k] for v in values) for k in range(3)]
        index = len(document["accessors"])
        document["accessors"].append(accessor)
        return index

    def subset(primitive, indices):
        used = sorted(set(indices))
        remap = {value: i for i, value in enumerate(used)}
        result = copy.deepcopy(primitive)
        result["attributes"] = {}
        for semantic, index in primitive["attributes"].items():
            original = read(index)
            result["attributes"][semantic] = append([original[i] for i in used], document["accessors"][index], 34962)
        result["indices"] = append([(remap[i],) for i in indices], {"componentType": 5125, "type": "SCALAR"}, 34963)
        return result

    magazines = []
    removed = 0
    for mesh, primitive, ids, indices in records:
        body, detached = [], []
        for i in range(0, len(indices), 3):
            triangle = indices[i:i + 3]
            chosen = find(ids[triangle[0]]) == magazine
            assert all((find(ids[index]) == magazine) == chosen for index in triangle)
            (detached if chosen else body).extend(triangle)
        if not detached:
            continue
        assert body, "Unexpected mesh containing only magazine geometry"
        magazines.append(subset(primitive, detached))
        mesh["primitives"][mesh["primitives"].index(primitive)] = subset(primitive, body)
        removed += len(detached) // 3
    assert magazines and removed > 0
    mesh_index = len(document["meshes"])
    document["meshes"].append({"name": "bm_magazine_m16", "primitives": magazines})
    node_index = len(document["nodes"])
    document["nodes"].append({"name": "bm_magazine_m16", "mesh": mesh_index})
    document["nodes"][1]["children"].append(node_index)
    document["buffers"][0]["byteLength"] = len(binary)
    document["asset"]["generator"] += " / ShadowVale magazine separation"
    encoded = json.dumps(document, separators=(",", ":")).encode()
    encoded += b" " * (-len(encoded) % 4)
    binary.extend(b"\0" * (-len(binary) % 4))
    total = 12 + 8 + len(encoded) + 8 + len(binary)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(struct.pack("<III", 0x46546C67, 2, total) + struct.pack("<II", len(encoded), 0x4E4F534A) + encoded + struct.pack("<II", len(binary), 0x004E4942) + binary)
    print(f"Separated magazine: {removed} triangles, {len(magazines)} primitives; wrote {OUTPUT}")


if __name__ == "__main__":
    build()
