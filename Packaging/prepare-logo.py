from pathlib import Path
import sys
from PIL import Image

source, target = map(Path, sys.argv[1:])
image = Image.open(source).convert('RGBA')
image.thumbnail((256, 256), Image.Resampling.LANCZOS)
frame = Image.new('RGBA', (256, 256))
frame.alpha_composite(image, ((256-image.width)//2, (256-image.height)//2))
frame.save(target, format='PNG')
