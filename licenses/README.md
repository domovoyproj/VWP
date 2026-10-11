# Bundled components

VWP uses dynamically loaded third-party libraries. Their license texts are provided alongside the executable; the managed and native DLLs remain separate and replaceable. The Three.js GLTFLoader addon has one relative import adjusted to the bundled directory layout.

- LibVLCSharp 3.9.7: https://github.com/videolan/libvlcsharp/tree/3.9.7
- VideoLAN.LibVLC.Windows 3.0.23.1 package: https://www.nuget.org/packages/VideoLAN.LibVLC.Windows/3.0.23.1
- VLC/LibVLC native build revision 79128878dd: https://code.videolan.org/videolan/vlc/-/tree/79128878dd
- .NET 8 runtime sources: https://github.com/dotnet/runtime/tree/v8.0.31
- Windows Desktop runtime sources: https://github.com/dotnet/wpf/tree/v8.0.31 and https://github.com/dotnet/winforms/tree/v8.0.31
- Three.js r180 and its bundled addons: https://github.com/mrdoob/three.js/tree/r180 (MIT; `Three-MIT.txt` and `assets/city/vendor/LICENSE.txt`).
- Microsoft.Web.WebView2 1.0.3405.78 SDK: https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.3405.78 (`WebView2-LICENSE.txt`). The Evergreen Runtime is distributed separately by Microsoft.
- ambientCG Facade001 color map: https://ambientcg.com/a/Facade001 (CC0-1.0; source and license URL in `assets/city/textures/sources.json`).
- Street characters, hairstyles and walking animations: Quaternius, https://quaternius.com/packs/universalbasecharacters.html and https://opengameart.org/content/universal-animation-library (CC0-1.0; original license files in `assets/city/models/`). VWP retargets the animations and adds its own street clothing shader.
- Car Concept: Eric Chadwick / Darmstadt Graphics Group GmbH, 2024, https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/CarConcept (CC-BY-4.0; full license and source metadata in `assets/city/models/`). VWP batches the mesh, changes paint and lights, and omits emblem/license-plate meshes. Logos and trademarks remain the property of their owners; this is not an endorsement.
- Shanghai Bund environment lighting: Greg Zaal / Poly Haven, https://polyhaven.com/a/shanghai_bund (CC0-1.0, https://polyhaven.com/license). Used for material illumination and reflections, not as the city background.
- Brick Wall 003 and Concrete Wall 007 colour/normal maps: https://polyhaven.com/a/brick_wall_003 and https://polyhaven.com/a/concrete_wall_007 (CC0-1.0; 2K downloads and attribution recorded in `assets/city/textures/sources.json`).

The license texts in this directory apply to those components, not to the original illustration assets.
