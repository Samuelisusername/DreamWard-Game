for f in *.aseprite; do
  aseprite -b "$f" --color-mode rgb \
  --sheet "../${f%.aseprite}.png" --sheet-type horizontal \
  --data "../${f%.aseprite}.json" --list-slices --list-tags
done

# important that the layer part is before the file part
aseprite -b --ignore-layer flat --ignore-layer meadow --ignore-layer shadow \
  day_top.aseprite --color-mode rgb \
  --sheet "../day_top.png" --sheet-type horizontal \

aseprite -b --layer flat --layer meadow --layer shadow \
  day_top.aseprite --color-mode rgb \
  --sheet "../day_top_base.png" --sheet-type horizontal \
  --data "../day_top_base.json" --list-slices --list-tags

aseprite -b --ignore-layer flat --ignore-layer meadow --ignore-layer shadow \
  night_top.aseprite --color-mode rgb \
  --sheet "../night_top.png" --sheet-type horizontal \

aseprite -b --layer flat --layer meadow --layer shadow \
  night_top.aseprite --color-mode rgb \
  --sheet "../night_top_base.png" --sheet-type horizontal \
  --data "../night_top_base.json" --list-slices --list-tags
