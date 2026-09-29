import json
import time
from pathlib import Path
from decompose import run

polygon = [(0, 0), (2, 0), (2, 1), (1, 1), (1, 2), (0, 2)]
vertices = [dict(x=x, y=y, z=z) for z in (0, 1) for x, y in polygon]
triangles = []
for a, b, c in [(0, 1, 3), (1, 2, 3), (0, 3, 5), (3, 4, 5)]:
    triangles.extend((c, b, a, a + 6, b + 6, c + 6))
for i in range(6):
    j = (i + 1) % 6
    triangles.extend((i, j, j + 6, i, j + 6, i + 6))
report = {}
for backend in ('CoACD', 'VHACD'):
    start = time.monotonic()
    result = run(dict(backend=backend, vertices=vertices, triangles=triangles, maxHulls=8, threshold=0.05))
    assert 1 < len(result['hulls']) <= 8, 'Concave L should decompose into multiple hulls.'
    assert all(len(h['triangles']) // 3 <= 255 for h in result['hulls'])
    report[backend] = dict(passed=True, hulls=len(result['hulls']), seconds=round(time.monotonic() - start, 2))
path = Path(__file__).parents[1] / 'Verification' / 'collider-report.json'
path.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
