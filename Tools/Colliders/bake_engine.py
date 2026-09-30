"""Resumable CoACD bake. Each unique mesh is computed once, in a bounded worker pool."""
import concurrent.futures
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parent
CACHE = Path(sys.argv[1]).resolve()

def bake(key):
    target = CACHE / (key + '.output.json')
    if target.exists():
        return key, 'cached'
    env = dict(os.environ, OMP_NUM_THREADS='2', OPENBLAS_NUM_THREADS='1')
    try:
        command = [sys.executable, str(ROOT / 'decompose.py'), '--input',
                   str(CACHE / (key + '.input.json')), '--output', str(target)]
        if '--bounded' in sys.argv:
            command = [sys.executable, str(ROOT / 'retry_engine.py'), str(CACHE), key]
        job = subprocess.run(command,
                             env=env, capture_output=True, text=True, timeout=900)
        if job.returncode:
            raise RuntimeError(job.stderr[-1500:])
        return key, job.stdout.strip()
    except Exception as exc:
        return key, 'FAILED: ' + str(exc)

if __name__ == '__main__':
    parts = json.loads((CACHE / 'manifest.json').read_text())['parts']
    keys = list(dict.fromkeys(p['key'] for p in parts))
    def option(name, default):
        return int(sys.argv[sys.argv.index(name)+1]) if name in sys.argv else default
    keys = keys[option('--start', 0):option('--end', len(keys))]
    if '--reverse' in sys.argv:
        keys.reverse()
    failures = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=option('--workers', 3)) as pool:
        jobs = {pool.submit(bake, key): key for key in keys}
        for i, job in enumerate(concurrent.futures.as_completed(jobs), 1):
            key, result = job.result()
            print(f'{i}/{len(keys)} {key[:12]} {result}', flush=True)
            if result.startswith('FAILED'):
                failures.append({'key': key, 'error': result})
    (CACHE / 'failures.json').write_text(json.dumps(failures, indent=2))
    sys.exit(bool(failures))
