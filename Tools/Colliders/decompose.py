"""Project-local CoACD / V-HACD bridge. Unity supplies mesh-local triangles as JSON."""
import argparse
import hashlib
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / 'python_deps'))
os.environ.setdefault('OMP_NUM_THREADS', '4')


def run(request, raw_cache=None):
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
    hull_count = max(1, min(512, int(request.get('maxHulls', 16))))
    fingerprint = hashlib.sha256(json.dumps(request, sort_keys=True).encode()).hexdigest()
    cache = json.loads(Path(raw_cache).read_text()) if raw_cache and Path(raw_cache).exists() else None
    cached = cache.get('hulls') if isinstance(cache, dict) and cache.get('inputHash') == fingerprint else None
    if cached is not None:
        hulls = [(h['vertices'], h['triangles']) for h in cached]
        print('Reusing completed native decomposition cache.', flush=True)
    elif request['backend'] == 'CoACD':
        import coacd
        coacd.set_log_level('info')
        # Unity serializes 0.01f as 0.009999999776. Native CoACD rejects values below 0.01.
        threshold = max(0.01, min(1.0, float(request.get('threshold', 0.05))))
        print('CoACD: decomposing %d triangles, threshold %.5f, merge=%s' % (len(faces), threshold, request.get('merge', True)), flush=True)
        hulls = coacd.run_coacd(coacd.Mesh(vertices, faces), threshold=threshold,
                               max_convex_hull=hull_count, decimate=True, max_ch_vertex=min(128, int(request.get('maxVertices', 64))), seed=0,
                               merge=bool(request.get('merge', True)),
                               preprocess_resolution=int(request.get('preprocessResolution', 50)),
                               resolution=int(request.get('resolution', 2000)),
                               mcts_iterations=int(request.get('searchIterations', 60)),
                               mcts_nodes=int(request.get('searchNodes', 20)),
                               mcts_max_depth=int(request.get('searchDepth', 3)))
    elif request['backend'] == 'VHACD':
        import vhacdx
        hulls = vhacdx.compute_vhacd(vertices, faces.astype(np.uint32).reshape(-1), maxConvexHulls=hull_count,
                                    maxNumVerticesPerCH=64, resolution=100000, asyncACD=False)
    else:
        raise ValueError('Unknown backend: ' + request['backend'])
    if raw_cache and cached is None:
        temporary_cache = raw_cache+'.tmp'
        Path(temporary_cache).write_text(json.dumps({'inputHash':fingerprint,'hulls':[{'vertices':np.asarray(p).tolist(),'triangles':np.asarray(t).tolist()} for p,t in hulls]}), encoding='utf-8')
        os.replace(temporary_cache,raw_cache)
    result = []
    skipped = 0
    for points, triangles in hulls:
        points, triangles = np.asarray(points), np.asarray(triangles).reshape(-1)
        if len(points) < 4 or len(triangles) < 12 or np.linalg.matrix_rank(points-points.mean(axis=0),tol=np.ptp(vertices,axis=0).max()*1e-9)<3:
            skipped += 1
            continue  # Zero-volume CAD/remeshing fragments cannot be valid solid colliders.
        if len(triangles) % 3 or len(triangles) // 3 > 255:
            raise ValueError('Backend hull has %d vertices and %d faces; Unity permits 255 convex faces.' % (len(points),len(triangles)//3))
        if not np.isfinite(points).all() or triangles.min() < 0 or triangles.max() >= len(points):
            raise ValueError('Backend returned invalid hull coordinates or indices.')
        result.append({'vertices': [dict(zip(('x', 'y', 'z'), map(float, p))) for p in points],
                       'triangles': triangles.astype(int).tolist()})
    if not result:
        raise ValueError('Backend produced no hulls.')
    return {'hulls': result, 'discardedZeroVolumeFragments':skipped, 'settings': {k: request[k] for k in ('backend', 'threshold', 'maxHulls', 'searchIterations', 'searchNodes', 'searchDepth', 'merge', 'maxVertices', 'preprocessResolution', 'resolution') if k in request}}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try:
        with open(args.input, encoding='utf-8-sig') as stream:
            result = run(json.load(stream), args.output+'.raw.json')
        temporary = args.output + '.' + str(os.getpid()) + '.tmp'
        with open(temporary, 'w', encoding='utf-8') as stream:
            json.dump(result, stream, allow_nan=False)
        os.replace(temporary, args.output)
        print('Generated', len(result['hulls']), 'convex hulls.')
    except Exception as error:
        import traceback
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)
