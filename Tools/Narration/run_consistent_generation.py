"""Background job: wait for checkpoint, smoke generate, resume all cues, verify."""
import json
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STATUS = ROOT / "Tools/Narration/job-status.json"
BASE = ROOT / "Tools/Narration/Models/Qwen3-TTS-12Hz-1.7B-Base"
BACKUP = ROOT / "Backups/Narration/20261008-172128-VoiceDesign/English"

def status(stage, error=""):
    STATUS.write_text(json.dumps(dict(stage=stage, error=error, updated=time.strftime("%Y-%m-%d %H:%M:%S")), indent=2))
    print(stage, flush=True)

def run(script, *args):
    subprocess.run([sys.executable, str(ROOT / "Tools/Narration" / script), *args], cwd=ROOT, check=True)

if __name__ == "__main__":
    try:
        status("waiting-for-clone-checkpoint")
        deadline = time.time() + 7200
        while not all((BASE / name).is_file() for name in ("model.safetensors", "speech_tokenizer/model.safetensors")):
            if time.time() > deadline:
                raise TimeoutError("Checkpoint download has not completed; original voices remain backed up.")
            time.sleep(15)
        status("checking-first-three-lines")
        run("generate_narration.py", "--limit", "3")
        # Inspect actual exported audio, not just whether the inference call returned.
        import numpy as np
        import soundfile as sf
        manifest = json.loads((ROOT / "Assets/Audio/Narration/English/manifest.json").read_text())
        for entry in manifest["clips"]:
            samples, rate = sf.read(ROOT / "Assets/Audio/Narration/English" / entry["file"])
            if rate != 24000 or not np.isfinite(samples).all() or np.sqrt(np.mean(samples*samples)) < .0005:
                raise RuntimeError("First-line waveform check failed: " + entry["id"])
        status("generating-all-lines")
        run("generate_narration.py")
        # Preserve Unity GUIDs even if an Editor cleaned up an orphan meta while
        # the previous WAV files were temporarily in the backup directory.
        output = ROOT / "Assets/Audio/Narration/English"
        for original in BACKUP.rglob("*.wav.meta"):
            target = output / original.relative_to(BACKUP)
            if not target.with_suffix("").is_file():
                continue
            original_guid = re.search(r"^guid: (.+)$", original.read_text(), re.M).group(1)
            current_guid = re.search(r"^guid: (.+)$", target.read_text(), re.M) if target.is_file() else None
            if not current_guid or current_guid.group(1) != original_guid:
                target.write_bytes(original.read_bytes())
        status("verifying-exports")
        run("verify_audio.py")
        status("complete")
    except Exception as error:
        status("failed", str(error))
        raise
