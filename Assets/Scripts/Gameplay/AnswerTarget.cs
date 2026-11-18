using System;
using System.Collections.Generic;
using UnityEngine;

public enum TargetKind { Answer, Grenade }

/// <summary>
/// Alvo cortavel: esfera com numero (resposta) ou granada. Fisica propria de gravidade
/// (gravityScale) para mais tempo no ar. O rotulo sempre olha para a camera.
/// Ao descer perto do chao, o collider e desligado: o alvo atravessa o piso e some.
/// Pode ser cortado pela lamina (Slice).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AnswerTarget : MonoBehaviour
{
    /// <summary>Alvos vivos e ainda nao cortados (usado pela lamina).</summary>
    public static readonly List<AnswerTarget> Active = new List<AnswerTarget>();
    /// <summary>Disparado quando um alvo e cortado (uma unica vez por alvo).</summary>
    public static event Action<AnswerTarget> Sliced;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active.Clear();
        Sliced = null;
        block = null;
    }

    [SerializeField] TargetKind kind = TargetKind.Answer;

    public int value;
    public bool isCorrect;
    public float radius = 0.22f;

    [SerializeField] TextMesh label;
    [SerializeField] Renderer bodyRenderer;
    [Tooltip("Material das particulas da explosao (granada).")]
    [SerializeField] Material explosionMaterial;

    [Tooltip("1 = gravidade normal. Menor que 1 deixa o alvo mais tempo no ar.")]
    public float gravityScale = 0.7f;
    [Tooltip("Tempo maximo de vida (seguranca).")]
    public float lifetime = 6f;
    public float labelOffset = 0.27f;

    [Header("Atravessar o chao")]
    [Tooltip("Altura (centro) abaixo da qual, descendo, o alvo deixa de colidir e atravessa o chao.")]
    public float fallThroughHeight = 0.7f;
    [Tooltip("Altura abaixo da qual o alvo e destruido (ja escondido pelo piso).")]
    public float destroyHeight = -0.8f;

    Rigidbody rb;
    Collider col;
    Camera cam;
    Color color = Color.white;
    float age;
    bool isSliced;
    static MaterialPropertyBlock block;

    public TargetKind Kind => kind;
    public bool IsGrenade => kind == TargetKind.Grenade;
    /// <summary>Ainda pode ser cortado (nao cortado nem removido).</summary>
    public bool IsAlive => !isSliced;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rb.useGravity = false; // gravidade aplicada manualmente em FixedUpdate
    }

    void OnEnable()
    {
        if (!isSliced && !Active.Contains(this)) Active.Add(this);
    }

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

    /// <summary>Remove o alvo sem conta-lo como corte (fim de onda/partida).</summary>
    public void Despawn()
    {
        isSliced = true;
        Active.Remove(this);
        Destroy(gameObject);
    }

    /// <summary>Remove todos os alvos ativos sem contar cortes.</summary>
    public static void DespawnAll()
    {
        var all = Active.ToArray();
        foreach (var t in all)
            if (t != null) t.Despawn();
        Active.Clear();
    }

    /// <summary>Corta o alvo: esfera vira duas metades, granada explode. Conta uma unica vez.</summary>
    public void Slice(Vector3 cutDirectionWorld)
    {
        if (isSliced) return;
        isSliced = true;
        Active.Remove(this);

        if (IsGrenade) ExplosionEffect.Spawn(transform.position, explosionMaterial);
        else SpawnHalves(cutDirectionWorld);

        Sliced?.Invoke(this);
        Destroy(gameObject);
    }

    void SpawnHalves(Vector3 cutDirectionWorld)
    {
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
        if (age > lifetime || transform.position.y < destroyHeight) Despawn();
    }

    void LateUpdate()
    {
        if (label == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        Vector3 toCam = (cam.transform.position - transform.position).normalized;
        label.transform.position = transform.position + toCam * labelOffset;
        label.transform.rotation = cam.transform.rotation;
    }
}
