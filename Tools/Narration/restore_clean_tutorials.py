"""Retire processed tutorial takes and regenerate them with the locked voice."""
import json,shutil,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];OUTPUT=ROOT/'Assets/Audio/Narration/English'
manifest=OUTPUT/'manifest.json';data=json.loads(manifest.read_text())
processed=[c for c in data['clips'] if c.get('practicePlaybackRate')]
archive=ROOT/'Backups/Narration'/time.strftime('%Y%m%d-%H%M%S-ProcessedTutorials')
if processed:
    archive.mkdir(parents=True,exist_ok=True);shutil.copy2(manifest,archive/'manifest.json')
    for clip in processed:
        destination=archive/clip['file'];destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(OUTPUT/clip['file'],destination)
    identities={c['id'] for c in processed};data['clips']=[c for c in data['clips'] if c['id'] not in identities]
    manifest.write_text(json.dumps(data,indent=2),encoding='utf-8')
print(json.dumps(dict(regenerate=len(processed),archive=str(archive))))
