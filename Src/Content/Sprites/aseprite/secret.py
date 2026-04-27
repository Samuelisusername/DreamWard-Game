import sys
import random
from PIL import Image

def encode(text: str = "flag\\{uuddlrlrba\\}", p: float = 0.01, gap: int = 10, out: str = "../night_background.png") -> None:
    random.seed(text)
    bits = "".join(f"{byte:08b}" for byte in text.encode("utf-8"))
    width, height = 640, 360
    stride = gap + 1
    capacity = (width * height) // stride
    if len(bits) > capacity:
        raise ValueError(f"String too long: {len(bits)} bits needed, {capacity} available")
    img = Image.new("RGB", (width, height), "black")
    pixels = img.load()
    for i, bit in enumerate(bits):
        j = i * stride
        x, y = j % width, j // width
        if bit == "1":
            pixels[x, y] = (234, 234, 234)
    for j in range(len(bits) * stride, width * height):
        x, y = j % width, j // width
        if random.random() < p:
            pixels[x, y] = (234, 234, 234)
    img.save(out)
    print(f"Encoded {len(bits)} bits into {out} (gap={gap}, padding p={p})")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python secret.py <string> [p] [gap] [output.png]")
        sys.exit(1)
    p = float(sys.argv[2]) if len(sys.argv) > 2 else 0.5
    gap = int(sys.argv[3]) if len(sys.argv) > 3 else 1
    if len(sys.argv) > 4:
        encode(sys.argv[1], p, gap, sys.argv[4])
    else:
        encode(sys.argv[1], p, gap)
