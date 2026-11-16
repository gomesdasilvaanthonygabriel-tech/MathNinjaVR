// MathNinja MCP Bridge: servidor HTTP local (127.0.0.1) dentro do Unity Editor.
// O servidor MCP (Node, pasta unity-mcp) conversa com este script.
// Seguranca: so escuta em loopback, exige token (Library/mcp-bridge.json), recusa requisicoes com Origin (navegador).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MathNinja.MCP
{
    [Serializable] class Req { public string cmd; public string json; }
    [Serializable] class Res { public bool ok; public string result; public string error; }

    [Serializable] class ArgHierarchy { public int maxDepth = 3; }
    [Serializable] class ArgPath { public string path; }
    [Serializable] class ArgCreate { public string name; public string parent; public string primitive; public string prefab; public string position; }
    [Serializable] class ArgTransform { public string path; public string position; public string rotation; public string scale; public bool local = true; }
    [Serializable] class ArgComponent { public string path; public string type; }
    [Serializable] class ArgProp { public string path; public string component; public string property; public string value; }
    [Serializable] class ArgGetProps { public string path; public string component; }
    [Serializable] class ArgFile { public string path; public string content; }
    [Serializable] class ArgPlay { public string action; }
    [Serializable] class ArgScene { public string path; public string action; }
    [Serializable] class ArgMenu { public string item; }
    [Serializable] class ArgConsole { public int count = 50; public bool clear; }
    [Serializable] class ArgSetActive { public string path; public bool active; }

    [InitializeOnLoad]
    public static class MCPBridge
    {
        const int BasePort = 8765;
        static HttpListener listener;
        static Thread thread;
        static string token;
        static int port;
        static readonly ConcurrentQueue<Action> mainQueue = new ConcurrentQueue<Action>();
        static readonly List<string> logs = new List<string>();
        static readonly object logLock = new object();

        static MCPBridge()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            Application.logMessageReceivedThreaded += OnLog;
            EditorApplication.update += Pump;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            Start();
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            lock (logLock)
            {
                logs.Add("[" + type + "] " + msg);
                if (logs.Count > 500) logs.RemoveRange(0, logs.Count - 500);
            }
        }

        static void Pump()
        {
            int n = 0;
            while (n++ < 20 && mainQueue.TryDequeue(out var a)) a();
        }

        static string ProjectRoot() { return Directory.GetParent(Application.dataPath).FullName; }

        static void Start()
        {
            if (listener != null) return;
            token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            for (int p = BasePort; p < BasePort + 20; p++)
            {
                try
                {
                    var l = new HttpListener();
                    l.Prefixes.Add("http://127.0.0.1:" + p + "/");
                    l.Start();
                    listener = l; port = p;
                    break;
                }
                catch { }
            }
            if (listener == null) { Debug.LogWarning("[MCPBridge] nao foi possivel abrir uma porta."); return; }
            try
            {
                var info = "{\"port\":" + port + ",\"token\":\"" + token + "\"}";
                File.WriteAllText(Path.Combine(ProjectRoot(), "Library", "mcp-bridge.json"), info);
            }
            catch (Exception e) { Debug.LogWarning("[MCPBridge] " + e.Message); }
            thread = new Thread(Loop) { IsBackground = true };
            thread.Start();
            Debug.Log("[MCPBridge] ouvindo em 127.0.0.1:" + port);
        }

        static void Stop()
        {
            try { listener?.Close(); } catch { }
            listener = null;
            try { File.Delete(Path.Combine(ProjectRoot(), "Library", "mcp-bridge.json")); } catch { }
        }

        static void Loop()
        {
            var l = listener;
            while (l != null && l.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = l.GetContext(); } catch { break; }
                ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
            }
        }

        static void Handle(HttpListenerContext ctx)
        {
            Res res = new Res();
            try
            {
                var rq = ctx.Request;
                string host = rq.Headers["Host"] ?? "";
                if (rq.Headers["Origin"] != null || !(host == "127.0.0.1:" + port || host == "localhost:" + port))
                    throw new Exception("requisicao recusada");
                if (rq.Headers["X-MCP-Token"] != token) throw new Exception("token invalido");
                if (rq.HttpMethod != "POST") throw new Exception("use POST");
                string body;
                using (var sr = new StreamReader(rq.InputStream, Encoding.UTF8)) body = sr.ReadToEnd();
                var req = JsonUtility.FromJson<Req>(body);
                var tcs = new TaskCompletionSource<string>();
                mainQueue.Enqueue(() =>
                {
                    try { tcs.SetResult(Dispatch(req.cmd, req.json ?? "{}")); }
                    catch (Exception e) { tcs.SetException(e); }
                });
                if (!tcs.Task.Wait(60000)) throw new Exception("timeout no editor (compilando ou em dialogo?)");
                res.ok = true; res.result = tcs.Task.Result;
            }
            catch (Exception e)
            {
                res.ok = false;
                res.error = (e is AggregateException ae && ae.InnerException != null) ? ae.InnerException.Message : e.Message;
            }
            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(res));
                ctx.Response.ContentType = "application/json";
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                ctx.Response.Close();
            }
            catch { }
        }

        // ---------- comandos (rodam na thread principal) ----------
        static string Dispatch(string cmd, string json)
        {
            switch (cmd)
            {
                case "ping": return "pong | Unity " + Application.unityVersion + " | play=" + EditorApplication.isPlaying + " | compiling=" + EditorApplication.isCompiling + " | cena=" + SceneManager.GetActiveScene().name;
                case "get_hierarchy": return Hierarchy(JsonUtility.FromJson<ArgHierarchy>(json).maxDepth);
                case "create_gameobject": return Create(JsonUtility.FromJson<ArgCreate>(json));
                case "delete_gameobject": { var g = Find(JsonUtility.FromJson<ArgPath>(json).path); Undo.DestroyObjectImmediate(g); return "removido"; }
                case "set_active": { var a = JsonUtility.FromJson<ArgSetActive>(json); var g = Find(a.path); Undo.RecordObject(g, "active"); g.SetActive(a.active); return "ok"; }
                case "set_transform": return SetTransform(JsonUtility.FromJson<ArgTransform>(json));
                case "add_component": return AddComponent(JsonUtility.FromJson<ArgComponent>(json));
                case "get_properties": return GetProps(JsonUtility.FromJson<ArgGetProps>(json));
                case "set_property": return SetProp(JsonUtility.FromJson<ArgProp>(json));
                case "write_file": return WriteFile(JsonUtility.FromJson<ArgFile>(json));
                case "read_file": return File.ReadAllText(SafePath(JsonUtility.FromJson<ArgFile>(json).path));
                case "list_files": return ListFiles(JsonUtility.FromJson<ArgFile>(json).path);
                case "refresh": AssetDatabase.Refresh(); return "refresh pedido | compiling=" + EditorApplication.isCompiling;
                case "play": return Play(JsonUtility.FromJson<ArgPlay>(json).action);
                case "scene": return SceneCmd(JsonUtility.FromJson<ArgScene>(json));
                case "menu": { var m = JsonUtility.FromJson<ArgMenu>(json).item; return EditorApplication.ExecuteMenuItem(m) ? "executado" : "menu nao encontrado: " + m; }
                case "console": return Console(JsonUtility.FromJson<ArgConsole>(json));
                default: throw new Exception("comando desconhecido: " + cmd);
            }
        }

        static string Hierarchy(int maxDepth)
        {
            var sb = new StringBuilder();
            var scene = SceneManager.GetActiveScene();
            sb.AppendLine("cena: " + scene.name + " (" + scene.path + ")");
            foreach (var r in scene.GetRootGameObjects()) Walk(r.transform, 0, maxDepth, sb);
            return sb.ToString();
        }

        static void Walk(Transform t, int depth, int max, StringBuilder sb)
        {
            var comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
            sb.AppendLine(new string(' ', depth * 2) + t.name + (t.gameObject.activeSelf ? "" : " (inativo)") + (comps.Length > 0 ? " [" + comps + "]" : ""));
            if (depth >= max) { if (t.childCount > 0) sb.AppendLine(new string(' ', depth * 2 + 2) + "... " + t.childCount + " filhos"); return; }
            foreach (Transform c in t) Walk(c, depth + 1, max, sb);
        }

        static GameObject Find(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new Exception("path vazio");
            var parts = path.Trim('/').Split('/');
            Transform cur = null;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == parts[0]) { cur = root.transform; break; }
            if (cur == null) throw new Exception("objeto nao encontrado: " + path);
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur.Find(parts[i]);
                if (next == null) throw new Exception("objeto nao encontrado: " + path);
                cur = next;
            }
            return cur.gameObject;
        }

        static string PathOf(GameObject g)
        {
            var p = g.name; var t = g.transform.parent;
            while (t != null) { p = t.name + "/" + p; t = t.parent; }
            return p;
        }

        static Vector3 V3(string s)
        {
            var p = s.Split(',');
            if (p.Length != 3) throw new Exception("vetor invalido (use 'x,y,z'): " + s);
            return new Vector3(F(p[0]), F(p[1]), F(p[2]));
        }
        static float F(string s) { return float.Parse(s.Trim(), CultureInfo.InvariantCulture); }

        static string Create(ArgCreate a)
        {
            GameObject g;
            if (!string.IsNullOrEmpty(a.prefab))
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(a.prefab);
                if (pf == null) throw new Exception("prefab nao encontrado: " + a.prefab);
                g = (GameObject)PrefabUtility.InstantiatePrefab(pf, SceneManager.GetActiveScene());
            }
            else if (!string.IsNullOrEmpty(a.primitive))
            {
                if (!Enum.TryParse<PrimitiveType>(a.primitive, true, out var pt)) throw new Exception("primitivo invalido: " + a.primitive);
                g = GameObject.CreatePrimitive(pt);
            }
            else g = new GameObject();
            if (!string.IsNullOrEmpty(a.name)) g.name = a.name;
            if (!string.IsNullOrEmpty(a.parent)) g.transform.SetParent(Find(a.parent).transform, false);
            if (!string.IsNullOrEmpty(a.position)) g.transform.localPosition = V3(a.position);
            Undo.RegisterCreatedObjectUndo(g, "MCP create");
            EditorSceneManager.MarkSceneDirty(g.scene);
            return PathOf(g);
        }

        static string SetTransform(ArgTransform a)
        {
            var t = Find(a.path).transform;
            Undo.RecordObject(t, "MCP transform");
            if (!string.IsNullOrEmpty(a.position)) { if (a.local) t.localPosition = V3(a.position); else t.position = V3(a.position); }
            if (!string.IsNullOrEmpty(a.rotation)) { if (a.local) t.localEulerAngles = V3(a.rotation); else t.eulerAngles = V3(a.rotation); }
            if (!string.IsNullOrEmpty(a.scale)) t.localScale = V3(a.scale);
            EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            return "ok";
        }

        static Type ResolveType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }
                foreach (var t in types)
                    if ((t.Name == name || t.FullName == name) && typeof(Component).IsAssignableFrom(t)) return t;
            }
            throw new Exception("componente nao encontrado: " + name + " (script novo ainda compilando?)");
        }

        static Component GetComp(GameObject g, string type)
        {
            var t = ResolveType(type);
            var c = g.GetComponent(t);
            if (c == null) throw new Exception("o objeto nao tem o componente " + type);
            return c;
        }

        static string AddComponent(ArgComponent a)
        {
            var g = Find(a.path);
            var c = Undo.AddComponent(g, ResolveType(a.type));
            EditorSceneManager.MarkSceneDirty(g.scene);
            return "adicionado " + c.GetType().Name;
        }

        static string GetProps(ArgGetProps a)
        {
            var c = GetComp(Find(a.path), a.component);
            var so = new SerializedObject(c);
            var sb = new StringBuilder();
            var it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                sb.AppendLine(it.propertyPath + " (" + it.propertyType + ") = " + ValueString(it));
            }
            return sb.ToString();
        }

        static string ValueString(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.intValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.Float: return p.floatValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Enum: return p.enumDisplayNames.Length > p.enumValueIndex && p.enumValueIndex >= 0 ? p.enumDisplayNames[p.enumValueIndex] : p.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString();
                case SerializedPropertyType.Color: return p.colorValue.ToString();
                case SerializedPropertyType.ObjectReference: return p.objectReferenceValue == null ? "null" : (AssetDatabase.GetAssetPath(p.objectReferenceValue) is string ap && ap.Length > 0 ? ap : p.objectReferenceValue.name);
                default: return "(" + p.propertyType + ")";
            }
        }

        static string SetProp(ArgProp a)
        {
            var g = Find(a.path);
            var c = GetComp(g, a.component);
            var so = new SerializedObject(c);
            var p = so.FindProperty(a.property);
            if (p == null) throw new Exception("propriedade nao encontrada: " + a.property);
            var v = a.value ?? "";
            var inv = CultureInfo.InvariantCulture;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: p.intValue = int.Parse(v, inv); break;
                case SerializedPropertyType.Boolean: p.boolValue = bool.Parse(v); break;
                case SerializedPropertyType.Float: p.floatValue = float.Parse(v, inv); break;
                case SerializedPropertyType.String: p.stringValue = v; break;
                case SerializedPropertyType.Enum:
                    { int i = Array.IndexOf(p.enumNames, v); p.enumValueIndex = i >= 0 ? i : int.Parse(v, inv); break; }
                case SerializedPropertyType.Vector2:
                    { var s = v.Split(','); p.vector2Value = new Vector2(F(s[0]), F(s[1])); break; }
                case SerializedPropertyType.Vector3: p.vector3Value = V3(v); break;
                case SerializedPropertyType.Color:
                    { var s = v.Split(','); p.colorValue = new Color(F(s[0]), F(s[1]), F(s[2]), s.Length > 3 ? F(s[3]) : 1f); break; }
                case SerializedPropertyType.ObjectReference:
                    if (v == "null" || v == "") p.objectReferenceValue = null;
                    else
                    {
                        var o = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(v);
                        if (o == null) { try { o = Find(v); } catch { } }
                        if (o == null) throw new Exception("referencia nao encontrada: " + v);
                        p.objectReferenceValue = o;
                    }
                    break;
                default: throw new Exception("tipo nao suportado: " + p.propertyType);
            }
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(g.scene);
            return "ok";
        }

        static string SafePath(string rel)
        {
            if (string.IsNullOrEmpty(rel)) throw new Exception("path vazio");
            var root = ProjectRoot();
            var full = Path.GetFullPath(Path.Combine(root, rel));
            var assets = Path.GetFullPath(Path.Combine(root, "Assets")) + Path.DirectorySeparatorChar;
            var pkgs = Path.GetFullPath(Path.Combine(root, "Packages")) + Path.DirectorySeparatorChar;
            var ps = Path.GetFullPath(Path.Combine(root, "ProjectSettings")) + Path.DirectorySeparatorChar;
            if (!(full.StartsWith(assets, StringComparison.OrdinalIgnoreCase) || full.StartsWith(pkgs, StringComparison.OrdinalIgnoreCase) || full.StartsWith(ps, StringComparison.OrdinalIgnoreCase)))
                throw new Exception("caminho fora de Assets/Packages/ProjectSettings: " + rel);
            return full;
        }

        static string WriteFile(ArgFile a)
        {
            var full = SafePath(a.path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, a.content ?? "", new UTF8Encoding(false));
            AssetDatabase.Refresh();
            return "gravado " + a.path + " | compiling=" + EditorApplication.isCompiling;
        }

        static string ListFiles(string rel)
        {
            var full = SafePath(string.IsNullOrEmpty(rel) ? "Assets" : rel);
            var root = ProjectRoot() + Path.DirectorySeparatorChar;
            var items = Directory.GetFileSystemEntries(full, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta")).Select(f => f.Substring(root.Length).Replace('\\', '/'));
            return string.Join("\n", items.Take(500));
        }

        static string Play(string action)
        {
            switch (action)
            {
                case "play": EditorApplication.isPlaying = true; return "entrando em play";
                case "stop": EditorApplication.isPlaying = false; return "parando";
                case "pause": EditorApplication.isPaused = !EditorApplication.isPaused; return "pausa=" + EditorApplication.isPaused;
                default: throw new Exception("acao invalida (play|stop|pause)");
            }
        }

        static string SceneCmd(ArgScene a)
        {
            switch (a.action)
            {
                case "save": EditorSceneManager.SaveOpenScenes(); return "salvo";
                case "open": EditorSceneManager.OpenScene(a.path); return "aberto " + a.path;
                case "new": EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single); return "nova cena";
                case "save_as": EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), a.path); return "salvo em " + a.path;
                default: throw new Exception("acao invalida (save|open|new|save_as)");
            }
        }

        static string Console(ArgConsole a)
        {
            lock (logLock)
            {
                if (a.clear) { logs.Clear(); return "limpo"; }
                int n = Math.Min(a.count <= 0 ? 50 : a.count, logs.Count);
                return string.Join("\n", logs.Skip(logs.Count - n));
            }
        }
    }
}
// reload
