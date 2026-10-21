"""Validate the exported cue catalogue using the same TTS virtual environment."""
import json
from pathlib import Path
import numpy as np
import soundfile as sf
from generate_narration import ROOT, OUTPUT, PROMPT, PROMPT_INFO, MODEL, SETTINGS, cues, file_hash, reference_identity

report = {"passed": False, "clips": 0, "seconds": 0, "bytes": 0, "checks": [], "error": ""}

def check(value, description):
    if not value:
        raise AssertionError(description)
    report["checks"].append(description)

try:
    expected = cues()
    manifest = json.loads((OUTPUT / "manifest.json").read_text(encoding="utf-8"))
    prompt_info = json.loads(PROMPT_INFO.read_text(encoding="utf-8"))
    prompt_hash = file_hash(PROMPT)
    identity = reference_identity()
    check(manifest["model"] == MODEL and manifest["settings"] == SETTINGS, "Fixed-reference Base checkpoint and requested sampling settings")
    check(manifest["voiceIdentity"] == identity == prompt_info["voiceIdentity"], "One primary voice identity for the complete catalogue")
    check(prompt_info["promptSHA256"] == prompt_hash, "Saved acoustic codes and speaker embedding retain their checksum")
    actual = {item["id"]: item for item in manifest["clips"]}
    check(set(actual) == {item["id"] for item in expected}, "Every assembly, repair and completion cue is exported")
    for item in expected:
        entry = actual[item["id"]]
        path = OUTPUT / item["file"]
        check(entry["textHash"] == item["textHash"] and entry["text"] == item["text"], item["id"] + ": current narration text and voice settings")
        check(entry["seed"] == item["seed"] and entry["voiceIdentity"] == identity and entry["promptSHA256"] == prompt_hash, item["id"] + ": fixed speaker prompt and reproducible per-line seed")
        check(entry["audioSHA256"] == file_hash(path), item["id"] + ": exported waveform checksum")
        info = sf.info(path)
        audio, rate = sf.read(path, dtype="float32")
        check(info.channels == 1 and rate == 24000 and info.subtype == "PCM_16", item["id"] + ": 24 kHz mono PCM16 WAV")
        check(np.isfinite(audio).all() and len(audio) >= rate // 2 and np.max(np.abs(audio)) > .001 and np.sqrt(np.mean(audio * audio)) > .0005, item["id"] + ": nonempty, finite, audible waveform")
        check(abs(info.duration - entry["seconds"]) < .002, item["id"] + ": manifest duration matches waveform")
        report["clips"] += 1
        report["seconds"] += info.duration
        report["bytes"] += path.stat().st_size
    check(len(list(OUTPUT.rglob("*.wav"))) == len(expected), "No stale extra WAV files in the export folder")
    check(not list(OUTPUT.rglob("*.pending")), "No incomplete audio exports remain")
    report["passed"] = True
except Exception as error:
    report["error"] = str(error)
finally:
    report["seconds"] = round(report["seconds"], 2)
    (ROOT / "Tools/Narration/audio-verification.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({key: value for key, value in report.items() if key != "checks"}, indent=2))
    if not report["passed"]:
        raise SystemExit(1)
