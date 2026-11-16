using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lamina do mouse: segurar o botao esquerdo e arrastar corta as esferas que o
/// movimento atravessa (teste em espaco de tela, segmento do frame anterior ate o atual).
/// </summary>
public class Blade : MonoBehaviour
{
    public Camera cam;
    [Tooltip("Movimento minimo (pixels) entre dois frames para contar como corte.")]
    public float minMovePixels = 4f;
    [Tooltip("Folga extra (pixels) no raio de acerto da esfera.")]
    public float hitPadding = 6f;
    public float trailDistance = 1.5f;

    TrailRenderer trail;
    bool dragging;
    Vector2 last;

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
        // copia: Slice remove da lista Active
        var targets = AnswerTarget.Active.ToArray();
        foreach (var t in targets)
        {
            if (t == null) continue;
            Vector3 center = cam.WorldToScreenPoint(t.transform.position);
            if (center.z <= 0f) continue;

            Vector3 edge = cam.WorldToScreenPoint(t.transform.position + cam.transform.right * t.radius);
            float radiusPx = Mathf.Abs(edge.x - center.x) + hitPadding;

            if (DistanceToSegment(new Vector2(center.x, center.y), a, b) <= radiusPx)
            {
                Vector2 dirScreen = (b - a).normalized;
                Vector3 dirWorld = cam.transform.right * dirScreen.x + cam.transform.up * dirScreen.y;
                t.Slice(dirWorld);
            }
        }
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
