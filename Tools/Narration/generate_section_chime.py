"""Create a short original celebration sting without external audio licensing."""
from pathlib import Path
import numpy as np
import soundfile as sf
root=Path(__file__).resolve().parents[2]
rate=24000
audio=np.zeros(int(rate*1.15),dtype=np.float64)
for start,freq in [(0,659.25),(.12,830.61),(.24,987.77),(.4,1318.51)]:
    t=np.arange(int(rate*.65))/rate
    bell=(np.sin(2*np.pi*freq*t)+.25*np.sin(2*np.pi*freq*2*t))*np.exp(-t*8)*np.minimum(1,t/.006)*.17
    first=int(start*rate);audio[first:first+len(bell)]+=bell
rng=np.random.default_rng(2026)
for start in [.03,.18,.32]:
    t=np.arange(int(rate*.035))/rate
    pop=rng.normal(0,1,len(t))*np.exp(-t*180)*np.minimum(1,t/.002)*.025
    first=int(start*rate);audio[first:first+len(pop)]+=pop
audio=np.tanh(audio)
path=root/'Assets/Resources/WorkshopSectionComplete.wav'
sf.write(path,audio,rate,subtype='PCM_16')
print(path)
