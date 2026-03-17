# Bash (macOS/Linux)
for f in *.aseprite; do
  aseprite -b "$f" --color-mode rgb --sheet "test_exp/${f%.aseprite}.png" --sheet-type horizontal
done