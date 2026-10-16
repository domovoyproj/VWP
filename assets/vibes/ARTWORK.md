# Vibe Collection — original artwork

Twenty background plates for VWP. Each plate has a companion `.prompt.txt` with its visual specification. Source images are kept alongside the renderer.

The landscape sources are 1672 × 941. Master movies are exported at 3840 × 2160 / 60 FPS. This is a 4K export of scaled source artwork with native-resolution motion, **not native 4K source illustration or generative character video**.

`vibes.js` animates masked reflections, cloud drift, rain, petals, mist, embers, fireflies, aurora and refractive glass. Orbital Bath includes an independently rotating 3D sphere. Midnight Record has a rotating record label. Separate character sprites were removed in 0.9.1; the original backgrounds and their environmental animation remain.

All motion uses a 12-second periodic phase. Native frame checks compare 0, 5 and 12 seconds. The release build decodes every movie and checks resolution, frame rate, duration, changing pixels and composed posters. Posters are renderer outputs and are never substituted for the animated wallpaper movie.

The twenty matching cursor packs are original code-native artwork under CC0 1.0, with 17 Windows roles, five sizes and animated busy indicators. Their reproducible source is `tools/make_vibe_styles.py` with the shared native encoder in `tools/make_cursors.py`.
