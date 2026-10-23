"""Finish the in-flight batch, resume revised repair cues, then audit all exports."""
import ctypes
import json
import subprocess
import sys
import time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
STATUS=ROOT/'Tools/Narration/beginner-job-status.json'
def status(stage,error=''):
    STATUS.write_text(json.dumps(dict(stage=stage,error=error,updated=time.strftime('%Y-%m-%d %H:%M:%S')),indent=2))

try:
    status('waiting-for-assembly-voice-batch')
    kernel=ctypes.WinDLL('kernel32',use_last_error=True)
    kernel.OpenProcess.restype=ctypes.c_void_p
    kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_uint32]
    kernel.CloseHandle.argtypes=[ctypes.c_void_p]
    handle=kernel.OpenProcess(0x100000,False,int(sys.argv[1]))
    if handle:
        kernel.WaitForSingleObject(handle,0xffffffff)
        kernel.CloseHandle(handle)
    status('resuming-final-catalogue')
    subprocess.run([sys.executable,'Tools/Narration/generate_narration.py'],cwd=ROOT,check=True)
    status('verifying-all-exports')
    subprocess.run([sys.executable,'Tools/Narration/verify_audio.py'],cwd=ROOT,check=True)
    # Cue paths and Unity GUIDs are unchanged, so the existing library reloads
    # the WAVs automatically on asset refresh. Never delete their .meta files.
    status('complete')
except Exception as error:
    status('failed',str(error))
    raise
