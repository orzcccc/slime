import os
from PIL import Image, ImageDraw
d = r'Assets/Resources/icon'
fs = sorted(f for f in os.listdir(d) if f.endswith('.png'))
W = 300
sheet = Image.new('RGBA', (W * 5, (W + 10) * 3), (90, 90, 90, 255))
for i, f in enumerate(fs):
    im = Image.open(os.path.join(d, f)).convert('RGBA'); im.thumbnail((W - 10, W - 10))
    x, y = (i % 5) * W, (i // 5) * (W + 10)
    sheet.alpha_composite(im, (x + 5, y + 5))
    ImageDraw.Draw(sheet).text((x + 5, y + W - 5), str(i), fill=(255, 255, 0, 255))
    print(i, f, im.getbbox())
sheet.save('.codex_tmp/sheet.png')
