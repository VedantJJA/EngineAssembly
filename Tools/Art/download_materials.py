"""Download the selected CC0 texture maps from their official ambientCG pages."""
import io
import json
import urllib.request
import zipfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

root = Path(__file__).resolve().parents[2]
target = root / "Assets/Art/Textures/ambientCG"
target.mkdir(parents=True, exist_ok=True)


def download(asset):
    url = f"https://ambientcg.com/get?file={asset}_1K-JPG.zip"
    request = urllib.request.Request(url, headers={"User-Agent": "EngineAssembly asset preparation"})
    data = urllib.request.urlopen(request, timeout=60).read()
    folder = target / asset
    folder.mkdir(exist_ok=True)
    maps = []
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for name in archive.namelist():
            filename = Path(name).name
            if filename.endswith(("_Color.jpg", "_Roughness.jpg", "_NormalGL.jpg")):
                (folder / filename).write_bytes(archive.read(name))
                maps.append(filename)
    return {"asset": asset, "page": f"https://ambientcg.com/view?id={asset}", "download": url, "license": "CC0", "maps": maps}


with ThreadPoolExecutor(max_workers=3) as pool:
    sources = list(pool.map(download, ["Metal006", "Metal012", "Rubber004"]))
(target / "sources.json").write_text(json.dumps(sources, indent=2), encoding="utf-8")
print(json.dumps(sources, indent=2))
