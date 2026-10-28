using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 给角色头/脚分区点击：自动按包围盒生成碰撞体，
/// 鼠标与手指走同一条射线检测。
/// </summary>
[DisallowMultipleComponent]
public class CharacterPartClickController : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent onHeadClicked;
    public UnityEvent onFootClicked;

    [Header("Hitbox layout (relative to renderer bounds)")]
    [Range(0.05f, 0.5f)] public float headHeightRatio = 0.22f;
    [Range(0.05f, 0.4f)] public float footHeightRatio = 0.14f;
    [Range(0.2f, 1.2f)] public float headRadiusRatio = 0.55f;
    [Range(0.2f, 1.2f)] public float footRadiusRatio = 0.45f;

    [Header("Debug")]
    public bool logClicks = true;
    public bool showScreenHint = true;

    string _hint;
    float _hintUntil;
    Camera _cam;

    void Awake()
    {
        _cam = Camera.main;
        EnsureHitboxes();
    }

    void Update()
    {
        if (_cam == null)
            _cam = Camera.main;

        if (TryGetPointerDown(out var screenPos))
            TryClick(screenPos);
    }

    void OnGUI()
    {
        if (!showScreenHint || Time.unscaledTime > _hintUntil || string.IsNullOrEmpty(_hint))
            return;

        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 28,
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Box(new Rect(20, 40, Screen.width - 40, 64), _hint, style);
    }

    [ContextMenu("Rebuild Hitboxes")]
    public void RebuildHitboxes()
    {
        DestroyChild("Hitbox_Head");
        DestroyChild("Hitbox_Foot");
        EnsureHitboxes();
    }

    void EnsureHitboxes()
    {
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning("[CharacterPartClick] 找不到 Renderer，无法自动生成头/脚碰撞盒。", this);
            return;
        }

        var bounds = renderer.bounds;
        var localCenter = transform.InverseTransformPoint(bounds.center);
        var lossy = transform.lossyScale;
        var inv = new Vector3(
            Mathf.Approximately(lossy.x, 0f) ? 1f : 1f / Mathf.Abs(lossy.x),
            Mathf.Approximately(lossy.y, 0f) ? 1f : 1f / Mathf.Abs(lossy.y),
            Mathf.Approximately(lossy.z, 0f) ? 1f : 1f / Mathf.Abs(lossy.z));

        var worldSize = bounds.size;
        var localSize = Vector3.Scale(worldSize, inv);
        var height = localSize.y;
        var horiz = Mathf.Max(localSize.x, localSize.z);

        CreateSphereHitbox(
            "Hitbox_Head",
            BodyPartType.Head,
            new Vector3(localCenter.x, localCenter.y + height * (0.5f - headHeightRatio * 0.5f), localCenter.z),
            horiz * headRadiusRatio * 0.5f);

        CreateSphereHitbox(
            "Hitbox_Foot",
            BodyPartType.Foot,
            new Vector3(localCenter.x, localCenter.y - height * (0.5f - footHeightRatio * 0.5f), localCenter.z),
            horiz * footRadiusRatio * 0.5f);
    }

    void CreateSphereHitbox(string name, BodyPartType part, Vector3 localPos, float localRadius)
    {
        var existing = transform.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(transform, false);
        }

        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var col = go.GetComponent<SphereCollider>();
        if (col == null)
            col = go.AddComponent<SphereCollider>();
        col.isTrigger = false;
        col.radius = Mathf.Max(0.01f, localRadius);

        var hitbox = go.GetComponent<BodyPartHitbox>();
        if (hitbox == null)
            hitbox = go.AddComponent<BodyPartHitbox>();
        hitbox.part = part;
    }

    void DestroyChild(string name)
    {
        var t = transform.Find(name);
        if (t == null)
            return;
        if (Application.isPlaying)
            Destroy(t.gameObject);
        else
            DestroyImmediate(t.gameObject);
    }

    static bool TryGetPointerDown(out Vector2 screenPos)
    {
        if (Input.GetMouseButtonDown(0))
        {
            screenPos = Input.mousePosition;
            return true;
        }

        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                screenPos = touch.position;
                return true;
            }
        }

        screenPos = default;
        return false;
    }

    void TryClick(Vector2 screenPos)
    {
        if (_cam == null)
            return;

        var ray = _cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out var hit, 100f))
            return;

        var hitbox = hit.collider.GetComponent<BodyPartHitbox>()
                     ?? hit.collider.GetComponentInParent<BodyPartHitbox>();
        if (hitbox == null)
            return;

        // 只响应挂在本角色下的命中盒，避免点到别的物体。
        if (!hitbox.transform.IsChildOf(transform) && hitbox.transform != transform)
            return;

        HandlePartClicked(hitbox.part);
    }

    void HandlePartClicked(BodyPartType part)
    {
        switch (part)
        {
            case BodyPartType.Head:
                onHeadClicked?.Invoke();
                ShowHint("点到了：头");
                if (logClicks)
                    Debug.Log("[CharacterPartClick] Head", this);
                break;
            case BodyPartType.Foot:
                onFootClicked?.Invoke();
                ShowHint("点到了：脚");
                if (logClicks)
                    Debug.Log("[CharacterPartClick] Foot", this);
                break;
        }
    }

    void ShowHint(string text)
    {
        _hint = text;
        _hintUntil = Time.unscaledTime + 1.2f;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        DrawHitboxGizmo("Hitbox_Head", new Color(1f, 0.4f, 0.2f, 0.35f));
        DrawHitboxGizmo("Hitbox_Foot", new Color(0.2f, 0.7f, 1f, 0.35f));
    }

    void DrawHitboxGizmo(string name, Color color)
    {
        var t = transform.Find(name);
        if (t == null)
            return;
        var col = t.GetComponent<SphereCollider>();
        if (col == null)
            return;
        Gizmos.color = color;
        Gizmos.matrix = t.localToWorldMatrix;
        Gizmos.DrawSphere(col.center, col.radius);
    }
#endif
}
