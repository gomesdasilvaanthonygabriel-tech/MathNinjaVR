using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Troca a sala antiga (gerada pelo ClassroomBuilder) pelo ambiente do Blender na Game.unity,
/// preservando a jogabilidade (Gameplay, GameUI, camera, textos do quadro, RobotTeacher).
/// Menus em MathNinja/Ambiente. Idempotente o bastante para rodar de novo sem duplicar.
/// </summary>
public static class AmbienteSceneSetup
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    // capturas ficam em <projeto>/Previews (fora do Git)
    static string PreviewDir => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Previews");

    // sala: 16 x 14 x 4,2 m, centro do piso na origem, quadro na parede +Z
    const float W = 16f, D = 14f, H = 4.2f, T = 0.2f;
    const float BoardZ = 6.94f; // frente do Quadro_Superficie (6,96) menos 2 cm

    [MenuItem("MathNinja/Ambiente/2a Remapear Materiais do FBX")]
    public static void RemapMaterials()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(AmbienteSetup.FbxPath);
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        bool ok = mi.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Local);
        mi.SaveAndReimport();
        Debug.Log("[Ambiente] remap de materiais: " + ok + " | remaps=" + mi.GetExternalObjectMap().Count);
    }

    [MenuItem("MathNinja/Ambiente/2b Montar Cena")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
        var log = new StringBuilder("[Ambiente] Montar cena\n");

        var classroom = GameObject.Find("Classroom");
        var amb = GameObject.Find("Ambiente");
        if (amb == null) amb = new GameObject("Ambiente");
        amb.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // 1) modelo do Blender (mantem o link com o FBX)
        var env = amb.transform.Find("MathNinja-Ambiente")?.gameObject;
        if (env == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(AmbienteSetup.FbxPath);
            env = (GameObject)PrefabUtility.InstantiatePrefab(src, amb.transform);
            env.name = "MathNinja-Ambiente";
        }
        // FBX ja vem com o quadro em +Z, janelas em +X e porta em -X (igual a sala antiga): sem rotacao
        env.transform.localPosition = Vector3.zero;
        env.transform.localRotation = Quaternion.identity;
        env.transform.localScale = Vector3.one;
        GameObjectUtility.SetStaticEditorFlags(env, (StaticEditorFlags)~0);
        GameObjectUtility.SetStaticEditorFlags(amb, (StaticEditorFlags)~0);

        // contas de giz do Blender ficariam por baixo da pergunta do jogo: escondidas
        foreach (var t in env.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Conta_")) t.gameObject.SetActive(false);

        // 2) textos do quadro (jogabilidade: AnswerSpawner.questionText/feedbackText)
        var texts = Child(amb.transform, "QuadroTextos");
        if (classroom != null)
        {
            foreach (var nm in new[] { "BoardTitle", "QuestionText", "FeedbackText" })
            {
                var t = classroom.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == nm);
                if (t != null) t.SetParent(texts, false);
            }
        }
        SetLocal(texts, "BoardTitle", new Vector3(0f, 2.12f, BoardZ));
        SetLocal(texts, "QuestionText", new Vector3(0f, 1.62f, BoardZ));
        SetLocal(texts, "FeedbackText", new Vector3(0f, 1.22f, BoardZ));

        // 3) luzes antigas reaproveitadas na grade das luminarias novas (x = -5,0,5; z = -4,5,0,4,5)
        var lights = Child(amb.transform, "Luzes");
        if (classroom != null)
            foreach (var l in classroom.GetComponentsInChildren<Light>(true))
                l.transform.SetParent(lights, true);
        for (int i = 0; i < 9; i++)
        {
            var lt = lights.Find("PointLight_" + i);
            if (lt == null) continue;
            int c = i / 3, r = i % 3;
            lt.localPosition = new Vector3(-5f + 5f * c, 3.8f, -4.5f + 4.5f * r);
            var L = lt.GetComponent<Light>();
            L.range = 8f;
        }
        var bl = lights.Find("BoardLight");
        if (bl) bl.localPosition = new Vector3(0f, 3.4f, 4.0f);

        // 4) RobotTeacher: mesmo GameObject/script, visual novo (meshes do FBX)
        var robot = GameObject.Find("RobotTeacher")?.GetComponent<RobotTeacher>();
        if (robot == null) robot = Object.FindObjectsByType<RobotTeacher>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (robot == null) { Debug.LogError("[Ambiente] RobotTeacher nao encontrado"); return; }
        robot.transform.SetParent(amb.transform, true);
        var srcCorpo = env.GetComponentsInChildren<Transform>(true).First(x => x.name == "RobotTeacher_Corpo");
        var srcRoupa = env.GetComponentsInChildren<Transform>(true).First(x => x.name == "RobotTeacher_Roupa");
        bool rebuild = robot.transform.Find("Visual") == null;
        if (rebuild)
        {
            for (int i = robot.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(robot.transform.GetChild(i).gameObject);
            robot.transform.SetPositionAndRotation(new Vector3(srcCorpo.position.x, 0f, srcCorpo.position.z), Quaternion.identity);
            robot.transform.localScale = Vector3.one;
            var visual = Child(robot.transform, "Visual");
            visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity;
            Clone(srcRoupa, visual);
            Clone(srcCorpo, visual);
        }
        srcCorpo.gameObject.SetActive(false); // copia do FBX desligada: so um professor visivel
        srcRoupa.gameObject.SetActive(false);
        GameObjectUtility.SetStaticEditorFlags(robot.gameObject, 0);
        foreach (var t in robot.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        var corpoR = robot.transform.Find("Visual/RobotTeacher_Corpo").GetComponent<MeshRenderer>();
        var so = new SerializedObject(robot);
        so.FindProperty("upperBody").objectReferenceValue = robot.transform.Find("Visual");
        so.FindProperty("head").objectReferenceValue = null;      // mesh novo e peca unica: sem cabeca/bracos separados
        so.FindProperty("rightArm").objectReferenceValue = null;
        so.FindProperty("leftArm").objectReferenceValue = null;
        so.FindProperty("antennaBulb").objectReferenceValue = corpoR;
        so.FindProperty("bulbMaterialIndex").intValue = System.Array.FindIndex(corpoR.sharedMaterials, m => m && m.name == "M_Robo_Olhos");
        so.FindProperty("bulbColor").colorValue = new Color(0.4f, 0.85f, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();

        // 5) colisores feitos a mao
        var col = Child(amb.transform, "Colisores");
        Box(col, "Col_Piso", new Vector3(0, -0.05f, 0), new Vector3(W + 2 * T, 0.1f, D + 2 * T));   // topo em y = 0
        Box(col, "Col_Teto", new Vector3(0, H + 0.05f, 0), new Vector3(W + 2 * T, 0.1f, D + 2 * T));
        Box(col, "Col_Parede_Frente", new Vector3(0, H / 2, D / 2 + T / 2), new Vector3(W + 2 * T, H, T));
        Box(col, "Col_Parede_Fundo", new Vector3(0, H / 2, -D / 2 - T / 2), new Vector3(W + 2 * T, H, T));
        Box(col, "Col_Parede_Janelas", new Vector3(W / 2 + T / 2, H / 2, 0), new Vector3(T, H, D));
        Box(col, "Col_Parede_Porta", new Vector3(-W / 2 - T / 2, H / 2, 0), new Vector3(T, H, D));
        Box(col, "Col_Tablado", new Vector3(0, 0.1f, 6.445f), new Vector3(6.42f, 0.2f, 1.11f));
        GameObjectUtility.SetStaticEditorFlags(col.gameObject, (StaticEditorFlags)~0);

        // 6) remove a sala antiga (paredes, piso, teto, janelas, quadro, carteiras, decoracao, paineis de luz)
        if (classroom != null)
        {
            var refs = ExternalRefsInto(classroom);
            if (refs.Count > 0)
            {
                Debug.LogError("[Ambiente] Ainda ha referencias para a sala antiga, nada apagado:\n" + string.Join("\n", refs));
                return;
            }
            Object.DestroyImmediate(classroom);
            log.AppendLine("Classroom antiga removida");
        }

        // 7) iluminacao em tempo real
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.50f, 0.50f, 0.54f);
        foreach (var l in lights.GetComponentsInChildren<Light>(true))
            if (l.name.StartsWith("PointLight_")) l.intensity = 3.2f;
        if (bl) bl.GetComponent<Light>().intensity = 2.5f;
        var sun = GameObject.Find("Directional Light");
        if (sun) sun.GetComponent<Light>().intensity = 0.35f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine(Report());
        Debug.Log(log.ToString());
    }

    [MenuItem("MathNinja/Ambiente/Relatorio")]
    public static void LogReport() => Debug.Log(Report());

    static string Report()
    {
        var sb = new StringBuilder("[Ambiente] Relatorio\n");
        var env = GameObject.Find("Ambiente/MathNinja-Ambiente");
        if (env)
        {
            Bounds b = new Bounds(); bool f = true;
            foreach (var r in env.GetComponentsInChildren<Renderer>())
            {
                if (r.name == "Fundo_Ceu" || r.name == "Corredor_Fundo") continue;
                if (!(r.name.StartsWith("Parede") || r.name == "Piso" || r.name == "Teto")) continue;
                if (f) { b = r.bounds; f = false; } else b.Encapsulate(r.bounds);
            }
            sb.AppendLine("Estrutura bounds: center=" + b.center.ToString("F2") + " size=" + b.size.ToString("F2"));
            var piso = env.GetComponentsInChildren<Renderer>().First(r => r.name == "Piso").bounds;
            var teto = env.GetComponentsInChildren<Renderer>().First(r => r.name == "Teto").bounds;
            sb.AppendLine("Interior: " + piso.size.x.ToString("F2") + " x " + piso.size.z.ToString("F2") + " x " + (teto.min.y - piso.max.y).ToString("F2") + " m");
            var q = env.GetComponentsInChildren<Renderer>().First(r => r.name == "Quadro_Superficie").bounds;
            sb.AppendLine("Quadro centro=" + q.center.ToString("F2"));
        }
        var robots = Object.FindObjectsByType<RobotTeacher>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine("RobotTeacher componentes=" + robots.Length);
        foreach (var rt in robots)
            sb.AppendLine($"  {rt.name} pos={rt.transform.position:F2} upperBody={(rt.upperBody ? rt.upperBody.name : "null")} bulb={(rt.antennaBulb ? rt.antennaBulb.name : "null")} idx={rt.bulbMaterialIndex}");
        int activeRobotMeshes = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.name.StartsWith("RobotTeacher_"));
        sb.AppendLine("Meshes de professor ativos=" + activeRobotMeshes);
        var sp = Object.FindFirstObjectByType<AnswerSpawner>();
        if (sp) sb.AppendLine("Spawner question=" + (sp.questionText ? sp.questionText.name : "NULL") + " feedback=" + (sp.feedbackText ? sp.feedbackText.name : "NULL"));
        var cam = Camera.main;
        if (cam) sb.AppendLine("Camera " + cam.transform.position.ToString("F2") + " fwd=" + cam.transform.forward.ToString("F2"));
        sb.AppendLine("Luzes=" + Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length + " ambient=" + RenderSettings.ambientLight);
        int pink = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r => r.sharedMaterials)
            .Count(m => m == null || m.shader == null || !m.shader.isSupported || m.shader.name.Contains("Error"));
        sb.AppendLine("Materiais nulos/invalidos=" + pink);
        sb.AppendLine("MeshColliders=" + Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Length +
                      " BoxColliders=" + Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Length);
        return sb.ToString();
    }

    // ---------------------------------------------------------------- capturas

    [MenuItem("MathNinja/Ambiente/3 Capturas")]
    public static void Captures()
    {
        Directory.CreateDirectory(PreviewDir);
        var cam = Camera.main;
        Render(cam, Path.Combine(PreviewDir, "unity_ambiente_novo.png"));

        // visao geral: de cima, em diagonal, sem teto e sem as paredes do lado da camera
        var go = new GameObject("__PreviewCam");
        var hidden = new List<Renderer>();
        try
        {
            var c = go.AddComponent<Camera>();
            c.CopyFrom(cam);
            c.fieldOfView = 55f;
            c.transform.position = new Vector3(-11.5f, 12.5f, -13.5f);
            c.transform.LookAt(new Vector3(0.5f, 0.3f, 1.0f));
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.enabled && (r.name.StartsWith("Teto") || r.name.StartsWith("Luminaria_") || r.name == "Parede_Fundo" || r.name == "Parede_Porta"
                    || r.name == "Barrado" || r.name == "Rodape" || r.name == "Faixa_MeiaAltura" || r.name == "Corredor_Fundo"))
                { r.enabled = false; hidden.Add(r); }
            Render(c, Path.Combine(PreviewDir, "unity_ambiente_geral.png"));
        }
        finally
        {
            foreach (var r in hidden) r.enabled = true;
            Object.DestroyImmediate(go);
        }
        Debug.Log("[Ambiente] capturas salvas em " + PreviewDir);
    }

    static void Render(Camera cam, string path)
    {
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var prevT = cam.targetTexture; var prevA = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        finally
        {
            cam.targetTexture = prevT; RenderTexture.active = prevA;
            rt.Release(); Object.DestroyImmediate(rt);
        }
    }

    // ---------------------------------------------------------------- helpers

    static Transform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
        return t;
    }

    static void SetLocal(Transform parent, string name, Vector3 pos)
    {
        var t = parent.Find(name);
        if (t) { t.localPosition = pos; t.localRotation = Quaternion.identity; }
    }

    static void Clone(Transform src, Transform parent)
    {
        var go = new GameObject(src.name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(src.position, src.rotation);
        go.transform.localScale = src.lossyScale;
        go.AddComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials;
    }

    static void Box(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var t = Child(parent, name);
        t.localPosition = center; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        var bc = t.GetComponent<BoxCollider>();
        if (bc == null) bc = t.gameObject.AddComponent<BoxCollider>();
        bc.center = Vector3.zero; bc.size = size;
    }

    /// <summary>Referencias serializadas de objetos fora da sala antiga apontando para dentro dela.</summary>
    static List<string> ExternalRefsInto(GameObject root)
    {
        var inside = new HashSet<Object>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            inside.Add(t.gameObject);
            foreach (var c in t.GetComponents<Component>()) if (c) inside.Add(c);
        }
        var res = new List<string>();
        foreach (var go in root.scene.GetRootGameObjects())
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || inside.Contains(c)) continue;
                var so = new SerializedObject(c);
                var p = so.GetIterator();
                while (p.Next(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue && inside.Contains(p.objectReferenceValue))
                        res.Add(c.GetType().Name + "@" + c.name + "." + p.propertyPath + " -> " + p.objectReferenceValue.name);
            }
        return res;
    }
}
