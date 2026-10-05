# FFmpeg 7.1 Windows x64

VWP runs the FFmpeg encoder as a separate process for local video editing and optional performance proxies. The binary is bundled from imageio-ffmpeg 0.6.0 (Windows x64), SHA-256: `2ce797a0f88d7f067180338fb227f7b1928ea727bd9a4d7a1d022f7c52af71a3`.

This FFmpeg build enables GPL components and version 3. GPL-3.0.txt is included in this folder. FFmpeg is Copyright (c) the FFmpeg developers; other linked libraries retain their respective copyrights.

- Encoder distribution and build provenance: https://github.com/imageio/imageio-ffmpeg/tree/v0.6.0
- Binary supplier and source/build information: https://www.gyan.dev/ffmpeg/builds/
- FFmpeg 7.1 source: https://ffmpeg.org/releases/ffmpeg-7.1.tar.xz
- GPL and source redistribution information: https://ffmpeg.org/legal.html
- x264 source: https://code.videolan.org/videolan/x264

The application does not send imported videos or captured audio to these services. WASAPI audio is measured in memory and is never recorded to a file by VWP.
