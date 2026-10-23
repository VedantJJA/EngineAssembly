"""Align visible caption words to the exported speech; run alongside generation."""
import difflib,json,re,time,os
from pathlib import Path
from faster_whisper import WhisperModel
import soundfile as sf
from scipy.signal import resample_poly
from math import gcd
ROOT=Path(__file__).resolve().parents[2]
TEXT=ROOT/'Assets/Resources/NarrationText.json'
MANIFEST=ROOT/'Assets/Audio/Narration/English/manifest.json'
def clean(s): return re.sub(r'[^a-z0-9]', '',s.lower())
model=WhisperModel('tiny.en',device='cpu',compute_type='int8',cpu_threads=4,download_root=str(ROOT/'Tools/Narration/Models/Alignment'))
while True:
    catalogue=json.loads(TEXT.read_text());entries={c['id']:c for c in catalogue['cues']}
    manifest=json.loads(MANIFEST.read_text())
    for clip in manifest['clips']:
        cue=entries.get(clip['id'])
        if not cue or cue.get('audioSHA256')==clip['audioSHA256'] or cue['text']!=clip['text']: continue
        audio,rate=sf.read(ROOT/'Assets/Audio/Narration/English'/clip['file'],dtype='float32')
        divisor=gcd(rate,16000);audio=resample_poly(audio,16000//divisor,rate//divisor)
        segments,_=model.transcribe(audio,language='en',word_timestamps=True,beam_size=3,condition_on_previous_text=False)
        heard=[w for segment in segments for w in (segment.words or [])]
        tokens=cue['text'].split();matches={}
        for a,b,count in difflib.SequenceMatcher(None,[clean(t) for t in tokens],[clean(w.word) for w in heard],autojunk=False).get_matching_blocks():
            for n in range(count): matches[a+n]=(heard[b+n].start,heard[b+n].end)
        if len(matches)<len(tokens)*.55:
            print('Alignment needs review:',clip['id'],flush=True);continue
        words=[]
        for i,token in enumerate(tokens):
            if i in matches: start,end=matches[i]
            else:
                left=max((n for n in matches if n<i),default=-1);right=min((n for n in matches if n>i),default=len(tokens))
                begin=matches[left][1] if left>=0 else 0;finish=matches[right][0] if right<len(tokens) else clip['seconds'];step=max(0,finish-begin)/(right-left)
                start=begin+step*(i-left-1);end=start+step
            words.append(dict(word=token,start=round(start,3),end=round(end,3)))
        cue.update(words=words,timingSource='speech-recognition-alignment',audioSHA256=clip['audioSHA256'])
        # Preserve words that another pass already completed.
        latest=json.loads(TEXT.read_text());latest['cues']=[cue if c['id']==cue['id'] else c for c in latest['cues']]
        tmp=TEXT.with_suffix('.aligning');tmp.write_text(json.dumps(latest,indent=2));os.replace(tmp,TEXT)
        print('Aligned',clip['id'],len(words),'words',flush=True)
    state=json.loads((ROOT/'Tools/Narration/progress.json').read_text())
    if state['status']=='complete':break
    time.sleep(15)
