# Consistent narration

The 183 assembly instruction steps use shared voice IDs for matching piston
and valve components. Mixed component types have separate steps. Together with
repair, completion, flip and inventory instructions, the catalogue contains
135 unique audio cues. A shared cue plays once per chapter visit, followed by
one short reminder to repeat the process; later matching assemblies are silent.
Captions remain available and do not include valve station codes such as C1 EX1.

All cues use one synthetic narrator
from `C:/Users/Vedant/AI_Models/qwen3tts/test_voice.wav`. Its exact transcript is
in `generate_narration.py`. The original voice files, manifest, generator and
verification report are backed up in
`Backups/Narration/20261008-172128-VoiceDesign`.

The Qwen 1.7B Base model extracts `ref_code` and `ref_spk_embedding` once into
`Tools/Narration/Voice/voice_clone_prompt.pt`. Every line explicitly receives the
same prompt via `generate_voice_clone(voice_clone_prompt=prompt)`. The prompt
also carries the reference transcript and enables acoustic/text conditioning.
The generator checks that both tensors remain unchanged. It resumes from the
saved prompt and verifies exported audio hashes before reusing a finished line.
Seeds are `1234 + cue_index`; temperature is 0.7 and repetition penalty is 1.1.

This follows the [official Qwen design-then-clone API](https://github.com/QwenLM/Qwen3-TTS#voice-design-then-clone).
`get_speaker_embedding` and a `speaker_embedding` generation keyword are not
public APIs in the installed package. Decoder hidden states are not carried
between independent sentences; the fixed acoustic prompt locks their speaker
conditioning without accumulating changes from earlier generated lines.

Run commands from the Unity project using the existing virtual environment:

```powershell
$ttsPython = 'C:/Users/Vedant/AI_Models/qwen3tts/venv/Scripts/python.exe'
& $ttsPython Tools/Narration/download_clone_model.py
& $ttsPython Tools/Narration/generate_narration.py --limit 3
& $ttsPython Tools/Narration/generate_narration.py
& $ttsPython Tools/Narration/verify_audio.py
```

The background job `run_consistent_generation.py` waits for the checkpoint,
generates three initial lines, checks their waveforms, resumes the full catalogue,
restores any missing original Unity metadata, and validates the completed export.
Do not start a second generator while this job is running.

Outputs stay in `Assets/Audio/Narration/English/Assembly` and `Repair` with their
existing filenames and GUIDs. The saved reference/prompt and model checkpoint
are outside Unity Assets. Progress is in `progress.json`; pipeline stage/errors
are in `job-status.json`. Logs are `consistent-generation.log` and
`consistent-generation-errors.log`; final checks are in `audio-verification.json`.
On finishing, Unity imports the replacement WAVs at their existing paths. The
existing narration library retains its references. After changing cue IDs or
the catalogue, use **Tools → Engine Assembly → Audio → Connect Exported Narration**.
