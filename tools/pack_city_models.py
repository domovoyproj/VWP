"""Pack the downloaded CC0 glTF assets without changing their meshes or images."""
import json
import pathlib
import struct

ROOT = pathlib.Path(__file__).resolve().parents[1]


def pack(source, destination):
    document = json.loads(source.read_text(encoding="utf-8"))
    binary = bytearray()
    offsets = []
    for buffer in document.get("buffers", []):
        while len(binary) % 4:
            binary.append(0)
        offsets.append(len(binary))
        binary.extend((source.parent / buffer["uri"]).read_bytes())
    for view in document.get("bufferViews", []):
        view["byteOffset"] = view.get("byteOffset", 0) + offsets[view["buffer"]]
        view["buffer"] = 0
    for image in document.get("images", []):
        if "uri" not in image:
            continue
        image_path = source.parent / image.pop("uri")
        if not image_path.exists():
            image_path = image_path.with_name(image_path.name.replace("_png.png", ".png"))
        payload = image_path.read_bytes()
        while len(binary) % 4:
            binary.append(0)
        image["bufferView"] = len(document.setdefault("bufferViews", []))
        document["bufferViews"].append({"buffer": 0, "byteOffset": len(binary), "byteLength": len(payload)})
        binary.extend(payload)
    document["buffers"] = [{"byteLength": len(binary)}]
    encoded = json.dumps(document, separators=(",", ":")).encode()
    encoded += b" " * (-len(encoded) % 4)
    binary += b"\0" * (-len(binary) % 4)
    destination.write_bytes(struct.pack("<III", 0x46546C67, 2, 28 + len(encoded) + len(binary)) +
                            struct.pack("<II", len(encoded), 0x4E4F534A) + encoded +
                            struct.pack("<II", len(binary), 0x004E4942) + binary)
    print(destination.name, len(binary), "bytes")


if __name__ == "__main__":
    downloaded = ROOT / "artifacts/city-assets/characters/Universal Base Characters[Standard]"
    models = ROOT / "assets/city/models"
    for gender in ("Male", "Female"):
        pack(downloaded / f"Base Characters/Godot - UE/Superhero_{gender}_FullBody.gltf", models / f"person-{gender.lower()}.glb")
    for name in ("Hair_Buzzed", "Hair_Long"):
        pack(downloaded / f"Hairstyles/Origin at 0/glTF (Godot)/{name}.gltf", models / f"{name}.glb")
