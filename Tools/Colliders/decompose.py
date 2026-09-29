"""Project-local CoACD / V-HACD bridge. Unity supplies mesh-local triangles as JSON."""
import argparse
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / 'python_deps'))


def run(request):
    import numpy as np
    vertices = np.asarray([[v['x'], v['y'], v['z']] for v in request['vertices']], dtype=np.float64)
    faces = np.asarray(request['triangles'], dtype=np.int32).reshape((-1, 3))
    if len(vertices) < 4 or len(faces) < 4 or not np.isfinite(vertices).all():
        raise ValueError('Mesh is empty, planar, or contains invalid coordinates.')
    if faces.min() < 0 or faces.max() >= len(vertices):
        raise ValueError('Triangle index is outside vertex array.')
    # Weld CAD seams without changing positions; duplicated vertices are common in imported CAD.
    vertices, inverse = np.unique(vertices, axis=0, return_inverse=True)
    faces = inverse[faces]
    faces = faces[(faces[:, 0] != faces[:, 1]) & (faces[:, 1] != faces[:, 2]) & (faces[:, 0] != faces[:, 2])]
    hull_count = max(1, min(64, int(request.get('maxHulls', 16))))
    if request['backend'] == 'CoACD':
        import coacd
        coacd.set_log_level('warn')
        hulls = coacd.run_coacd(coacd.Mesh(vertices, faces), threshold=float(request.get('threshold', 0.05)),
                               max_convex_hull=hull_count, decimate=True, max_ch_vertex=64, seed=0)
    elif request['backend'] == 'VHACD':
        import vhacdx
        hulls = vhacdx.compute_vhacd(vertices, faces.astype(np.uint32).reshape(-1), maxConvexHulls=hull_count,
                                    maxNumVerticesPerCH=64, resolution=100000, asyncACD=False)
    else:
        raise ValueError('Unknown backend: ' + request['backend'])
    result = []
    for points, triangles in hulls:
        points, triangles = np.asarray(points), np.asarray(triangles).reshape(-1)
        if len(points) < 4 or len(triangles) < 12 or len(triangles) % 3 or len(triangles) // 3 > 255:
            raise ValueError('Backend returned an invalid hull or exceeded the convex collider face limit.')
        if not np.isfinite(points).all() or triangles.min() < 0 or triangles.max() >= len(points):
            raise ValueError('Backend returned invalid hull coordinates or indices.')
        result.append({'vertices': [dict(zip(('x', 'y', 'z'), map(float, p))) for p in points],
                       'triangles': triangles.astype(int).tolist()})
    if not result:
        raise ValueError('Backend produced no hulls.')
    return {'hulls': result}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try:
        with open(args.input, encoding='utf-8-sig') as stream:
            result = run(json.load(stream))
        temporary = args.output + '.tmp'
        with open(temporary, 'w', encoding='utf-8') as stream:
            json.dump(result, stream, allow_nan=False)
        os.replace(temporary, args.output)
        print('Generated', len(result['hulls']), 'convex hulls.')
    except Exception as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
