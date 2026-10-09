"""Check the ten pre-rendered desktop videos before packaging."""
from pathlib import Path
import imageio_ffmpeg

root = Path(__file__).resolve().parents[1]
for scene_id in range(18, 28):
    video = root / "assets" / "cinematic" / f"{scene_id}.mp4"
    if not video.is_file() or video.stat().st_size < 100_000:
        raise SystemExit(f"Missing cinematic video: {video}")
    reader = imageio_ffmpeg.read_frames(str(video), pix_fmt="rgb24", output_params=["-frames:v", "1"])
    info = next(reader)
    first = next(reader)
    reader.close()
    if tuple(info["size"]) != (3840, 2160) or abs(info["fps"] - 60) > .01 or abs(info["duration"] - 12) > .1 or len(first) < 3840 * 2160 * 3:
        raise SystemExit(f"Wrong or undecodable cinematic video for {scene_id}: {info}")
print("All ten cinematic desktop videos decode at 3840x2160, 60 FPS, 12 seconds")
