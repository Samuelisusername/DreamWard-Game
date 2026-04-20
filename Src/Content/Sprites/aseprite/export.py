import json
import shutil
import subprocess

MERGE_DIRS = ["SW", "NW", "N", "NE", "SE", "S"]

ASEPRITE_AVAILABLE = shutil.which("aseprite") is not None
if not ASEPRITE_AVAILABLE:
    print("WARNING: aseprite not found. skipping sprite export, JSON patching only")

def run_aseprite(*args):
    if not ASEPRITE_AVAILABLE:
        return
    subprocess.run(["aseprite", "-b", *args], check=True)

# Export all aseprite files
import glob
for f in sorted(glob.glob("*.aseprite")):
    base = f.removesuffix(".aseprite")
    run_aseprite(
        f, "--color-mode", "rgb",
        "--sheet", f"../{base}.png", "--sheet-type", "horizontal",
        "--data", f"../{base}.json", "--list-slices", "--list-tags",
    )

# Patch monsters.json with oneShotTransition entries
with open("../monsters.json") as f:
    data = json.load(f)

data["meta"]["oneShotTransition"] = [
    transition
    for d in MERGE_DIRS
    for transition in [
        {"from": f"merge{d}",        "to": "smokeWalk"},
        {"from": f"mergeIdle{d}",    "to": "smokeWalk"},
        {"from": f"rebuild{d}",      "to": f"walk{d}"},
        {"from": f"rebuildIdle{d}",  "to": f"idle{d}"},
        {"from": f"rebuildIdle{d}",  "to": f"attack{d}"},

    ]
]

with open("../monsters.json", "w") as f:
    json.dump(data, f, indent=2)

run_aseprite(
    "icon.aseprite", "--color-mode", "rgb",
    "--sheet", "../../../Icon.ico", "--sheet-type", "horizontal",
)

# important that the layer part is before the file part
# json is already exported earlier
run_aseprite(
    "--ignore-layer", "flat", "--ignore-layer", "meadow", "--ignore-layer", "shadow",
    "day_top.aseprite", "--color-mode", "rgb",
    "--sheet", "../day_top.png", "--sheet-type", "horizontal",
)

run_aseprite(
    "--layer", "flat", "--layer", "meadow", "--layer", "shadow",
    "day_top.aseprite", "--color-mode", "rgb",
    "--sheet", "../day_top_base.png", "--sheet-type", "horizontal",
    "--data", "../day_top_base.json", "--list-slices", "--list-tags",
)

run_aseprite(
    "--ignore-layer", "flat", "--ignore-layer", "meadow", "--ignore-layer", "shadow",
    "night_top.aseprite", "--color-mode", "rgb",
    "--sheet", "../night_top.png", "--sheet-type", "horizontal",
)

run_aseprite(
    "--layer", "flat", "--layer", "meadow", "--layer", "shadow",
    "night_top.aseprite", "--color-mode", "rgb",
    "--sheet", "../night_top_base.png", "--sheet-type", "horizontal",
    "--data", "../night_top_base.json", "--list-slices", "--list-tags",
)
