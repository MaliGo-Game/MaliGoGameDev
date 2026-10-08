"""Offline model of a Unity 1D blend tree (Idle at 0, Run at 1) on Kenney's rig: per bone, translations lerped and
rotations nlerped (what Mecanim does for generic clips) at a shared normalized time. For each run weight w it
reports how far a planted foot travels backward per cycle (model units), how level it stays during contact,
and the blend's cycle length (the weighted average of the clip lengths).

Source of MovementMath.StridePerCycle. Needs Python 3 + numpy; no Unity:
    python tools/fbx_stride/blend.py Assets/kenney_animated-characters-protagonists/Animations/idle.fbx \
        Assets/kenney_animated-characters-protagonists/Animations/run.fbx
(stride.py alone prints the run clip's per-frame foot positions: python tools/fbx_stride/stride.py <run.fbx> "Root|Run")"""
import sys, os, math
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fbxparse import *
import stride

def quat_from_mat(m):
    t = np.trace(m)
    if t > 0:
        s = math.sqrt(t + 1.0) * 2; w = 0.25 * s
        x = (m[2,1] - m[1,2]) / s; y = (m[0,2] - m[2,0]) / s; z = (m[1,0] - m[0,1]) / s
    elif m[0,0] > m[1,1] and m[0,0] > m[2,2]:
        s = math.sqrt(1.0 + m[0,0] - m[1,1] - m[2,2]) * 2; w = (m[2,1] - m[1,2]) / s
        x = 0.25 * s; y = (m[0,1] + m[1,0]) / s; z = (m[0,2] + m[2,0]) / s
    elif m[1,1] > m[2,2]:
        s = math.sqrt(1.0 + m[1,1] - m[0,0] - m[2,2]) * 2; w = (m[0,2] - m[2,0]) / s
        x = (m[0,1] + m[1,0]) / s; y = 0.25 * s; z = (m[1,2] + m[2,1]) / s
    else:
        s = math.sqrt(1.0 + m[2,2] - m[0,0] - m[1,1]) * 2; w = (m[1,0] - m[0,1]) / s
        x = (m[0,2] + m[2,0]) / s; y = (m[1,2] + m[2,1]) / s; z = 0.25 * s
    return np.array([w, x, y, z])

def mat_from_quat(q):
    w, x, y, z = q / np.linalg.norm(q)
    return np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                     [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                     [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])

def locals_at(clip, t):
    models, names, parent, anim, stop = clip
    out = {}
    for i, m in models.items():
        p = props70(m)
        vals = {}
        for prop, dflt in (('Lcl Translation', [0,0,0]), ('Lcl Rotation', [0,0,0]), ('Lcl Scaling', [1,1,1])):
            v = list(p.get(prop, dflt))
            for ax, (ts, vs) in anim.get((i, prop), {}).items():
                v[ax] = float(np.interp(t, ts, vs))
            vals[prop] = v
        out[names[i]] = (np.array(vals['Lcl Translation']), quat_from_mat(stride.euler_xyz(vals['Lcl Rotation'])), np.array(vals['Lcl Scaling']))
    return out

def blended_pose(idle, run, w, phase):
    a = locals_at(idle, phase * idle[4])
    b = locals_at(run, phase * run[4])
    models, names, parent, _, _ = run
    byname = {n: i for i, n in names.items()}
    local = {}
    for n, (ta, qa, sa) in a.items():
        tb, qb, sb = b[n]
        if np.dot(qa, qb) < 0: qb = -qb
        q = (1 - w) * qa + w * qb
        m = np.eye(4); m[:3,:3] = mat_from_quat(q) @ np.diag((1 - w) * sa + w * sb); m[:3,3] = (1 - w) * ta + w * tb
        local[n] = m
    world = {}
    pname = {names[i]: names.get(parent.get(i)) for i in models}
    def get(n):
        if n in world: return world[n]
        p = pname[n]
        wm = local[n] if p is None or p not in local else get(p) @ local[n]
        world[n] = wm; return wm
    rinv = np.linalg.inv(get('Root'))
    return {n: (rinv @ get(n))[:3,3] for n in local}

if __name__ == '__main__':
    idle = stride.load(sys.argv[1], 'Root|Idle')
    run = stride.load(sys.argv[2], 'Root|Run')
    N = 64
    for w in (0.0, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0):
        poses = [blended_pose(idle, run, w, k / N) for k in range(N + 1)]
        L = (1 - w) * idle[4] + w * run[4]
        # Planted foot = the lower toe joint; contact = within 0.02 of the lowest toe height seen.
        zl = np.array([p['LeftToes'][2] for p in poses]); zr = np.array([p['RightToes'][2] for p in poses])
        yl = np.array([p['LeftToes'][1] for p in poses]); yr = np.array([p['RightToes'][1] for p in poses])
        floor = min(zl.min(), zr.min())
        vels, zs = [], []
        for k in range(N):
            for z, y in ((zl, yl), (zr, yr)):
                if z[k] < floor + 0.02 and z[k+1] < floor + 0.02:
                    vels.append((y[k+1] - y[k]) * N)  # model units per cycle (+Y is backward)
                    zs.append(z[k])
        vels = np.array(vels)
        hips = np.array([p['Hips'][2] for p in poses])
        lift = max(zl.max(), zr.max()) - floor
        if len(vels):
            print('w=%.1f cycle %.3fs  planted-foot travel %.3f u/cycle (sd %.3f, %d samples, %.0f%% contact)  toe floor %.4f  toe lift %.3f  hips bob %.3f'
                  % (w, L, vels.mean(), vels.std(), len(vels), 100 * len(vels) / (2 * N), floor, lift, hips.max() - hips.min()))
        else:
            print('w=%.1f cycle %.3fs no contact samples; floor %.4f' % (w, L, floor))
