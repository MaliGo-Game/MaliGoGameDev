"""Forward kinematics over a Kenney animation FBX: samples bone positions per frame of one take, in the
armature's ("Root") local space, and measures how fast a planted foot moves backward (= the ground speed at
which that clip's feet would not slide)."""
import sys, os, math
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from fbxparse import *

TICKS = 46186158000.0

def rot(ax, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    if ax == 0: return np.array([[1,0,0],[0,c,-s],[0,s,c]])
    if ax == 1: return np.array([[c,0,s],[0,1,0],[-s,0,c]])
    return np.array([[c,-s,0],[s,c,0],[0,0,1]])

def euler_xyz(r):
    return rot(2, r[2]) @ rot(1, r[1]) @ rot(0, r[0])

def mat(t, r, s):
    m = np.eye(4)
    m[:3,:3] = euler_xyz(r) @ np.diag(s)
    m[:3,3] = t
    return m

def load(path, take):
    root, _ = parse(path)
    objs = root.first('Objects')
    byid = {c.props[0]: c for c in objs.children}
    models = {c.props[0]: c for c in objs.children if c.name == 'Model'}
    names = {i: m.props[1].split('\x00')[0] for i, m in models.items()}
    parent = {}
    oo_children = {}
    op = []
    for c in root.first('Connections').find('C'):
        if c.props[0] == 'OO':
            parent.setdefault(c.props[1], c.props[2])
            oo_children.setdefault(c.props[2], []).append(c.props[1])
        elif c.props[0] == 'OP':
            op.append((c.props[1], c.props[2], c.props[3]))
    stack = [c for c in objs.children if c.name == 'AnimationStack' and c.props[1].split('\x00')[0] == take][0]
    layers = [i for i in oo_children.get(stack.props[0], []) if byid[i].name == 'AnimationLayer']
    curvenodes = set(i for l in layers for i in oo_children.get(l, []))
    # curvenode -> (model, prop)
    cn_target = {}
    curves_of = {}
    for child, par, prop in op:
        if child in curvenodes and par in models:
            cn_target[child] = (par, prop)
        if byid.get(child) is not None and byid[child].name == 'AnimationCurve' and par in curvenodes:
            curves_of.setdefault(par, {})[prop] = byid[child]
    anim = {}  # (model, prop) -> {axis: (times, values)}
    for cn, (m, prop) in cn_target.items():
        d = {}
        for ch, cv in curves_of.get(cn, {}).items():
            ax = 'XYZ'.index(ch[-1])
            d[ax] = (np.array(cv.first('KeyTime').props[0]) / TICKS, np.array(cv.first('KeyValueFloat').props[0]))
        anim[(m, prop)] = d
    stop = props70(stack)['LocalStop'][0] / TICKS
    return models, names, parent, anim, stop

def sample(models, names, parent, anim, t):
    local = {}
    for i, m in models.items():
        p = props70(m)
        vals = {}
        for prop, dflt in (('Lcl Translation', [0,0,0]), ('Lcl Rotation', [0,0,0]), ('Lcl Scaling', [1,1,1])):
            v = list(p.get(prop, dflt))
            for ax, (ts, vs) in anim.get((i, prop), {}).items():
                v[ax] = float(np.interp(t, ts, vs))
            vals[prop] = v
        local[i] = mat(vals['Lcl Translation'], vals['Lcl Rotation'], vals['Lcl Scaling'])
    world = {}
    def get(i):
        if i in world: return world[i]
        par = parent.get(i)
        w = local[i] if par not in models else get(par) @ local[i]
        world[i] = w; return w
    rootid = [i for i, n in names.items() if n == 'Root'][0]
    rootinv = np.linalg.inv(get(rootid))
    return {names[i]: (rootinv @ get(i))[:3,3] for i in models}

if __name__ == '__main__':
    path, take = sys.argv[1], sys.argv[2]
    models, names, parent, anim, stop = load(path, take)
    animated = sorted(set(names[m] for (m, p) in anim))
    print('take', take, 'length', stop, 's; animated bones:', animated)
    fps = 24
    n = int(round(stop * fps))
    frames = [sample(models, names, parent, anim, k / fps) for k in range(n + 1)]
    # Root space is Blender's: Z up, character faces -Y (Unity forward after import axis conversion).
    for b in ('Hips', 'Head_end', 'LeftToes_end', 'RightToes_end', 'LeftFoot', 'RightFoot'):
        print(b, 'frame0', np.round(frames[0][b], 3))
    allz = [f[b][2] for f in frames for b in names.values()]
    print('min z any bone', min(allz))
    print('frame  L.toe(y,z)        R.toe(y,z)       L.foot(y,z)     R.foot(y,z)  hipsZ')
    for k, f in enumerate(frames):
        print(k, np.round([f['LeftToes'][1], f['LeftToes'][2], f['RightToes'][1], f['RightToes'][2],
                           f['LeftFoot'][1], f['LeftFoot'][2], f['RightFoot'][1], f['RightFoot'][2], f['Hips'][2]], 3))
