using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Esfera com um numero (resposta). Fisica propria de gravidade (gravityScale) para
/// uma queda realista porem com mais tempo no ar. O rotulo sempre olha para a camera.
/// Ao descer perto do chao, o collider e desligado: a esfera atravessa o piso e some.
/// Pode ser cortada pela lamina (Slice).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AnswerTarget : MonoBehaviour
{
    /// <summary>Esferas vivas e ainda nao cortadas (usado pela lamina).</summary>
    public static readonly List<AnswerTarget> Active = new List<AnswerTarget>();
    /// <summary>Disparado quando uma esfera e cortada.</summary>
    public static event Action<AnswerTarget> Sliced;

    public int value;
    public bool isCorrect;
    public float radius = 0.22f;

    [SerializeField] TextMesh label;
    [SerializeField] Renderer bodyRenderer;

    [Tooltip("1 = gravidade normal. Menor que 1 deixa a esfera mais tempo no ar.")]
    public float gravityScale = 0.7f;
    [Tooltip("Tempo maximo de vida (seguranca).")]
    public float lifetime = 6f;
    public float labelOffset = 0.27f;

    [Header("Atravessar o chao")]
    [Tooltip("Altura (centro da esfera) abaixo da qual, descendo, ela deixa de colidir e atravessa o chao.")]
    public float fallThroughHeight = 0.7f;
    [Tooltip("Altura abaixo da qual a esfera e destruida (ja escondida pelo piso).")]
    public float destroyHeight = -0.8f;

    Rigidbody rb;
    Collider col;
    Camera cam;
    Color color;
    float age;
    bool isSliced;
    static MaterialPropertyBlock block;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.useGravity = false; // gravidade aplicada manualmente em FixedUpdate
    }

    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    public void Setup(int number, bool correct, Color c)
    {
        value = number;
        isCorrect = correct;
        color = c;
        if (label != null) label.text = number.ToString();
        if (bodyRenderer != null)
        {
            if (block == null) block = new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            bodyRenderer.SetPropertyBlock(block);
        }
    }

    public void Launch(Vector3 velocity, Vector3 angularVelocity)
    {
        rb.linearVelocity = velocity;
        rb.angularVelocity = angularVelocity;
    }

    /// <summary>Corta a esfera: gera duas metades que se afastam e remove a esfera.</summary>
    public void Slice(Vector3 cutDirectionWorld)
    {
        if (isSliced) return;
        isSliced = true;
        Active.Remove(this);

        // metades se afastam perpendicularmente ao corte
        Vector3 side = Vector3.Cross(cutDirectionWorld.normalized, Vector3.forward);
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        side.Normalize();

        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? 1f : -1f;
            var half = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            half.name = "SlicedHalf";
            Destroy(half.GetComponent<Collider>());
            half.transform.position = transform.position + side * s * 0.06f;
            half.transform.rotation = transform.rotation;
            half.transform.localScale = new Vector3(0.44f, 0.22f, 0.44f);
            half.transform.up = side * s;
            var r = half.GetComponent<Renderer>();
            if (bodyRenderer != null) r.sharedMaterial = bodyRenderer.sharedMaterial;
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            r.SetPropertyBlock(block);

            var hrb = half.AddComponent<Rigidbody>();
            hrb.mass = 0.1f;
            hrb.linearVelocity = rb.linearVelocity * 0.6f + side * s * 1.2f;
            hrb.angularVelocity = UnityEngine.Random.onUnitSphere * 3f;
            Destroy(half, 1.2f);
        }

        Sliced?.Invoke(this);
        Destroy(gameObject);
    }

    void FixedUpdate()
    {
        rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);

        // descendo e perto do chao: desliga a colisao para atravessar o piso
        if (col != null && col.enabled && rb.linearVelocity.y < 0f && rb.position.y < fallThroughHeight)
            col.enabled = false;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age > lifetime || transform.position.y < destroyHeight) Destroy(gameObject);
    }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || label == null) return;
        Vector3 toCam = (cam.transform.position - transform.position).normalized;
        label.transform.position = transform.position + toCam * labelOffset;
        label.transform.rotation = cam.transform.rotation;
    }
}
