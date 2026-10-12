# Vibe Collection — original artwork

Twenty original clean plates and two original adult character layers were created for VWP using the built-in ImageGen tool. No paid external video service is used. Every plate has a sibling `.prompt.txt`; the two character prompts are in `characters/`. The source images remain preserved alongside the renderer.

The landscape sources are 1672 × 941; character layers are 1024 × 1536 RGBA. Master movies are exported at 3840 × 2160 / 60 FPS. This is a 4K export of scaled source artwork with native-resolution motion, **not native 4K source illustration or generative character video**.

`vibes.js` animates masked reflections, cloud drift, rain, petals, mist, embers, fireflies, aurora and refractive glass. Orbital Bath includes an independently rotating 3D sphere. Midnight Record has a rotating record label. Midnight Record and Summer Rooftop have independent transparent character layers with gentle breathing, hair movement, blinking and hand/pose deformation. These are original adult women, not depictions of identifiable people.

All motion uses a 12-second periodic phase. Native frame checks compare 0, 5 and 12 seconds. Character movement is checked in isolation from the background. The release build decodes every movie and checks resolution, frame rate, duration, changing pixels and composed posters. Posters are renderer outputs and are never substituted for the animated wallpaper movie.

The twenty matching cursor packs are original code-native artwork under CC0 1.0, with 17 Windows roles, five sizes and animated busy indicators. Their reproducible source is `tools/make_vibe_styles.py` with the shared native encoder in `tools/make_cursors.py`.
