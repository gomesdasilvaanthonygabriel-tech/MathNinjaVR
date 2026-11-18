using UnityEngine;

/// <summary>Animacao ociosa do professor robo: respira, escreve no quadro e mexe a cabeca.</summary>
public class RobotTeacher : MonoBehaviour
{
    public Transform upperBody;
    public Transform head;
    public Transform rightArm;
    public Transform leftArm;
    public Renderer antennaBulb;

    Vector3 bodyPos;
    Quaternion headRot, rightRot, leftRot;
    MaterialPropertyBlock block;

    void Start()
    {
        if (upperBody) bodyPos = upperBody.localPosition;
        if (head) headRot = head.localRotation;
        if (rightArm) rightRot = rightArm.localRotation;
        if (leftArm) leftRot = leftArm.localRotation;
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        float t = Time.time;

        if (upperBody)
            upperBody.localPosition = bodyPos + Vector3.up * Mathf.Sin(t * 1.6f) * 0.012f;

        if (head)
            head.localRotation = headRot * Quaternion.Euler(Mathf.Sin(t * 0.7f) * 3f, Mathf.Sin(t * 0.45f) * 14f, 0f);

        // braco direito "escrevendo" no quadro
        if (rightArm)
            rightArm.localRotation = rightRot * Quaternion.Euler(Mathf.Sin(t * 2.4f) * 5f, 0f, Mathf.Sin(t * 1.2f) * 9f);

        if (leftArm)
            leftArm.localRotation = leftRot * Quaternion.Euler(Mathf.Sin(t * 1.1f) * 3f, 0f, Mathf.Sin(t * 0.9f) * 2f);

        if (antennaBulb)
        {
            float pulse = 1.5f + Mathf.Sin(t * 3f);
            if (block == null) block = new MaterialPropertyBlock();
            antennaBulb.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.2f) * pulse);
            antennaBulb.SetPropertyBlock(block);
        }
    }
}
