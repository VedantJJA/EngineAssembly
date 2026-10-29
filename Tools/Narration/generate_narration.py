"""Generate all cues with one fixed Qwen acoustic reference and speaker embedding.
Run in the user's qwen3tts venv. Qwen's supported Base API takes
voice_clone_prompt, not get_speaker_embedding or cross-line decoder hidden states.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import time
from pathlib import Path

os.environ.setdefault("HF_HUB_OFFLINE", "1")
os.environ.setdefault("TRANSFORMERS_OFFLINE", "1")
os.environ.setdefault("TOKENIZERS_PARALLELISM", "false")
ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/Audio/Narration/English"
STATE = ROOT / "Tools/Narration/progress.json"
VOICE_FOLDER = ROOT / "Tools/Narration/Voice"
REFERENCE = VOICE_FOLDER / "reference.wav"
PROMPT = VOICE_FOLDER / "voice_clone_prompt.pt"
PROMPT_INFO = VOICE_FOLDER / "voice_clone_prompt.json"
MODEL = "Qwen/Qwen3-TTS-12Hz-1.7B-Base"
MODEL_PATH = ROOT / "Tools/Narration/Models/Qwen3-TTS-12Hz-1.7B-Base"
VOICE = "Fixed acoustic reference and speaker embedding from the local main.py test voice."
REF_TEXT = ("Turn the block upside down. Fit the two needle bearings and four upper "
            "main-bearing shells into their matching crankshaft bearing positions.")
SEED = 1234
SETTINGS = dict(temperature=0.7, repetition_penalty=1.1, subtalker_temperature=0.7,
                do_sample=True, subtalker_dosample=True, max_new_tokens=1024)
VERSION = "fixed-reference-icl-v1"

def file_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def reference_identity():
    value = dict(model=MODEL, referenceSHA256=file_hash(REFERENCE), referenceText=REF_TEXT,
                 settings=SETTINGS, pipeline=VERSION)
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()

def digest(text, index):
    value = dict(voiceIdentity=reference_identity(), text=text, seed=SEED + index)
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()

def accessible(text):
    for command in ("Turn the engine upright. ", "Turn the engine upside down. ",
                    "Turn the block upside down. ", "Turn the workpiece upright. ",
                    "Turn the workpiece upside down. "):
        text = text.replace(command, "")
    return text

def cues():
    data = json.loads((ROOT / "Assets/Resources/EngineLearning.json").read_text(encoding="utf-8"))
    shared = {}
    result = []
    for step in data["steps"]:
        identity = step.get("voiceId") or step["id"]
        text = accessible(step["narration"])
        if identity in shared:
            if shared[identity] != text:
                raise ValueError("Shared instruction texts differ: " + identity)
            continue
        shared[identity] = text
        # Preserve the seed of this voice's first original instruction so the
        # existing locked-speaker exports can be reused when text is unchanged.
        source_index = int(step["id"].rsplit("-", 1)[1])-1
        if source_index >= 167:
            source_index = 300 + source_index - 167
        result.append(dict(id=identity, text=text, folder="Assembly", sourceIndex=source_index))
    repair = (ROOT / "Docs/RepairNarration.txt").read_text(encoding="utf-8")
    paragraphs = re.split(r"\n\s*\n", repair.strip())
    result.append(dict(id="repair-introduction", text=paragraphs[1], folder="Repair"))
    for number, text in re.findall(r"Step (\d+)\.\s*(.*?)(?=\n\s*\n|\Z)", repair, re.S):
        result.append(dict(id="repair-step-" + str(int(number)-1), text=text.strip(), folder="Repair"))
    result += [
        dict(id="complete", text="Section complete. Well done.", folder="Assembly"),
        dict(id="repair-complete", text="Repair complete. The replacement is fitted. Well done.", folder="Repair"),
        dict(id="flip-upright", text="Turn the active workpiece upright before fitting the next part. Press F, or press the flip button beside its lift.", folder="Assembly"),
        dict(id="flip-upside-down", text="Turn the active workpiece upside down before fitting the next part. Press F, or press the flip button beside its lift.", folder="Assembly"),
        dict(id="repeat-piston", text="Build the remaining piston units using the same steps.", folder="Assembly",sourceIndex=187),
        dict(id="repeat-valve", text="Repeat this fitting step for the remaining matching valve parts.", folder="Assembly",sourceIndex=188),
        dict(id="repeat-matching", text="Repeat this step for the remaining matching parts.", folder="Assembly",sourceIndex=189),
        dict(id="flip-upside-down-spatial", text="Turn the assembly upside down before fitting the next part. Press the red flip button on the lift control panel, then wait for the lift to finish turning.", folder="Assembly",sourceIndex=4000000),
        dict(id="flip-upright-spatial", text="Turn the assembly upright before fitting the next part. Press the red flip button on the lift control panel, then wait for the lift to finish turning.", folder="Assembly",sourceIndex=4000001),
    ]
    for detail in data['details']:
        identity = detail['voiceId']
        if identity not in shared:
            shared[identity] = detail['description']
            result.append(dict(id=identity, text=detail['description'].replace('\n\n', ' '), folder='Display',
                               sourceIndex=5000+int(identity[7:15],16)%1000000))
    operation = json.loads((ROOT / "Assets/Resources/EngineOperationNarration.json").read_text(encoding="utf-8"))
    for index, cue in enumerate(operation["cues"]):
        result.append(dict(id=cue["id"], text=cue["text"], folder="Operation", sourceIndex=2000000+index))
    tutorial = json.loads((ROOT / "Assets/Resources/WorkshopTutorial.json").read_text(encoding="utf-8"))
    for index, cue in enumerate(tutorial["lessons"]):
        result.append(dict(id=cue["id"], text=cue["text"], folder="Tutorial", sourceIndex=3000000+index))
    identity = reference_identity()
    # Keep the original repair/completion seeds stable when new assembly cues
    # are inserted. New assembly cue indices use a separate range above.
    companion_index = 167
    for item in result:
        if "sourceIndex" not in item:
            item["sourceIndex"] = companion_index
            companion_index += 1
        index = item["sourceIndex"]
        item.update(file=f"{item['folder']}/{item['id']}.wav", seed=SEED+index,
                    textHash=digest(item["text"], index), voiceIdentity=identity)
    text_path=ROOT/'Assets/Resources/NarrationText.json'
    existing={c['id']:c for c in json.loads(text_path.read_text()).get('cues',[])} if text_path.exists() else {}
    atomic_json(text_path, {'cues': [existing[i['id']] if i['id'] in existing and existing[i['id']]['text']==i['text'] else dict(id=i['id'],text=i['text']) for i in result]})
    return result

def atomic_json(path, value):
    temporary = path.with_suffix(path.suffix + ".pending")
    temporary.write_text(json.dumps(value, indent=2), encoding="utf-8")
    temporary.replace(path)

def prepare_prompt(model, torch):
    from qwen_tts.inference.qwen3_tts_model import VoiceClonePromptItem
    identity = reference_identity()
    if PROMPT.is_file() and PROMPT_INFO.is_file() and json.loads(PROMPT_INFO.read_text())["voiceIdentity"] == identity:
        payload = torch.load(PROMPT, map_location="cpu", weights_only=True)
        if file_hash(PROMPT) != json.loads(PROMPT_INFO.read_text())["promptSHA256"]:
            raise RuntimeError("Saved prompt checksum mismatch")
        prompt = [VoiceClonePromptItem(**payload)]
    else:
        # Extract acoustic codes and the speaker vector exactly once.
        prompt = model.create_voice_clone_prompt(ref_audio=str(REFERENCE), ref_text=REF_TEXT,
                                                x_vector_only_mode=False)
        item = prompt[0]
        payload = dict(ref_code=item.ref_code.detach().cpu(),
                       ref_spk_embedding=item.ref_spk_embedding.detach().cpu(),
                       ref_text=item.ref_text, icl_mode=item.icl_mode,
                       x_vector_only_mode=item.x_vector_only_mode)
        temporary = PROMPT.with_suffix(".pending")
        torch.save(payload, temporary)
        temporary.replace(PROMPT)
        atomic_json(PROMPT_INFO, dict(voiceIdentity=identity, model=MODEL,
            referenceSHA256=file_hash(REFERENCE), referenceText=REF_TEXT,
            acousticCodeShape=list(payload["ref_code"].shape),
            speakerEmbeddingShape=list(payload["ref_spk_embedding"].shape),
            promptSHA256=file_hash(PROMPT)))
    return prompt

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--limit", type=int, help="Generate this many pending cues for a smoke run.")
    args = parser.parse_args()
    import numpy as np
    import soundfile as sf
    import torch
    from qwen_tts import Qwen3TTSModel
    OUTPUT.mkdir(parents=True, exist_ok=True)
    items = cues()
    manifest_path = OUTPUT / "manifest.json"
    previous = json.loads(manifest_path.read_text()) if manifest_path.exists() else {}
    completed = {entry["id"]: entry for entry in previous.get("clips", [])}
    reusable = {entry["textHash"]: entry for entry in previous.get("clips", [])}
    for item in items:
        entry = reusable.get(item["textHash"])
        if entry and (OUTPUT / entry["file"]).is_file() and entry.get("audioSHA256") == file_hash(OUTPUT / entry["file"]):
            target = OUTPUT / item["file"]
            if entry["file"] != item["file"]:
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(OUTPUT / entry["file"], target)
            completed[item["id"]] = dict(entry, **item)
    completed = {item["id"]: completed[item["id"]] for item in items
                 if item["id"] in completed and completed[item["id"]].get("textHash") == item["textHash"]
                 and (OUTPUT / item["file"]).exists()
                 and completed[item["id"]].get("audioSHA256") == file_hash(OUTPUT / item["file"])}
    started = time.time()
    def save(status="generating", error=""):
        atomic_json(manifest_path, dict(model=MODEL, language="English", voice=VOICE,
            pipeline=VERSION, voiceIdentity=reference_identity(),
            referenceSHA256=file_hash(REFERENCE), settings=SETTINGS,
            clips=[completed[i["id"]] for i in items if i["id"] in completed]))
        state = dict(status=status, completed=len(completed), total=len(items),
            assemblyLines=len(json.loads((ROOT / "Assets/Resources/EngineLearning.json").read_text())["steps"]),
            seconds=round(time.time()-started, 1), output=str(OUTPUT),
            voiceIdentity=reference_identity(), error=error)
        atomic_json(STATE, state)
        print(json.dumps(state), flush=True)
    save("loading")
    torch.set_num_threads(6)
    model = Qwen3TTSModel.from_pretrained(str(MODEL_PATH), device_map="cuda:0", dtype=torch.bfloat16,
                                        attn_implementation="sdpa", local_files_only=True)
    prompt = prepare_prompt(model, torch)
    prompt_hash = file_hash(PROMPT)
    completed = {key: entry for key, entry in completed.items()
                 if entry.get("promptSHA256") == prompt_hash}
    # Record tensors too: changing an in-memory reference must not silently drift.
    original_code = prompt[0].ref_code.detach().clone()
    original_embedding = prompt[0].ref_spk_embedding.detach().clone()
    pending = [item for item in items if item["id"] not in completed]
    if args.limit is not None:
        pending = pending[:args.limit]
    for item in pending:
        print("Generating " + item["id"] + ": " + item["text"][:60], flush=True)
        torch.manual_seed(item["seed"])
        torch.cuda.manual_seed_all(item["seed"])
        with torch.inference_mode():
            wavs, rate = model.generate_voice_clone(text=item["text"], language="English",
                voice_clone_prompt=prompt, non_streaming_mode=True, **SETTINGS)
        if not torch.equal(original_code, prompt[0].ref_code) or not torch.equal(original_embedding, prompt[0].ref_spk_embedding):
            raise RuntimeError("The fixed voice prompt changed during generation")
        audio = np.asarray(wavs[0], dtype=np.float32).reshape(-1)
        if not np.isfinite(audio).all() or len(audio) < rate // 2:
            raise RuntimeError("Invalid audio for " + item["id"])
        peak = float(np.max(np.abs(audio)))
        if peak < .001:
            raise RuntimeError("Silent audio for " + item["id"])
        if peak > .98:
            audio *= .98/peak
        active = np.flatnonzero(np.abs(audio) > .004)
        if active.size:
            padding = int(rate*.12)
            audio = audio[max(0, active[0]-padding):min(len(audio), active[-1]+padding+1)]
        path = OUTPUT / item["file"]
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix(".pending")
        sf.write(temporary, audio, rate, format="WAV", subtype="PCM_16")
        temporary.replace(path)
        completed[item["id"]] = dict(item, sampleRate=rate, seconds=round(len(audio)/rate, 3),
                                     audioSHA256=file_hash(path), promptSHA256=prompt_hash)
        save()
        del wavs, audio
    if len(completed)==len(items):
        expected={item['file'] for item in items}
        archive=ROOT/'Backups/Narration'/time.strftime('%Y%m%d-%H%M%S-RetiredCues')
        for old in OUTPUT.rglob('*.wav'):
            relative=old.relative_to(OUTPUT)
            if relative.as_posix() in expected:continue
            destination=archive/relative
            if not old.resolve().is_relative_to(OUTPUT.resolve()) or not destination.resolve().is_relative_to((ROOT/'Backups/Narration').resolve()):raise RuntimeError('Narration archive escaped its folder')
            destination.parent.mkdir(parents=True,exist_ok=True);shutil.move(old,destination)
            meta=old.with_suffix('.wav.meta')
            if meta.exists():shutil.move(meta,destination.with_suffix('.wav.meta'))
    save("complete" if len(completed) == len(items) else "partial")

if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        import traceback
        traceback.print_exc()
        existing = json.loads(STATE.read_text()) if STATE.exists() else {}
        existing.update(status="failed", error=str(error))
        atomic_json(STATE, existing)
        raise

