# Add-on MathNinja MCP: ponte HTTP local (127.0.0.1) para o servidor MCP do Claude Code.
# Os comandos chegam numa thread HTTP, entram numa fila e rodam na thread principal
# (bpy.app.timers), porque o bpy nao e thread-safe.

bl_info = {
    "name": "MathNinja MCP",
    "author": "MathNinja",
    "version": (1, 0, 0),
    "blender": (4, 2, 0),
    "location": "3D View > Barra N > MathNinja",
    "description": "Ponte local para o Claude Code controlar o Blender via MCP",
    "category": "Interface",
}

import atexit
import contextlib
import io
import json
import math
import os
import queue
import secrets
import tempfile
import threading
import time
import traceback
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import bpy
import bmesh
import mathutils
from bpy.app.handlers import persistent

PORT = 8766
HOST = "127.0.0.1"
CMD_TIMEOUT = 60.0
BRIDGE_DIR = os.path.join(os.path.expanduser("~"), ".mathninja")
BRIDGE_FILE = os.path.join(BRIDGE_DIR, "blender-bridge.json")
DEFAULT_EXPORT_DIR = r"C:\Users\cliente\MathNinja\Assets\Models"

_state = {"server": None, "thread": None, "token": None, "error": None}
_queue = queue.Queue()


# ---------------------------------------------------------------- utilidades

def _vec(v, nd=4):
    return [round(float(x), nd) for x in v]


def _deg(euler):
    return [round(math.degrees(a), 3) for a in euler]


def _rad(vals):
    return [math.radians(float(a)) for a in vals]


def _obj(name):
    if not name:
        raise ValueError("informe 'name' do objeto")
    o = bpy.data.objects.get(name)
    if o is None:
        raise ValueError(f"objeto '{name}' nao encontrado")
    return o


def _obj_summary(o):
    d = {
        "name": o.name,
        "type": o.type,
        "location": _vec(o.location),
        "rotation_deg": _deg(o.rotation_euler),
        "scale": _vec(o.scale),
        "dimensions": _vec(o.dimensions),
        "parent": o.parent.name if o.parent else None,
        "visible": o.visible_get() if hasattr(o, "visible_get") else True,
        "materials": [s.material.name for s in getattr(o, "material_slots", []) if s.material],
    }
    if o.type == "MESH" and o.data:
        d["vertices"] = len(o.data.vertices)
        d["faces"] = len(o.data.polygons)
    return d


def _ctx_override(obj=None):
    """Monta um override de contexto (janela/area VIEW_3D quando houver UI)."""
    kw = {}
    wm = bpy.context.window_manager
    if wm and wm.windows:
        win = wm.windows[0]
        kw["window"] = win
        for area in win.screen.areas:
            if area.type == "VIEW_3D":
                kw["area"] = area
                for region in area.regions:
                    if region.type == "WINDOW":
                        kw["region"] = region
                break
    if obj is not None:
        kw["object"] = obj
        kw["active_object"] = obj
        kw["selected_objects"] = [obj]
        kw["selected_editable_objects"] = [obj]
    return bpy.context.temp_override(**kw)


def _make_active(o):
    vl = bpy.context.view_layer
    if o.name not in vl.objects:
        raise ValueError(f"objeto '{o.name}' nao esta na view layer ativa")
    for other in vl.objects:
        if other.select_get():
            other.select_set(False)
    o.select_set(True)
    vl.objects.active = o


def _ensure_object_mode():
    o = bpy.context.view_layer.objects.active
    if o is not None and o.mode != "OBJECT":
        with _ctx_override(o):
            bpy.ops.object.mode_set(mode="OBJECT")


def _undo_push(msg):
    try:
        with _ctx_override():
            bpy.ops.ed.undo_push(message="MCP: " + msg)
    except Exception:
        pass


def _jsonable(v):
    try:
        json.dumps(v)
        return v
    except Exception:
        pass
    if isinstance(v, (mathutils.Vector, mathutils.Euler, mathutils.Quaternion, mathutils.Color)):
        return list(v)
    if isinstance(v, mathutils.Matrix):
        return [list(r) for r in v]
    if isinstance(v, (list, tuple, set)):
        return [_jsonable(x) for x in v]
    if isinstance(v, dict):
        return {str(k): _jsonable(x) for k, x in v.items()}
    return repr(v)


# ---------------------------------------------------------------- comandos

def cmd_ping(args):
    return {
        "blender": bpy.app.version_string,
        "file": bpy.data.filepath or "(nao salvo)",
        "scene": bpy.context.scene.name,
        "background": bpy.app.background,
        "objects": len(bpy.context.scene.objects),
    }


def cmd_scene_info(args):
    sc = bpy.context.scene
    act = bpy.context.view_layer.objects.active
    return {
        "scene": sc.name,
        "frame": sc.frame_current,
        "active": act.name if act else None,
        "mode": act.mode if act else "OBJECT",
        "objects": [_obj_summary(o) for o in sc.objects],
    }


def cmd_object_info(args):
    o = _obj(args.get("name"))
    d = _obj_summary(o)
    d["modifiers"] = []
    for m in getattr(o, "modifiers", []):
        md = {"name": m.name, "type": m.type, "show_viewport": m.show_viewport}
        for p in m.bl_rna.properties:
            if p.identifier in ("rna_type", "name", "type") or p.type in ("POINTER", "COLLECTION"):
                continue
            try:
                md[p.identifier] = _jsonable(getattr(m, p.identifier))
            except Exception:
                pass
        d["modifiers"].append(md)
    bb = [o.matrix_world @ mathutils.Vector(c) for c in o.bound_box]
    d["bbox_world"] = {
        "min": _vec([min(v[i] for v in bb) for i in range(3)]),
        "max": _vec([max(v[i] for v in bb) for i in range(3)]),
    }
    d["mode"] = o.mode
    d["collections"] = [c.name for c in o.users_collection]
    if o.type == "MESH":
        d["edges"] = len(o.data.edges)
        d["mesh"] = o.data.name
    return d


_PRIMS = {
    "cube": "primitive_cube_add",
    "uv_sphere": "primitive_uv_sphere_add",
    "ico_sphere": "primitive_ico_sphere_add",
    "cylinder": "primitive_cylinder_add",
    "cone": "primitive_cone_add",
    "torus": "primitive_torus_add",
    "plane": "primitive_plane_add",
    "circle": "primitive_circle_add",
    "monkey": "primitive_monkey_add",
}


def cmd_create_primitive(args):
    t = (args.get("type") or "cube").lower()
    if t not in _PRIMS:
        raise ValueError(f"type invalido: {t}. Use: {', '.join(_PRIMS)}")
    _ensure_object_mode()
    kw = {
        "location": tuple(args.get("location") or (0, 0, 0)),
        "rotation": tuple(_rad(args.get("rotation") or (0, 0, 0))),
    }
    size, radius, depth, seg = args.get("size"), args.get("radius"), args.get("depth"), args.get("segments")
    if size is not None and t in ("cube", "plane", "monkey"):
        kw["size"] = float(size)
    if t in ("uv_sphere", "ico_sphere", "cylinder", "circle"):
        r = radius if radius is not None else (float(size) / 2 if size is not None else None)
        if r is not None:
            kw["radius"] = float(r)
    if t == "cone" and radius is not None:
        kw["radius1"] = float(radius)
    if t == "torus" and radius is not None:
        kw["major_radius"] = float(radius)
    if depth is not None and t in ("cylinder", "cone"):
        kw["depth"] = float(depth)
    if seg is not None:
        seg = int(seg)
        if t in ("cylinder", "cone", "circle"):
            kw["vertices"] = seg
        elif t == "uv_sphere":
            kw["segments"] = seg
            kw["ring_count"] = max(3, seg // 2)
        elif t == "ico_sphere":
            kw["subdivisions"] = max(1, min(seg, 8))
        elif t == "torus":
            kw["major_segments"] = seg
    with _ctx_override():
        getattr(bpy.ops.mesh, _PRIMS[t])(**kw)
    o = bpy.context.view_layer.objects.active
    if args.get("name"):
        o.name = args["name"]
        if o.data:
            o.data.name = args["name"]
    if args.get("scale") is not None:
        o.scale = tuple(args["scale"])
    _undo_push("criar " + o.name)
    return _obj_summary(o)


def cmd_delete_object(args):
    o = _obj(args.get("name"))
    n = o.name
    bpy.data.objects.remove(o, do_unlink=True)
    _undo_push("apagar " + n)
    return {"deleted": n}


def cmd_set_transform(args):
    o = _obj(args.get("name"))
    if args.get("location") is not None:
        o.location = tuple(args["location"])
    if args.get("rotation") is not None:
        o.rotation_mode = "XYZ" if o.rotation_mode in ("QUATERNION", "AXIS_ANGLE") else o.rotation_mode
        o.rotation_euler = tuple(_rad(args["rotation"]))
    if args.get("scale") is not None:
        o.scale = tuple(args["scale"])
    _undo_push("transform " + o.name)
    return _obj_summary(o)


def _set_prop(target, key, val):
    cur = getattr(target, key)
    if isinstance(cur, bpy.types.ID) or (cur is None and isinstance(val, str)):
        # ponteiro para datablock: aceita nome de objeto/material/etc.
        idv = bpy.data.objects.get(val) or bpy.data.materials.get(val) or bpy.data.textures.get(val)
        setattr(target, key, idv)
    else:
        setattr(target, key, val)


def cmd_add_modifier(args):
    o = _obj(args.get("name"))
    mtype = (args.get("type") or "").upper()
    if not mtype:
        raise ValueError("informe 'type' (ex.: BEVEL, SUBSURF, MIRROR, ARRAY, SOLIDIFY, BOOLEAN)")
    m = o.modifiers.new(name=args.get("modifier_name") or mtype.title(), type=mtype)
    errs = {}
    for k, v in (args.get("props") or {}).items():
        try:
            _set_prop(m, k, v)
        except Exception as e:
            errs[k] = str(e)
    _undo_push("modificador " + m.name)
    r = {"object": o.name, "modifier": m.name, "type": m.type}
    if errs:
        r["prop_errors"] = errs
    return r


def cmd_apply_modifier(args):
    o = _obj(args.get("name"))
    mod = args.get("modifier")
    if mod not in o.modifiers:
        raise ValueError(f"modificador '{mod}' nao existe em '{o.name}'. Existentes: {[m.name for m in o.modifiers]}")
    _ensure_object_mode()
    _make_active(o)
    with _ctx_override(o):
        bpy.ops.object.modifier_apply(modifier=mod)
    _undo_push("aplicar " + mod)
    return _obj_summary(o)


def _principled(mat):
    try:
        if not mat.use_nodes:
            mat.use_nodes = True
    except Exception:
        pass
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        out = next((n for n in nt.nodes if n.type == "OUTPUT_MATERIAL"), None) or nt.nodes.new("ShaderNodeOutputMaterial")
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return bsdf


def cmd_set_material(args):
    o = _obj(args.get("name"))
    if not hasattr(o.data, "materials"):
        raise ValueError(f"objeto '{o.name}' ({o.type}) nao aceita material")
    mname = args.get("material_name") or (o.name + "_Mat")
    mat = bpy.data.materials.get(mname) or bpy.data.materials.new(mname)
    bsdf = _principled(mat)
    inp = bsdf.inputs
    if args.get("base_color") is not None:
        c = list(args["base_color"]) + [1.0] * (4 - len(args["base_color"]))
        inp["Base Color"].default_value = c[:4]
        mat.diffuse_color = c[:4]
    if args.get("metallic") is not None:
        inp["Metallic"].default_value = float(args["metallic"])
    if args.get("roughness") is not None:
        inp["Roughness"].default_value = float(args["roughness"])
    if args.get("emission_color") is not None:
        c = list(args["emission_color"]) + [1.0] * (4 - len(args["emission_color"]))
        inp["Emission Color"].default_value = c[:4]
    if args.get("emission_strength") is not None:
        inp["Emission Strength"].default_value = float(args["emission_strength"])
    if mat.name not in [m.name for m in o.data.materials if m]:
        if len(o.data.materials) == 0:
            o.data.materials.append(mat)
        else:
            o.data.materials[0] = mat
    _undo_push("material " + mat.name)
    return {"object": o.name, "material": mat.name}


def cmd_set_mode(args):
    o = _obj(args.get("name"))
    mode = (args.get("mode") or "OBJECT").upper()
    cur = bpy.context.view_layer.objects.active
    if cur is not None and cur != o and cur.mode != "OBJECT":
        _ensure_object_mode()
    _make_active(o)
    with _ctx_override(o):
        bpy.ops.object.mode_set(mode=mode)
    return {"object": o.name, "mode": o.mode}


def cmd_execute_python(args):
    code = args.get("code") or ""
    ns = {"bpy": bpy, "bmesh": bmesh, "mathutils": mathutils, "math": math, "C": bpy.context, "D": bpy.data}
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        exec(compile(code, "<mcp>", "exec"), ns)
    out = {"stdout": buf.getvalue()}
    if "result" in ns:
        r = ns["result"]
        try:
            json.dumps(r)
            out["result"] = r
        except Exception:
            out["result"] = _jsonable(r)
    _undo_push("python")
    return out


def _temp_png():
    fd, p = tempfile.mkstemp(prefix="mathninja_shot_", suffix=".png")
    os.close(fd)
    return p


def _resize_png(path, max_size):
    try:
        img = bpy.data.images.load(path, check_existing=False)
        w, h = img.size
        if max(w, h) > max_size:
            s = max_size / max(w, h)
            img.scale(max(1, int(w * s)), max(1, int(h * s)))
            img.filepath_raw = path
            img.file_format = "PNG"
            img.save()
        bpy.data.images.remove(img)
    except Exception:
        pass


def cmd_viewport_screenshot(args):
    max_size = int(args.get("max_size") or 1024)
    path = _temp_png()
    # 1) com interface: captura a area VIEW_3D
    wm = bpy.context.window_manager
    if not bpy.app.background and wm and wm.windows:
        for win in wm.windows:
            for area in win.screen.areas:
                if area.type == "VIEW_3D":
                    with bpy.context.temp_override(window=win, area=area, screen=win.screen):
                        bpy.ops.screen.screenshot_area(filepath=path)
                    _resize_png(path, max_size)
                    return {"path": path, "method": "screenshot_area"}
    # 2) sem interface: render da camera (OpenGL; se falhar, Workbench)
    sc = bpy.context.scene
    r = sc.render
    saved = (r.filepath, r.resolution_x, r.resolution_y, r.resolution_percentage, r.image_settings.file_format, r.engine, sc.camera)
    tmp_cam = None
    try:
        if sc.camera is None:
            cam_data = bpy.data.cameras.new("MCP_TempCam")
            tmp_cam = bpy.data.objects.new("MCP_TempCam", cam_data)
            sc.collection.objects.link(tmp_cam)
            pts = [o.matrix_world @ mathutils.Vector(c) for o in sc.objects if o.type == "MESH" for c in o.bound_box]
            center = sum(pts, mathutils.Vector()) / len(pts) if pts else mathutils.Vector()
            radius = max(((p - center).length for p in pts), default=2.0)
            direction = mathutils.Vector((1, -1, 0.8)).normalized()
            tmp_cam.location = center + direction * max(radius, 0.5) * 3.2
            tmp_cam.rotation_euler = (center - tmp_cam.location).to_track_quat("-Z", "Y").to_euler()
            sc.camera = tmp_cam
        aspect = saved[1] / saved[2] if saved[2] else 16 / 9
        if aspect >= 1:
            r.resolution_x, r.resolution_y = max_size, max(1, int(max_size / aspect))
        else:
            r.resolution_x, r.resolution_y = max(1, int(max_size * aspect)), max_size
        r.resolution_percentage = 100
        r.image_settings.file_format = "PNG"
        r.filepath = path
        method = "render_opengl"
        try:
            with _ctx_override():
                bpy.ops.render.opengl(write_still=True)
        except Exception:
            method = "render_workbench"
            r.engine = "BLENDER_WORKBENCH"
            bpy.ops.render.render(write_still=True)
        if not os.path.exists(path) or os.path.getsize(path) == 0:
            raise RuntimeError("render nao gerou imagem")
        return {"path": path, "method": method}
    finally:
        r.filepath, r.resolution_x, r.resolution_y, r.resolution_percentage = saved[0], saved[1], saved[2], saved[3]
        r.image_settings.file_format = saved[4]
        r.engine = saved[5]
        sc.camera = saved[6]
        if tmp_cam is not None:
            cd = tmp_cam.data
            bpy.data.objects.remove(tmp_cam, do_unlink=True)
            bpy.data.cameras.remove(cd)


def cmd_export(args):
    fmt = (args.get("format") or "fbx").lower()
    if fmt not in ("fbx", "glb", "obj"):
        raise ValueError("format deve ser fbx, glb ou obj")
    sel = bool(args.get("selected_only", False))
    path = args.get("path")
    if not path:
        base = os.path.splitext(os.path.basename(bpy.data.filepath))[0] if bpy.data.filepath else None
        if not base:
            act = bpy.context.view_layer.objects.active
            base = act.name if (sel and act) else "MathNinjaModel"
        path = os.path.join(DEFAULT_EXPORT_DIR, f"{base}.{fmt}")
    path = os.path.abspath(path)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    _ensure_object_mode()
    with _ctx_override():
        if fmt == "fbx":
            bpy.ops.export_scene.fbx(
                filepath=path, use_selection=sel, apply_scale_options="FBX_SCALE_ALL",
                axis_forward="-Z", axis_up="Y", apply_unit_scale=True, use_mesh_modifiers=True,
            )
        elif fmt == "glb":
            bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=sel, export_apply=True)
        else:
            bpy.ops.wm.obj_export(filepath=path, export_selected_objects=sel, apply_modifiers=True)
    if not os.path.exists(path):
        raise RuntimeError("exportacao nao gerou o arquivo " + path)
    return {"path": path, "format": fmt, "bytes": os.path.getsize(path)}


def cmd_save_file(args):
    path = args.get("path")
    with _ctx_override():
        if path:
            path = os.path.abspath(path)
            os.makedirs(os.path.dirname(path), exist_ok=True)
            bpy.ops.wm.save_as_mainfile(filepath=path)
        else:
            if not bpy.data.filepath:
                raise ValueError("arquivo ainda nao foi salvo: informe 'path' (.blend)")
            bpy.ops.wm.save_mainfile()
    return {"saved": bpy.data.filepath}


def cmd_undo(args):
    steps = int(args.get("steps") or 1)
    with _ctx_override():
        for _ in range(steps):
            bpy.ops.ed.undo()
    return {"undone": steps}


COMMANDS = {
    "ping": cmd_ping,
    "scene_info": cmd_scene_info,
    "object_info": cmd_object_info,
    "create_primitive": cmd_create_primitive,
    "delete_object": cmd_delete_object,
    "set_transform": cmd_set_transform,
    "add_modifier": cmd_add_modifier,
    "apply_modifier": cmd_apply_modifier,
    "set_material": cmd_set_material,
    "set_mode": cmd_set_mode,
    "execute_python": cmd_execute_python,
    "viewport_screenshot": cmd_viewport_screenshot,
    "export": cmd_export,
    "save_file": cmd_save_file,
    "undo": cmd_undo,
}


# ---------------------------------------------------------------- fila / thread principal

class _Job:
    def __init__(self, cmd, args):
        self.cmd, self.args = cmd, args
        self.done = threading.Event()
        self.response = None
        self.cancelled = False


def _run_job(job):
    fn = COMMANDS.get(job.cmd)
    if fn is None:
        return {"ok": False, "error": f"comando desconhecido: {job.cmd}", "traceback": ""}
    try:
        return {"ok": True, "result": _jsonable(fn(job.args or {}))}
    except Exception as e:
        return {"ok": False, "error": f"{type(e).__name__}: {e}", "traceback": traceback.format_exc()}


def process_queue(max_jobs=20):
    """Executa os comandos pendentes. Precisa rodar na thread principal."""
    n = 0
    while n < max_jobs:
        try:
            job = _queue.get_nowait()
        except queue.Empty:
            break
        if not job.cancelled:
            job.response = _run_job(job)
        job.done.set()
        n += 1
    return n


def _timer():
    process_queue()
    return 0.05 if _state["server"] is not None else None


def _ensure_timer():
    if _state["server"] is not None and not bpy.app.timers.is_registered(_timer):
        bpy.app.timers.register(_timer, first_interval=0.05, persistent=True)


# ---------------------------------------------------------------- servidor HTTP

class _Handler(BaseHTTPRequestHandler):
    server_version = "MathNinjaMCP/1.0"

    def log_message(self, fmt, *a):
        pass

    def _send(self, code, obj):
        body = json.dumps(obj).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _guard(self):
        if self.headers.get("Origin") is not None:
            self._send(403, {"ok": False, "error": "requisicoes de navegador (Origin) nao sao permitidas"})
            return False
        tok = self.headers.get("X-MCP-Token") or ""
        if not _state["token"] or not secrets.compare_digest(tok, _state["token"]):
            self._send(401, {"ok": False, "error": "token invalido"})
            return False
        return True

    def do_GET(self):
        if self._guard():
            self._send(405, {"ok": False, "error": "use POST /cmd"})

    def do_POST(self):
        if not self._guard():
            return
        if self.path.rstrip("/") != "/cmd":
            self._send(404, {"ok": False, "error": "use POST /cmd"})
            return
        try:
            n = int(self.headers.get("Content-Length") or 0)
            data = json.loads(self.rfile.read(n).decode("utf-8") or "{}")
            cmd, args = data.get("cmd"), data.get("args") or {}
            if not isinstance(args, dict):
                raise ValueError("'args' deve ser um objeto")
        except Exception as e:
            self._send(400, {"ok": False, "error": f"JSON invalido: {e}"})
            return
        job = _Job(cmd, args)
        _queue.put(job)
        if not job.done.wait(CMD_TIMEOUT):
            job.cancelled = True
            self._send(504, {"ok": False, "error": f"timeout de {int(CMD_TIMEOUT)} s esperando o Blender (ocupado ou modal aberto?)"})
            return
        self._send(200, job.response)


def _write_bridge():
    os.makedirs(BRIDGE_DIR, exist_ok=True)
    tmp = BRIDGE_FILE + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump({"port": PORT, "token": _state["token"], "pid": os.getpid(), "blender": bpy.app.version_string}, f)
    os.replace(tmp, BRIDGE_FILE)


def _remove_bridge():
    try:
        with open(BRIDGE_FILE, encoding="utf-8") as f:
            if json.load(f).get("token") != _state["token"]:
                return
        os.remove(BRIDGE_FILE)
    except Exception:
        pass


def is_running():
    return _state["server"] is not None


def start_server():
    if is_running():
        return True
    try:
        srv = ThreadingHTTPServer((HOST, PORT), _Handler)
    except OSError as e:
        _state["error"] = f"porta {PORT} ocupada: {e}"
        print("[MathNinja MCP]", _state["error"])
        return False
    srv.daemon_threads = True
    _state["token"] = secrets.token_hex(24)
    _state["server"] = srv
    _state["error"] = None
    th = threading.Thread(target=srv.serve_forever, name="MathNinjaMCP", daemon=True)
    th.start()
    _state["thread"] = th
    _write_bridge()
    _ensure_timer()
    print(f"[MathNinja MCP] servidor em http://{HOST}:{PORT}")
    return True


def stop_server():
    srv = _state["server"]
    if srv is None:
        return
    _remove_bridge()
    _state["server"] = None
    try:
        srv.shutdown()
        srv.server_close()
    except Exception:
        pass
    # libera quem estiver esperando
    while True:
        try:
            job = _queue.get_nowait()
        except queue.Empty:
            break
        job.response = {"ok": False, "error": "servidor parado", "traceback": ""}
        job.done.set()
    _state["token"] = None
    _state["thread"] = None
    if bpy.app.timers.is_registered(_timer):
        bpy.app.timers.unregister(_timer)
    print("[MathNinja MCP] servidor parado")


atexit.register(_remove_bridge)


@persistent
def _on_load_post(*_a):
    _ensure_timer()


# ---------------------------------------------------------------- UI

class MATHNINJA_OT_toggle_server(bpy.types.Operator):
    bl_idname = "mathninja.toggle_mcp_server"
    bl_label = "Iniciar/Parar servidor MCP"
    bl_description = "Inicia ou para a ponte MCP local"

    def execute(self, context):
        if is_running():
            stop_server()
        elif not start_server():
            self.report({"ERROR"}, _state["error"] or "falha ao iniciar")
            return {"CANCELLED"}
        return {"FINISHED"}


class MATHNINJA_PT_panel(bpy.types.Panel):
    bl_label = "MathNinja MCP"
    bl_idname = "MATHNINJA_PT_mcp"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "MathNinja"

    def draw(self, context):
        col = self.layout.column()
        if is_running():
            col.label(text="Status: conectado", icon="CHECKMARK")
            col.label(text=f"{HOST}:{PORT}")
            col.operator(MATHNINJA_OT_toggle_server.bl_idname, text="Parar servidor", icon="PAUSE")
        else:
            col.label(text="Status: parado", icon="CANCEL")
            if _state["error"]:
                col.label(text=_state["error"][:60], icon="ERROR")
            col.operator(MATHNINJA_OT_toggle_server.bl_idname, text="Iniciar servidor", icon="PLAY")


_classes = (MATHNINJA_OT_toggle_server, MATHNINJA_PT_panel)


def register():
    for c in _classes:
        bpy.utils.register_class(c)
    if _on_load_post not in bpy.app.handlers.load_post:
        bpy.app.handlers.load_post.append(_on_load_post)
    # Em --background (ex.: instalacao) nao sobe o servidor sozinho; o teste chama start_server().
    if not bpy.app.background:
        start_server()


def unregister():
    stop_server()
    if _on_load_post in bpy.app.handlers.load_post:
        bpy.app.handlers.load_post.remove(_on_load_post)
    for c in reversed(_classes):
        bpy.utils.unregister_class(c)
