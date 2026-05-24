import glob
import os
import subprocess

def run_aseprite(*args):
    subprocess.run(["aseprite", "-b", *args], check=True)

for name in ["day_top", "night_top"]:
    run_aseprite(
        f"{name}.aseprite", "--color-mode", "rgb",
        "--oneframe",
        "--scale", "3",
        "--save-as", f"../presi_{name}_{{slice}}.png",
    )

run_aseprite(
    f"logo.aseprite", "--color-mode", "rgb",
    "--scale", "6",
    "--save-as", f"../presi_{name}_{{slice}}.png",
)



KEEP_WORDS = {"tent", "wall", "AOE", "Sniper", "bomb", "tree", "buff", "fence", "decoy"}

# Idk why many frames export so I'll just delete
for path in glob.glob("../presi_*.png"):
    stem = os.path.splitext(os.path.basename(path))[0]
    if stem.endswith("2") or stem.endswith("3") or stem.endswith("4") or not any(w in stem for w in KEEP_WORDS):
        os.remove(path)
