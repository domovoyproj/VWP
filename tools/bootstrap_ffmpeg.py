"""Copy the pinned Windows encoder into the app bundle, verifying its exact bytes."""
from pathlib import Path
import hashlib
import shutil
import imageio_ffmpeg
EXPECTED='2ce797a0f88d7f067180338fb227f7b1928ea727bd9a4d7a1d022f7c52af71a3'
source=Path(imageio_ffmpeg.get_ffmpeg_exe())
assert hashlib.sha256(source.read_bytes()).hexdigest()==EXPECTED,'Install imageio-ffmpeg==0.6.0 with its Windows x64 binary.'
destination=Path(__file__).resolve().parents[1]/'tools'/'ffmpeg.exe'
shutil.copyfile(source,destination)
print('Bundled FFmpeg 7.1 x64, SHA-256 verified.')
