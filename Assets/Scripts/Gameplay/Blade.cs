using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lamina do mouse: segurar o botao esquerdo e arrastar corta os alvos (esferas e
/// granadas) que o movimento atravessa (teste em espaco de tela, segmento do frame
/// anterior ate o atual). Desligada (enabled = false) fora da partida.
/// </summary>
public class Blade : MonoBehaviour
{
    public Camera cam;
    [Tooltip("Movimento minimo (pixels) entre dois frames para contar como corte.")]
    public float minMovePixels = 4f;
    [Tooltip("Folga extra (pixels) no raio de acerto do alvo.")]
    public float hitPadding = 6f;
    public float trailDistance = 1.5f;

    TrailRenderer trail;
    bool dragging;
    Vector2 last;
    readonly List<AnswerTarget> hits = new List<AnswerTarget>();

    void Awake()
    {
        if (cam == null) cam = Camera.main;

        var go = new GameObject("BladeTrail");
        go.transform.SetParent(transform, false);
        trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.minVertexDistance = 0.01f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.045f), new Keyframe(1f, 0f));
        trail.material = new Material(Shader.Find("Sprites/Default"));
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.6f, 0.95f, 1f), 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = grad;
        trail.emitting = false;
    }

    void OnDisable()
    {
        dragging = false;
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        Vector2 pos = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            dragging = true;
            last = pos;
            trail.Clear();
            trail.transform.position = ToWorld(pos);
            trail.emitting = true;
        }

        if (!dragging) return;

        trail.transform.position = ToWorld(pos);

        if (mouse.leftButton.isPressed)
        {
            if ((pos - last).sqrMagnitude >= minMovePixels * minMovePixels)
            {
                CheckCuts(last, pos);
                last = pos;
            }
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            dragging = false;
            trail.emitting = false;
        }
    }

    Vector3 ToWorld(Vector2 screen) => cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, trailDistance));

    void CheckCuts(Vector2 a, Vector2 b)
    {
        // 1) coleta todos os alvos atravessados pelo segmento (antes de cortar qualquer um)
        hits.Clear();
        foreach (var t in AnswerTarget.Active)
        {
            if (t == null || !t.IsAlive) continue;
            Vector3 center = cam.WorldToScreenPoint(t.transform.position);
            if (center.z <= 0f) continue;

            Vector3 edge = cam.WorldToScreenPoint(t.transform.position + cam.transform.right * t.radius);
            float radiusPx = Mathf.Abs(edge.x - center.x) + hitPadding;

            if (DistanceToSegment(new Vector2(center.x, center.y), a, b) <= radiusPx)
                hits.Add(t);
        }
        if (hits.Count == 0) return;

        // 2) corta cada um uma unica vez (Slice ignora repeticoes)
        Vector2 dirScreen = (b - a).normalized;
        Vector3 dirWorld = cam.transform.right * dirScreen.x + cam.transform.up * dirScreen.y;
        foreach (var t in hits)
        {
            if (!enabled) break; // a partida pode ter acabado no meio deste corte
            if (t != null && t.IsAlive) t.Slice(dirWorld);
        }
        hits.Clear();
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }
}
