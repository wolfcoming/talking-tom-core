using System.Collections;
using System.Text;
using UnityEngine;

/// <summary>
/// 关键帧式驱动右臂挥手。点头时由 CharacterPartClickController 直接调用。
/// </summary>
[DisallowMultipleComponent]
public class CharacterWavePlayer : MonoBehaviour
{
    [Header("Bones (auto-found if empty)")]
    public Transform rightArm;
    public Transform rightForeArm;
    public Transform rightHand;

    [Header("Wave timing")]
    public float duration = 1.2f;
    public int swingCount = 3;

    [Header("Pose offsets (degrees, relative to rest)")]
    public Vector3 armRaiseEuler = new Vector3(0f, 0f, -80f);
    public Vector3 forearmSwingEuler = new Vector3(0f, 0f, 60f);
    public Vector3 handSwingEuler = new Vector3(0f, 0f, 30f);

    [Header("Conflict fix")]
    [Tooltip("FBX 自带 Animator 会每帧覆盖骨骼，挥手时先关掉它")]
    public bool disableAnimatorWhileWaving = true;

    [Header("Debug")]
    public bool logPlay = true;

    Quaternion _armRest;
    Quaternion _foreRest;
    Quaternion _handRest;
    bool _restCached;
    Coroutine _routine;
    Animator[] _animators;
    bool[] _animatorWasEnabled;

    public string LastStatus { get; private set; } = "";
    public bool IsPlaying => _routine != null;

    void Awake()
    {
        AutoFindBones();
        CacheRestPose();
        _animators = GetComponentsInChildren<Animator>(true);
    }

    [ContextMenu("Auto Find Bones")]
    public void AutoFindBones()
    {
        if (rightArm == null)
            rightArm = FindBone("RightArm", "rightarm", "R_Arm", "r_arm", "UpperArm_R", "mixamorig:RightArm");
        if (rightForeArm == null)
            rightForeArm = FindBone("RightForeArm", "rightforearm", "R_ForeArm", "LowerArm_R", "mixamorig:RightForeArm");
        if (rightHand == null)
            rightHand = FindBone("RightHand", "righthand", "R_Hand", "Hand_R", "mixamorig:RightHand");

        // 仍找不到时：用肩膀下第一个子节点当手臂
        if (rightArm == null)
        {
            var shoulder = FindBone("RightShoulder", "rightshoulder", "mixamorig:RightShoulder");
            if (shoulder != null && shoulder.childCount > 0)
                rightArm = shoulder.GetChild(0);
        }
    }

    [ContextMenu("Play Wave Now")]
    public void PlayWave()
    {
        if (!isActiveAndEnabled)
            return;

        AutoFindBones();
        if (rightArm == null && rightForeArm == null)
        {
            LastStatus = "挥手失败：找不到手臂骨骼";
            Debug.LogWarning("[CharacterWave] " + LastStatus + "\n" + DumpBoneNames(), this);
            return;
        }

        if (!_restCached)
            CacheRestPose();

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(WaveRoutine());
        LastStatus = "挥手中… (" + BoneLabel() + ")";
        if (logPlay)
            Debug.Log("[CharacterWave] Play " + BoneLabel(), this);
    }

    void CacheRestPose()
    {
        if (rightArm != null)
            _armRest = rightArm.localRotation;
        if (rightForeArm != null)
            _foreRest = rightForeArm.localRotation;
        if (rightHand != null)
            _handRest = rightHand.localRotation;
        _restCached = rightArm != null || rightForeArm != null;
    }

    IEnumerator WaveRoutine()
    {
        SetAnimatorsEnabled(false);

        var t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            var u = Mathf.Clamp01(t / duration);
            var raise = Mathf.Sin(u * Mathf.PI);
            var swing = Mathf.Sin(u * Mathf.PI * (swingCount * 2f)) * raise;

            if (rightArm != null)
                rightArm.localRotation = _armRest * Quaternion.Euler(armRaiseEuler * raise);
            if (rightForeArm != null)
                rightForeArm.localRotation = _foreRest * Quaternion.Euler(forearmSwingEuler * swing);
            if (rightHand != null)
                rightHand.localRotation = _handRest * Quaternion.Euler(handSwingEuler * swing);

            yield return null;
        }

        RestoreRestPose();
        SetAnimatorsEnabled(true);
        _routine = null;
        LastStatus = "挥手结束";
    }

    void SetAnimatorsEnabled(bool enabled)
    {
        if (!disableAnimatorWhileWaving)
            return;
        if (_animators == null)
            _animators = GetComponentsInChildren<Animator>(true);

        if (!enabled)
        {
            _animatorWasEnabled = new bool[_animators.Length];
            for (var i = 0; i < _animators.Length; i++)
            {
                if (_animators[i] == null)
                    continue;
                _animatorWasEnabled[i] = _animators[i].enabled;
                _animators[i].enabled = false;
            }
        }
        else if (_animatorWasEnabled != null)
        {
            for (var i = 0; i < _animators.Length; i++)
            {
                if (_animators[i] == null)
                    continue;
                _animators[i].enabled = _animatorWasEnabled[i];
            }
        }
    }

    void RestoreRestPose()
    {
        if (rightArm != null)
            rightArm.localRotation = _armRest;
        if (rightForeArm != null)
            rightForeArm.localRotation = _foreRest;
        if (rightHand != null)
            rightHand.localRotation = _handRest;
    }

    string BoneLabel()
    {
        return "arm=" + (rightArm ? rightArm.name : "-")
               + " fore=" + (rightForeArm ? rightForeArm.name : "-")
               + " hand=" + (rightHand ? rightHand.name : "-");
    }

    string DumpBoneNames()
    {
        var sb = new StringBuilder();
        foreach (var tr in GetComponentsInChildren<Transform>(true))
        {
            var n = tr.name.ToLowerInvariant();
            if (n.Contains("arm") || n.Contains("hand") || n.Contains("shoulder") || n.Contains("腕") || n.Contains("手"))
                sb.AppendLine(tr.name);
        }
        return sb.Length > 0 ? sb.ToString() : "(没有名字里带 arm/hand 的节点)";
    }

    Transform FindBone(params string[] candidates)
    {
        var transforms = GetComponentsInChildren<Transform>(true);
        foreach (var c in candidates)
        {
            foreach (var tr in transforms)
            {
                if (string.Equals(tr.name, c, System.StringComparison.OrdinalIgnoreCase))
                    return tr;
            }
        }

        foreach (var c in candidates)
        {
            var key = c.Replace("mixamorig:", "").ToLowerInvariant();
            foreach (var tr in transforms)
            {
                if (tr.name.ToLowerInvariant().Contains(key))
                    return tr;
            }
        }

        return null;
    }
}
