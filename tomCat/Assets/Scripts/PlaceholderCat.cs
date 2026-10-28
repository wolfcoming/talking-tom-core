using UnityEngine;

/// <summary>
/// M0 占位角色：用基础几何拼一只看得见的卡通猫。
/// 以后换成可商用模型时，删掉本组件即可。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlaceholderCat : MonoBehaviour
{
    static readonly Color Fur = new Color(1f, 0.62f, 0.22f);
    static readonly Color Accent = new Color(0.95f, 0.45f, 0.15f);
    static readonly Color EyeWhite = Color.white;
    static readonly Color Pupil = new Color(0.12f, 0.12f, 0.12f);
    static readonly Color Nose = new Color(0.9f, 0.35f, 0.4f);

    void OnEnable()
    {
        BuildIfNeeded();
    }

    [ContextMenu("Rebuild Placeholder")]
    public void Rebuild()
    {
        ClearChildren();
        BuildIfNeeded();
    }

    void BuildIfNeeded()
    {
        if (transform.childCount > 0)
            return;

        var bodyMat = MakeMaterial(Fur);
        var accentMat = MakeMaterial(Accent);
        var eyeMat = MakeMaterial(EyeWhite);
        var pupilMat = MakeMaterial(Pupil);
        var noseMat = MakeMaterial(Nose);

        // 身体略前倾站立，方便正面看。
        var body = Part("Body", PrimitiveType.Capsule, new Vector3(0f, 0.85f, 0f), new Vector3(0.85f, 0.9f, 0.7f), bodyMat);
        body.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);

        var head = Part("Head", PrimitiveType.Sphere, new Vector3(0f, 1.85f, 0.12f), Vector3.one * 0.72f, bodyMat);

        Part("EarL", PrimitiveType.Cube, new Vector3(-0.28f, 0.38f, 0f), new Vector3(0.22f, 0.35f, 0.12f), accentMat, head.transform)
            .transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        Part("EarR", PrimitiveType.Cube, new Vector3(0.28f, 0.38f, 0f), new Vector3(0.22f, 0.35f, 0.12f), accentMat, head.transform)
            .transform.localRotation = Quaternion.Euler(0f, 0f, -18f);

        var eyeL = Part("EyeL", PrimitiveType.Sphere, new Vector3(-0.18f, 0.08f, 0.3f), Vector3.one * 0.2f, eyeMat, head.transform);
        Part("PupilL", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.08f), Vector3.one * 0.45f, pupilMat, eyeL.transform);

        var eyeR = Part("EyeR", PrimitiveType.Sphere, new Vector3(0.18f, 0.08f, 0.3f), Vector3.one * 0.2f, eyeMat, head.transform);
        Part("PupilR", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.08f), Vector3.one * 0.45f, pupilMat, eyeR.transform);

        Part("Nose", PrimitiveType.Sphere, new Vector3(0f, -0.05f, 0.36f), new Vector3(0.1f, 0.08f, 0.08f), noseMat, head.transform);

        // 尾巴：稍后 M2 可换成独立碰撞部位。
        var tail = Part("Tail", PrimitiveType.Capsule, new Vector3(0.35f, 0.55f, -0.45f), new Vector3(0.18f, 0.55f, 0.18f), accentMat);
        tail.transform.localRotation = Quaternion.Euler(55f, -25f, 20f);

        // 身体碰撞体，留给 M1 戳击打断。
        if (GetComponent<CapsuleCollider>() == null)
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.1f, 0f);
            col.height = 2.2f;
            col.radius = 0.55f;
        }
    }

    GameObject Part(string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material mat, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.hideFlags = HideFlags.DontSaveInEditor;
        go.transform.SetParent(parent == null ? transform : parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = mat;

        var col = go.GetComponent<Collider>();
        if (col != null)
            DestroyImmediateSafe(col);

        return go;
    }

    static Material MakeMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Diffuse");
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        return mat;
    }

    void ClearChildren()
    {
        for (var i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediateSafe(transform.GetChild(i).gameObject);
    }

    static void DestroyImmediateSafe(Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(obj);
        else
            Object.DestroyImmediate(obj);
    }
}
