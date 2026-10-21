"""Download the official clone checkpoint into the project, outside Unity Assets."""
import hashlib
import json
import os
import shutil
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
os.environ["HF_HUB_DISABLE_XET"] = "1"
os.environ["HF_HUB_OFFLINE"] = "0"
os.environ["HF_HOME"] = str(ROOT / "Tools/Narration/Models/cache")
from huggingface_hub import HfApi, hf_hub_download
import requests

MODEL = "Qwen/Qwen3-TTS-12Hz-1.7B-Base"
DEST = ROOT / "Tools/Narration/Models/Qwen3-TTS-12Hz-1.7B-Base"

def checksum(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(8*1024*1024), b""):
            h.update(block)
    return h.hexdigest()

def weights(file, revision):
    target = DEST / file.rfilename
    if target.exists() and checksum(target) == file.lfs.sha256:
        return
    if file.rfilename.startswith("speech_tokenizer/"):
        cache = Path.home() / ".cache/huggingface/hub/models--Qwen--Qwen3-TTS-12Hz-1.7B-VoiceDesign"
        ref = (cache / "refs/main").read_text().strip()
        old = cache / "snapshots" / ref / file.rfilename
        if old.is_file() and checksum(old) == file.lfs.sha256:
            shutil.copyfile(old, target)
            return
    chunks = DEST / ".download-parts" / file.lfs.sha256
    chunks.mkdir(parents=True, exist_ok=True)
    size = file.size
    step = 16*1024*1024
    starts = list(range(0, size, step))
    def fetch(start):
        end = min(size-1, start+step-1)
        part = chunks / str(start)
        if part.is_file() and part.stat().st_size == end-start+1:
            return part.stat().st_size
        url = f"https://huggingface.co/{MODEL}/resolve/{revision}/{file.rfilename}?part={start}"
        for attempt in range(5):
            try:
                with requests.get(url, headers={"Range": f"bytes={start}-{end}"}, stream=True, timeout=(20,90)) as response:
                    response.raise_for_status()
                    if response.status_code != 206 or response.headers.get("Content-Range") != f"bytes {start}-{end}/{size}":
                        raise RuntimeError("Server did not honor the exact download range")
                    pending = part.with_suffix(".pending")
                    with pending.open("wb") as stream:
                        for block in response.iter_content(1024*1024):
                            stream.write(block)
                    if pending.stat().st_size != end-start+1:
                        raise RuntimeError("Partial download range")
                    pending.replace(part)
                    return end-start+1
            except Exception:
                if attempt == 4:
                    raise
                time.sleep(2**attempt)
    done = 0
    with ThreadPoolExecutor(max_workers=8) as pool:
        for future in as_completed([pool.submit(fetch, start) for start in starts]):
            done += future.result()
            print(json.dumps(dict(file=file.rfilename, downloaded=done, total=size)), flush=True)
    temporary = target.with_suffix(".pending")
    with temporary.open("wb") as stream:
        for start in starts:
            with (chunks / str(start)).open("rb") as part:
                shutil.copyfileobj(part, stream, length=8*1024*1024)
    if checksum(temporary) != file.lfs.sha256:
        raise RuntimeError("Official model checksum mismatch")
    temporary.replace(target)
    # Remove only verified range files under this checkpoint's staging directory.
    for start in starts:
        (chunks / str(start)).unlink()

if __name__ == "__main__":
    info = HfApi().model_info(MODEL, files_metadata=True)
    for file in info.siblings:
        if file.rfilename.endswith(".safetensors"):
            continue
        hf_hub_download(MODEL, file.rfilename, revision=info.sha, local_dir=DEST)
    for file in info.siblings:
        if file.rfilename.endswith(".safetensors"):
            weights(file, info.sha)
    print(json.dumps(dict(status="complete", model=MODEL, revision=info.sha, folder=str(DEST))), flush=True)
