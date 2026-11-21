using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Cria os materiais URP do ambiente Blender e faz diagnosticos do FBX importado.</summary>
public static class AmbienteSetup
{
    public const string Dir = "Assets/Art/Ambiente";
    public const string MatDir = Dir + "/Materials";
    public const string FbxPath = Dir + "/MathNinja-Ambiente.fbx";
    const float FloorTile = 4.8f; // T_Piso_Taco cobre 4,8 x 4,8 m (UVs do piso em metros)

    [Serializable] class BMat { public string name; public float[] @base; public float rough, metal, alpha, trans; public float[] emis; public float es; }
    [Serializable] class BMats { public BMat[] items; }

    static Color Lin(float[] c) => new Color(c[0], c[1], c[2]).gamma; // Blender guarda cor linear; SetColor espera sRGB

    static Color HdrFromLinear(Color e) =>
        new Color(Mathf.LinearToGammaSpace(e.r), Mathf.LinearToGammaSpace(e.g), Mathf.LinearToGammaSpace(e.b));

    [MenuItem("MathNinja/Ambiente/1 Criar Materiais URP")]
    public static void CreateMaterials()
    {
        var data = JsonUtility.FromJson<BMats>(File.ReadAllText(Dir + "/blender_materials.json"));
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Dir, "Materials");
        var floorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Textures/T_Piso_Taco.png");
        var skyTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Textures/T_Ceu_Fundo.png");
        int n = 0;
        foreach (var b in data.items)
        {
            string path = MatDir + "/" + b.name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.SetColor("_BaseColor", Lin(b.@base));
            m.SetFloat("_Metallic", b.metal);
            m.SetFloat("_Smoothness", Mathf.Clamp01(1f - b.rough));
            m.SetFloat("_Cull", 2f);
            SetOpaque(m);
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            m.SetColor("_EmissionColor", Color.black);
            m.SetTexture("_EmissionMap", null);

            float emisScale = 0f;
            if (b.es > 0.001f)
            {
                // intensidades para tempo real: luminaria clara, teto so um toque (nao estourar)
                if (b.name == "M_Luminaria_Emissiva") emisScale = 2.2f;
                else if (b.name == "M_Teto") emisScale = 0.03f;
                else if (b.name == "M_Robo_Olhos") emisScale = 1.6f;
                else if (b.name == "M_Corredor") emisScale = 0.25f;
                else if (b.name == "M_Ceu_Fundo") emisScale = 0.9f;
                else emisScale = Mathf.Min(b.es, 2f);
            }
            if (b.name == "M_Piso_Taco")
            {
                m.SetTexture("_BaseMap", floorTex);
                m.SetColor("_BaseColor", Color.white);
                m.SetTextureScale("_BaseMap", new Vector2(1f / FloorTile, 1f / FloorTile));
                m.SetFloat("_Smoothness", 0.4f);
            }
            if (b.name == "M_Ceu_Fundo")
            {
                m.SetTexture("_BaseMap", skyTex);
                m.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.35f));
                m.SetTexture("_EmissionMap", skyTex);
                m.SetFloat("_Smoothness", 0f);
            }
            if (emisScale > 0f)
            {
                var e = new Color(b.emis[0], b.emis[1], b.emis[2]) * emisScale; // linear
                m.SetColor("_EmissionColor", HdrFromLinear(e));
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (b.trans > 0.5f || b.alpha < 0.99f)
            {
                var c = Lin(b.@base); c.a = Mathf.Clamp(b.alpha, 0.15f, 0.35f);
                m.SetColor("_BaseColor", c);
                SetTransparent(m);
                m.SetFloat("_Smoothness", 0.92f);
            }
            if (b.name.StartsWith("M_Vidro") || b.name.StartsWith("M_Papel") || b.name.StartsWith("M_Giz") || b.name == "M_Ceu_Fundo"
                || b.name == "M_Corredor" || b.name == "M_Caneta_Azul" || b.name == "M_Cortina_Creme")
                m.SetFloat("_Cull", 0f); // pecas planas: duas faces
            EditorUtility.SetDirty(m);
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Ambiente] {n} materiais URP Lit em {MatDir}");
    }

    static void SetOpaque(Material m)
    {
        m.SetFloat("_Surface", 0f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
        m.SetFloat("_ZWrite", 1f);
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Opaque");
        m.renderQueue = -1;
        m.SetShaderPassEnabled("DepthOnly", true); m.SetShaderPassEnabled("ShadowCaster", true);
    }

    static void SetTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetShaderPassEnabled("DepthOnly", false); m.SetShaderPassEnabled("ShadowCaster", false);
    }

    [MenuItem("MathNinja/Ambiente/Diagnostico FBX")]
    public static void Diagnose()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (go == null) { Debug.LogError("[Ambiente] FBX nao encontrado: " + FbxPath); return; }
        var sb = new StringBuilder("[Ambiente] Diagnostico\n");
        var inst = (GameObject)UnityEngine.Object.Instantiate(go);
        try
        {
            inst.transform.position = Vector3.zero;
            sb.AppendLine("root rot=" + inst.transform.rotation.eulerAngles + " scale=" + inst.transform.localScale);
            Bounds all = new Bounds(); bool first = true;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                if (r.name == "Fundo_Ceu" || r.name == "Corredor_Fundo") continue;
                if (first) { all = r.bounds; first = false; } else all.Encapsulate(r.bounds);
            }
            sb.AppendLine("bounds(sem ceu) center=" + all.center.ToString("F3") + " size=" + all.size.ToString("F3"));
            foreach (var nm in new[] { "Piso", "Teto", "Parede_Frente", "Parede_Fundo", "Parede_Janelas", "Parede_Porta", "Quadro_Superficie", "RobotTeacher", "RobotTeacher_Corpo", "RobotTeacher_Roupa", "Kyoudan_Tablado", "Luminaria_1_Difusor", "Fundo_Ceu" })
            {
                var t = inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == nm);
                if (t == null) { sb.AppendLine(nm + ": AUSENTE"); continue; }
                var r = t.GetComponent<Renderer>();
                sb.AppendLine(nm + ": parent=" + t.parent.name + " lpos=" + t.localPosition.ToString("F3") + " lrot=" + t.localEulerAngles.ToString("F1") + " lscale=" + t.localScale.ToString("F3") +
                    (r ? " bounds c=" + r.bounds.center.ToString("F3") + " s=" + r.bounds.size.ToString("F3") + " mats=" + string.Join(",", r.sharedMaterials.Select(m => m ? m.name + "(" + m.shader.name + ")" : "NULL")) : ""));
            }
            var rs = inst.GetComponentsInChildren<Renderer>();
            var foreign = rs.SelectMany(r => r.sharedMaterials).Where(m => m == null || !AssetDatabase.GetAssetPath(m).StartsWith(MatDir)).Select(m => m ? m.name : "NULL").Distinct().ToList();
            sb.AppendLine("renderers=" + rs.Length + " filhosDiretos=" + inst.transform.childCount + " matsForaDaPasta=" + string.Join(",", foreign));
            sb.AppendLine("colliders=" + inst.GetComponentsInChildren<Collider>().Length);
        }
        finally { UnityEngine.Object.DestroyImmediate(inst); }
        Debug.Log(sb.ToString());
    }
}
