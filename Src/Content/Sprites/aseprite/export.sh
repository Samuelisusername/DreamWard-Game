# Bash (macOS/Linux)
for f in *.aseprite; do
  aseprite -b "$f" --color-mode rgb --sheet "../${f%.aseprite}.png" --sheet-type horizontal --data "../${f%.aseprite}.json" --list-slices
done
