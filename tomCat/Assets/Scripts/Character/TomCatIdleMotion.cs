using System.Collections;
using UnityEngine;

/// <summary>
/// Tom 风格待机：只动上半身（脊柱/头），髋与腿不动，脚始终贴地。
/// </summary>
[DisallowMultipleComponent]
public class TomCatIdleMotion : MonoBehaviour
{
    [Header("Upper body only (auto if empty)")]
    public Transform spine;
    public Transform spine1;
    public Transform head;

    [Header("Breathing (spine pitch)")]
    public float breathDegrees = 2.5f;
    public float breathSpeed = 1.4f;

    [Header("Sway (spine roll, feet stay)")]
    public float swayDegrees = 3f;
    public float swaySpeed = 0.65f;

    [Header("Occasional head tilts")]
    public Vector2 gestureInterval = new Vector2(2.8f, 5.5f);
    public float headTiltDegrees = 10f;

    public bool IsActive { get; set; } = true;

    Quaternion _spineRest;
    Quaternion _spine1Rest;
    Quaternion _headRest;
    bool _hasSpine;
    bool _hasSpine1;
    bool _hasHead;
    bool _cached;
    Coroutine _gestureRoutine;
    float _gestureUntil;
    float _tiltSign = 1f;

    void Awake()
    {
        ResolveTargets();
        CacheRest();
    }

    void OnEnable()
    {
        if (_gestureRoutine == null)
            _gestureRoutine = StartCoroutine(GestureLoop());
    }

    void OnDisable()
    {
        if (_gestureRoutine != null)
        {
            StopCoroutine(_gestureRoutine);
            _gestureRoutine = null;
        }
        RestoreRest();
    }

    public void ResolveTargets()
    {
        if (spine == null)
            spine = FindBone("Spine", "spine", "mixamorig:Spine");
        if (spine1 == null)
            spine1 = FindBone("Spine1", "spine1", "Chest", "chest", "mixamorig:Spine1");
        if (head == null)
            head = FindBone("Head", "head", "mixamorig:Head");

        // 没有独立脊柱时，不要用 Hips（会带动腿）
        if (spine == null && spine1 != null)
            spine = spine1;
    }

    void CacheRest()
    {
        _hasSpine = spine != null;
        _hasSpine1 = spine1 != null && spine1 != spine;
        _hasHead = head != null;

        if (_hasSpine)
            _spineRest = spine.localRotation;
        if (_hasSpine1)
            _spine1Rest = spine1.localRotation;
        if (_hasHead)
            _headRest = head.localRotation;

        _cached = _hasSpine || _hasSpine1 || _hasHead;
    }

    void LateUpdate()
    {
        if (!IsActive || !_cached)
            return;

        var t = Time.time;
        var breath = Mathf.Sin(t * breathSpeed) * breathDegrees;
        var sway = Mathf.Sin(t * swaySpeed) * swayDegrees;

        // 只旋转脊柱：前后微呼吸 + 左右轻晃，不影响腿
        if (_hasSpine)
            spine.localRotation = _spineRest * Quaternion.Euler(breath, 0f, sway);

        if (_hasSpine1)
            spine1.localRotation = _spine1Rest * Quaternion.Euler(breath * 0.6f, 0f, sway * 0.5f);

        if (_hasHead)
        {
            var gestureTilt = 0f;
            if (Time.time < _gestureUntil)
            {
                var remain = _gestureUntil - Time.time;
                var u = 1f - Mathf.Clamp01(remain / 0.9f);
                gestureTilt = headTiltDegrees * _tiltSign * Mathf.Sin(u * Mathf.PI);
            }
            head.localRotation = _headRest * Quaternion.Euler(breath * 0.25f, gestureTilt, sway * 0.25f);
        }
    }

    IEnumerator GestureLoop()
    {
        while (enabled)
        {
            yield return new WaitForSeconds(Random.Range(gestureInterval.x, gestureInterval.y));
            if (!IsActive)
                continue;

            // 只歪头，不做整身弹跳（避免脚离地）
            _tiltSign = Random.value > 0.5f ? 1f : -1f;
            _gestureUntil = Time.time + 0.9f;
        }
    }

    public void RestoreRest()
    {
        if (!_cached)
            return;
        if (_hasSpine)
            spine.localRotation = _spineRest;
        if (_hasSpine1)
            spine1.localRotation = _spine1Rest;
        if (_hasHead)
            head.localRotation = _headRest;
        _gestureUntil = 0f;
    }

    Transform FindBone(params string[] names)
    {
        var all = GetComponentsInChildren<Transform>(true);
        foreach (var n in names)
        {
            foreach (var tr in all)
            {
                if (string.Equals(tr.name, n, System.StringComparison.OrdinalIgnoreCase))
                    return tr;
            }
        }
        foreach (var n in names)
        {
            var key = n.ToLowerInvariant();
            foreach (var tr in all)
            {
                if (tr.name.ToLowerInvariant().Contains(key))
                    return tr;
            }
        }
        return null;
    }
}
