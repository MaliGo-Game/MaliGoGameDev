import struct, zlib, sys

class Node:
    def __init__(s, name, props, children):
        s.name, s.props, s.children = name, props, children
    def find(s, name):
        return [c for c in s.children if c.name == name]
    def first(s, name):
        r = s.find(name); return r[0] if r else None

def parse(path):
    data = open(path, 'rb').read()
    ver = struct.unpack_from('<I', data, 23)[0]
    wide = ver >= 7500
    pos = 27
    def read_node(pos):
        if wide:
            end, nprops, plen = struct.unpack_from('<QQQ', data, pos); pos += 24
        else:
            end, nprops, plen = struct.unpack_from('<III', data, pos); pos += 12
        nlen = data[pos]; pos += 1
        if end == 0:
            return None, pos
        name = data[pos:pos+nlen].decode('latin1'); pos += nlen
        props = []
        for _ in range(nprops):
            t = chr(data[pos]); pos += 1
            if t == 'Y': props.append(struct.unpack_from('<h', data, pos)[0]); pos += 2
            elif t == 'C': props.append(bool(data[pos])); pos += 1
            elif t == 'I': props.append(struct.unpack_from('<i', data, pos)[0]); pos += 4
            elif t == 'F': props.append(struct.unpack_from('<f', data, pos)[0]); pos += 4
            elif t == 'D': props.append(struct.unpack_from('<d', data, pos)[0]); pos += 8
            elif t == 'L': props.append(struct.unpack_from('<q', data, pos)[0]); pos += 8
            elif t in 'fdlib':
                n, enc, clen = struct.unpack_from('<III', data, pos); pos += 12
                raw = data[pos:pos+clen]; pos += clen
                if enc == 1: raw = zlib.decompress(raw)
                fmt = {'f':'f','d':'d','l':'q','i':'i','b':'?'}[t]
                props.append(list(struct.unpack('<%d%s' % (n, fmt), raw)))
            elif t in 'SR':
                n = struct.unpack_from('<I', data, pos)[0]; pos += 4
                v = data[pos:pos+n]; pos += n
                props.append(v.decode('latin1') if t == 'S' else v)
            else:
                raise ValueError('bad type %r at %d' % (t, pos))
        children = []
        while pos < end:
            c, pos = read_node(pos)
            if c is None: break
            children.append(c)
        return Node(name, props, children), end
    top = []
    while pos < len(data) - 200:
        n, pos = read_node(pos)
        if n is None: break
        top.append(n)
    return Node('root', [], top), ver

def props70(node):
    out = {}
    p = node.first('Properties70')
    if p:
        for c in p.find('P'):
            out[c.props[0]] = c.props[4:]
    return out
