"""Bound the search for CAD housings that exceed the normal bake timeout."""
import json
from pathlib import Path
import subprocess
import sys
cache=Path(sys.argv[1]).resolve()
key=sys.argv[2]
request=json.loads((cache/(key+'.input.json')).read_text())
request.update(threshold=.1, searchIterations=20, searchNodes=10, searchDepth=2)
source=cache/(key+'.retry.input.json')
source.write_text(json.dumps(request))
subprocess.run([sys.executable,str(Path(__file__).with_name('decompose.py')),'--input',str(source),
                '--output',str(cache/(key+'.output.json'))],check=True,timeout=900)
