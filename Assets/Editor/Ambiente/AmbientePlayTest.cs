using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Ferramentas de teste em Play: inicia uma partida sem clicar na UI e captura a visao do jogador.</summary>
public static class AmbientePlayTest
{
    [MenuItem("MathNinja/Ambiente/Teste - Iniciar Partida (Play)")]
    static void StartMatch()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Ambiente] entre em Play primeiro"); return; }
        Application.runInBackground = true; // so nesta sessao de Play (PlayerSettings nao muda): o Editor fica sem foco durante o teste
        var flow = Object.FindFirstObjectByType<GameFlow>();
        var m = typeof(GameFlow).GetMethod("HandleStart", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var ps = m.GetParameters();
        m.Invoke(flow, new object[] { System.Enum.GetValues(ps[0].ParameterType).GetValue(0), System.Enum.GetValues(ps[1].ParameterType).GetValue(0) });
        Debug.Log("[Ambiente] partida iniciada para teste");
    }

    [MenuItem("MathNinja/Ambiente/Teste - Estado (Play)")]
    static void State()
    {
        var targets = Object.FindObjectsByType<AnswerTarget>(FindObjectsSortMode.None);
        var rt = Object.FindFirstObjectByType<RobotTeacher>();
        var sp = Object.FindFirstObjectByType<AnswerSpawner>();
        Debug.Log($"[Ambiente] t={Time.time:F2} frame={Time.frameCount} paused={EditorApplication.isPaused} runInBg={Application.runInBackground} alvos ativos={targets.Length} pos=[{string.Join(" ", targets.Take(4).Select(t => t.transform.position.ToString("F1")))}] " +
                  $"robo.upperBody.y={(rt && rt.upperBody ? rt.upperBody.localPosition.y.ToString("F4") : "-")} pergunta='{(sp && sp.questionText ? sp.questionText.text : "")}'");
    }

    [MenuItem("MathNinja/Ambiente/Teste - Captura em Play")]
    static void Capture()
    {
        var cam = Camera.main;
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
        cam.targetTexture = null; RenderTexture.active = null;
        File.WriteAllBytes(@"C:\Users\cliente\Projetos\MathNinja\Backups\previews\unity_ambiente_play.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        Debug.Log("[Ambiente] captura em play salva");
    }
}
