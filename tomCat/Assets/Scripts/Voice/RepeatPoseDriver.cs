using UnityEngine;

public enum PoseMode
{
    FollowIdle,
    Listen,
    Talk,
    Poke
}

/// <summary>
/// 听 / 说 / 被戳的占位动作。待机仍交给 TomCatIdleMotion。
/// 说话时用音量驱动下巴；没有下巴骨骼就轻轻点头。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class RepeatPoseDriver : MonoBehaviour
{
    public PoseMode Mode { get; private set; } = PoseMode.FollowIdle;
    public float MouthOpen;

    [Header("Pose (degrees)")]
    public float listenLean = 14f;
    public float jawOpenDegrees = 28f;
    public float pokeRecoilDegrees = 22f;

    Transform _spine;
    Transform _spine1;
    Transform _head;
    Transform _jaw;
    Quaternion _spineRest;
    Quaternion _spine1Rest;
    Quaternion _headRest;
    Quaternion _jawRest;
    bool _hasSpine;
    bool _hasSpine1;
    bool _hasHead;
    bool _hasJaw;
    bool _cached;
    float _modeStart;
    TomCatIdleMotion _idle;
    CharacterWavePlayer _wave;

    public void SetMode(PoseMode mode)
    {
        if (Mode != mode)
        {
            Mode = mode;
            _modeStart = Time.time;
        }

        SyncIdle();
    }

    public void Capture(TomCatIdleMotion idle)
    {
        _idle = idle;
        _wave = GetComponent<CharacterWavePlayer>();
        Resolve();
        if (_idle != null)
            _idle.RestoreRest();
        CacheRest();
    }

    void LateUpdate()
    {
        SyncIdle();
        if (Mode == PoseMode.FollowIdle || !_cached)
            return;

        switch (Mode)
        {
            case PoseMode.Listen:
                ApplyListen();
                break;
            case PoseMode.Talk:
                ApplyTalk();
                break;
            case PoseMode.Poke:
                ApplyPoke();
                break;
        }
    }

    void SyncIdle()
    {
        if (_idle == null)
            return;
        if (_wave != null && _wave.IsPlaying)
            return;
        _idle.IsActive = Mode == PoseMode.FollowIdle;
    }

    void ApplyListen()
    {
        var breathe = Mathf.Sin(Time.time * 3f) * 2f;
        if (_hasSpine)
            _spine.localRotation = _spineRest * Quaternion.Euler(listenLean + breathe, 0f, 0f);
        if (_hasSpine1)
            _spine1.localRotation = _spine1Rest * Quaternion.Euler((listenLean + breathe) * 0.45f, 0f, 0f);
        if (_hasHead)
            _head.localRotation = _headRest * Quaternion.Euler(-8f, 8f, 6f);
        if (_hasJaw)
            _jaw.localRotation = _jawRest;
    }

    void ApplyTalk()
    {
        var open = Mathf.Clamp01(MouthOpen);
        var flutter = Mathf.Abs(Mathf.Sin(Time.time * 16f));
        var shaped = Mathf.Clamp01(open * 0.65f + open * flutter * 0.55f);

        if (_hasJaw)
            _jaw.localRotation = _jawRest * Quaternion.Euler(jawOpenDegrees * shaped, 0f, 0f);
        else if (_hasHead)
            _head.localRotation = _headRest * Quaternion.Euler(10f * shaped, 0f, 0f);

        if (_hasHead && _hasJaw)
            _head.localRotation = _headRest * Quaternion.Euler(-3f * shaped, 0f, 0f);

        if (_hasSpine)
            _spine.localRotation = _spineRest * Quaternion.Euler(3f * shaped, 0f, 0f);
        if (_hasSpine1)
            _spine1.localRotation = _spine1Rest * Quaternion.Euler(2f * shaped, 0f, 0f);
    }

    void ApplyPoke()
    {
        var u = Time.time - _modeStart;
        float recoil;
        if (u < 0.08f)
            recoil = u / 0.08f;
        else if (u < 0.32f)
            recoil = 1f;
        else
            recoil = 1f - Mathf.Clamp01((u - 0.32f) / 0.38f);

        if (_hasSpine)
            _spine.localRotation = _spineRest * Quaternion.Euler(-pokeRecoilDegrees * recoil, 0f, 7f * recoil);
        if (_hasSpine1)
            _spine1.localRotation = _spine1Rest * Quaternion.Euler(-pokeRecoilDegrees * 0.6f * recoil, 0f, 4f * recoil);
        if (_hasHead)
            _head.localRotation = _headRest * Quaternion.Euler(12f * recoil, 0f, -8f * recoil);
        if (_hasJaw)
            _jaw.localRotation = _jawRest;
    }

    void Resolve()
    {
        if (_spine == null)
            _spine = FindBone("Spine", "spine", "mixamorig:Spine");
        if (_spine1 == null)
            _spine1 = FindBone("Spine1", "spine1", "Chest", "chest", "mixamorig:Spine1");
        if (_head == null)
            _head = FindBone("Head", "head", "mixamorig:Head");
        if (_jaw == null)
            _jaw = FindBone("jaw", "Jaw", "mixamorig:Jaw", "chin");

        if (_spine == null && _spine1 != null)
            _spine = _spine1;
    }

    void CacheRest()
    {
        _hasSpine = _spine != null;
        _hasSpine1 = _spine1 != null && _spine1 != _spine;
        _hasHead = _head != null;
        _hasJaw = _jaw != null;
        if (_hasSpine)
            _spineRest = _spine.localRotation;
        if (_hasSpine1)
            _spine1Rest = _spine1.localRotation;
        if (_hasHead)
            _headRest = _head.localRotation;
        if (_hasJaw)
            _jawRest = _jaw.localRotation;
        _cached = _hasSpine || _hasHead || _hasJaw;
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
