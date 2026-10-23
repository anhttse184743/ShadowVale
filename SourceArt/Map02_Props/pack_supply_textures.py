"""Second step of prepare_supply_props.py: python pack_supply_textures.py <texture dir>

Resizes each prop's maps (a source with only a colour map keeps only that) to its budget and packs metallic (R) and smoothness = 1 - roughness (A) into one
URP Lit _MetallicGlossMap, writing <Name>_Albedo/_Normal/_MetalSmooth.png next to the prop FBX files.
"""
import os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "Props", "Map02", "Supplies"))
src = sys.argv[1]
SIZES = {"GrenadeCrate": 1024, "AmmoCan": 1024, "MedkitBag": 1024, "Grenade": 512, "StrawCart": 1024}
for name, px in SIZES.items():
    def load(kind, mode): return Image.open(os.path.join(src, f"{name}_{kind}.png")).convert(mode).resize((px, px), Image.LANCZOS)
    load("Albedo", "RGB").save(os.path.join(OUT, f"{name}_Albedo.png"))
    if not os.path.exists(os.path.join(src, f"{name}_Normal.png")):   # a colour-only source (the cart): matte, no relief map
        print("packed", name, px, "(albedo only)"); continue
    load("Normal", "RGB").save(os.path.join(OUT, f"{name}_Normal.png"))
    metal = load("Metallic", "L"); smooth = load("Roughness", "L").point(lambda v: 255 - v)
    Image.merge("RGBA", (metal, metal, metal, smooth)).save(os.path.join(OUT, f"{name}_MetalSmooth.png"))
    print("packed", name, px)
