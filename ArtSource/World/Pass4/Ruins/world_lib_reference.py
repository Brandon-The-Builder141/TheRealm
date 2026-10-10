
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Euler
TEXDIR = r"C:\Users\Brandon\OneDrive\Desktop\The Realm\The Realm\Assets\Realm\Art\World\Textures"
TILE = {"W_Plaster": 2.0, "W_Oak": 1.2, "W_Slate": 1.6, "W_Rubble": 1.8, "W_Planks": 1.4, "W_Cobblestone": 4.0,
        "W_Earth": 4.0, "W_Grass": 4.0, "W_Iron": 0.6, "W_Glow": 1.0, "W_Thatch": 2.0}

def material(name):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled'); nt.links.new(bsdf.outputs[0], out.inputs[0])
    if name == "W_Glow":
        bsdf.inputs['Base Color'].default_value = (1.0, 0.45, 0.12, 1); bsdf.inputs['Emission Color'].default_value = (1.0, 0.42, 0.10, 1)
        bsdf.inputs['Emission Strength'].default_value = 6; return m
    def img(suffix, noncolor):
        p = os.path.join(TEXDIR, f"{name}_{suffix}.png")
        if not os.path.exists(p): return None
        n = nt.nodes.new('ShaderNodeTexImage'); n.image = bpy.data.images.load(p, check_existing=True)
        if noncolor: n.image.colorspace_settings.name = 'Non-Color'
        return n
    b = img("BaseColor", False)
    if b:
        nt.links.new(b.outputs[0], bsdf.inputs['Base Color'])
        if name.startswith("T_"):
            m.blend_method = 'CLIP'; m.alpha_threshold = 0.4
            m.use_backface_culling = False
            if 'Alpha' in bsdf.inputs: nt.links.new(b.outputs['Alpha'], bsdf.inputs['Alpha'])
    nrm = img("Normal", True)
    if nrm:
        nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(nrm.outputs[0], nm.inputs['Color']); nt.links.new(nm.outputs[0], bsdf.inputs['Normal'])
    mask = img("Mask", True)
    if mask:
        sep = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(mask.outputs[0], sep.inputs[0]); nt.links.new(sep.outputs['Red'], bsdf.inputs['Metallic'])
        inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1
        nt.links.new(mask.outputs['Alpha'], inv.inputs[1]); nt.links.new(inv.outputs[0], bsdf.inputs['Roughness'])
    return m

class Kit:
    """Accumulates parts into one bmesh with material slots; UVs are world-scale box projections per part."""
    def __init__(self, name):
        self.name = name; self.bm = bmesh.new(); self.uv = self.bm.loops.layers.uv.new("UVMap"); self.mats = []
    def slot(self, mat):
        if mat not in self.mats: self.mats.append(mat)
        return self.mats.index(mat)
    def add_geom(self, verts_faces, mat, grain=None, uv_offset=None):
        """verts_faces: (list of Vector, list of index tuples). grain: local direction the texture V should follow."""
        vs = [self.bm.verts.new(v) for v in verts_faces[0]]
        idx = self.slot(mat); tile = TILE.get(mat, 2.0)
        off = uv_offset if uv_offset is not None else Vector((random.random()*7, random.random()*7))
        g = Vector(grain).normalized() if grain is not None else None
        made = []
        for f in verts_faces[1]:
            try: face = self.bm.faces.new([vs[i] for i in f])
            except ValueError: continue
            face.material_index = idx; face.normal_update(); made.append(face)
            n = face.normal
            if g is not None and abs(n.dot(g)) < 0.7:
                vdir = (g - n*n.dot(g)).normalized(); udir = n.cross(vdir).normalized()
            else:
                ax = max(range(3), key=lambda i: abs(n[i]))
                udir, vdir = [(Vector((0,1,0)), Vector((0,0,1))), (Vector((1,0,0)), Vector((0,0,1))), (Vector((1,0,0)), Vector((0,1,0)))][ax]
            for l in face.loops:
                p = l.vert.co
                l[self.uv].uv = (p.dot(udir)/tile + off.x, p.dot(vdir)/tile + off.y)
        return made
    def box(self, center, size, mat, rot=(0,0,0), grain=None, taper=0.0, bevel=0.0):
        c = Vector(center); sx, sy, sz = (s/2 for s in size)
        R = Euler(rot).to_matrix()
        pts = []
        for z in (-sz, sz):
            k = 1 - taper if z > 0 else 1
            for x, y in ((-sx, -sy), (sx, -sy), (sx, sy), (-sx, sy)): pts.append(c + R @ Vector((x*k, y*k, z)))
        faces = [(0,3,2,1), (4,5,6,7), (0,1,5,4), (1,2,6,5), (2,3,7,6), (3,0,4,7)]
        gw = R @ Vector(grain) if grain is not None else None
        return self.add_geom((pts, faces), mat, grain=gw)
    def beam(self, a, b, thick, mat="W_Oak", depth=None, jitter=0.01):
        """Timber between two points, grain along its length."""
        a = Vector(a); b = Vector(b); d = b - a; L = d.length
        ax = d.normalized()
        up = Vector((0,0,1)) if abs(ax.z) < 0.9 else Vector((1,0,0))
        side = ax.cross(up).normalized(); up2 = side.cross(ax).normalized()
        depth = depth or thick
        j = lambda: (random.random()-0.5)*2*jitter
        pts = []
        for t, s_ in ((0, a), (1, b)):
            for u, v in ((-1,-1), (1,-1), (1,1), (-1,1)):
                pts.append(s_ + side*(u*thick/2 + j()) + up2*(v*depth/2 + j()))
        faces = [(0,3,2,1), (4,5,6,7), (0,1,5,4), (1,2,6,5), (2,3,7,6), (3,0,4,7)]
        return self.add_geom((pts, faces), mat, grain=ax)
    def prism(self, verts2d, y0, y1, mat, axis='Y', grain=None):
        """Extrude a 2D polygon (x,z) between y0..y1 (gable walls, roof slabs)."""
        n = len(verts2d); pts = []
        for y in (y0, y1):
            for x, z in verts2d: pts.append(Vector((x, y, z)))
        faces = [tuple(range(n))[::-1], tuple(range(n, 2*n))]
        for i in range(n):
            j = (i+1) % n; faces.append((i, j, n+j, n+i))
        return self.add_geom((pts, faces), mat, grain=grain)
    def cylinder(self, center, radius, height, mat, seg=12, axis=(0,0,1), grain=None, r2=None):
        c = Vector(center); ax = Vector(axis).normalized()
        u = ax.cross(Vector((0,0,1)) if abs(ax.z) < 0.9 else Vector((1,0,0))).normalized(); v = ax.cross(u)
        r2 = radius if r2 is None else r2; pts = []
        for k, rr in ((0, radius), (1, r2)):
            for i in range(seg):
                a = 2*math.pi*i/seg
                pts.append(c + ax*(height*k) + (u*math.cos(a) + v*math.sin(a))*rr)
        faces = [tuple(range(seg))[::-1], tuple(range(seg, 2*seg))] + [(i, (i+1)%seg, seg+(i+1)%seg, seg+i) for i in range(seg)]
        return self.add_geom((pts, faces), mat, grain=grain if grain is not None else ax)
    def quad(self, p0, p1, p2, p3, mat, uv=((0,0),(1,0),(1,1),(0,1)), two_sided=False):
        """Explicit-UV quad (foliage cards, signage) — bypasses the world-scale box projection."""
        idx = self.slot(mat)
        vs = [self.bm.verts.new(p) for p in (p0, p1, p2, p3)]
        f = self.bm.faces.new(vs); f.material_index = idx
        for l, (u, v) in zip(f.loops, uv): l[self.uv].uv = (u, v)
        if two_sided:
            vb = [self.bm.verts.new(p) for p in (p0, p1, p2, p3)]
            fb = self.bm.faces.new(list(reversed(vb))); fb.material_index = idx
            for l, (u, v) in zip(fb.loops, list(reversed(uv))): l[self.uv].uv = (u, v)
        return f
    def finish(self, collection, smooth_angle=35):
        me = bpy.data.meshes.new(self.name)
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=0.0005)
        self.bm.normal_update(); self.bm.to_mesh(me); self.bm.free()
        for m in self.mats: me.materials.append(material(m))
        o = bpy.data.objects.new(self.name, me); collection.objects.link(o)
        for p in me.polygons: p.use_smooth = False
        return o

def collection(name, clear=True):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(c)
    if clear:
        for o in list(c.objects):
            d = o.data; bpy.data.objects.remove(o, do_unlink=True)
            if d and d.users == 0: bpy.data.meshes.remove(d)
    return c
