# VWP Engine Studio

Wallpaper Engine is the product reference for VWP's wallpaper workflow.
The implementation uses VWP's existing WPF and LibVLC code and original UI assets.

## Reference research

- [Product overview](https://www.wallpaperengine.io/en): configurable wallpapers, performance policies, display support, playlists, image-based scenes and community sharing.
- [Scene editor](https://docs.wallpaperengine.io/en/scene/overview.html): image layers, particle effects, audio reaction and cursor parallax.
- [User properties](https://docs.wallpaperengine.io/en/scene/userproperties/overview.html): users tune scene properties alongside the selected wallpaper.
- [Application rules and monitor profiles](https://help.wallpaperengine.io/en/functionality/wallpaperperapp.html): policies for running applications and monitor configurations.

## Implemented in this update

The wallpaper page keeps a persistent selected-scene preview beside a responsive library. Cards keep 16:9 artwork, with search across names, descriptions, categories and format labels. Sorting supports names, recent use, favorites first and collections. Monitor selection, volume, pause and apply remain in a dedicated bottom bar.

**Create scene** opens an image as a realtime project. **Customize live scene** creates a configurable scene from the selected wallpaper's full source image when available. The designer supports a background, an optional transparent foreground, cursor parallax, system-audio reaction, clock/date, particle color, speed, density, size, wind and a vertical effect region. Available modes: none, snow, rain, petals, leaves, embers, stars, fireflies, mist, lightning and water ripple. Built-in scene catalog metadata supplies an appropriate starting effect. Changes stay in the preview until Save; Cancel discards edits. Saved artwork is copied into VWP's local scene library.

Live scenes run under the selected monitor's quality profile: 15/30/60 FPS and bounded texture sizes. Image projects are stored in existing settings and travel with their properties and source assets through hash-verified `.vwpbundle` export/import. Existing video wallpapers keep their original playback path until explicitly customized. **Restore original** removes custom properties for a built-in wallpaper.

Playback policies now include maximized foreground windows per display and named running applications. Matching application names is case insensitive; processes are sampled at most once every three seconds. Existing fullscreen, battery, session lock and manual pause behavior remains in place.

## Limits and next steps

This is an image scene editor, not feature parity with Wallpaper Engine. It does not import Steam Workshop scene packages or implement SceneScript, puppet animation, shader authoring, a timeline, or a Workshop backend. Water uses localized image displacement; foreground layers must be supplied as transparent PNGs. Effects on an image do not animate a person's face or body. The WPF desktop surface follows the existing software composition path; no new GPU acceleration claim is made.

## Verification

`live-check --engine-check` renders every effect at two times, checks a still scene with effects and parallax disabled, round-trips an image scene and its properties through BundleService, rejects non-finite property values, checks application matching and metadata search, and renders the designer. It also plays a realtime scene through an embedded native DesktopHost and verifies manual/automatic pause, resume and cleanup without changing the desktop wallpaper.

`VWP --verify-engine` uses isolated verification settings and renders the library at 980, 1160 and 1480 pixels without applying desktop wallpapers. `Engine studio` CI runs both checks and uploads screenshots and reports.
