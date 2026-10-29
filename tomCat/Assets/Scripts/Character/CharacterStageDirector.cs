using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 手机竖屏 Tom 构图：角色居中全身、窗在脑后、两侧家具入画、轻微仰视。
/// </summary>
[DisallowMultipleComponent]
public class CharacterStageDirector : MonoBehaviour
{
    public string characterObjectName = "Robot";
    public float introDelay = 0.25f;

    [Header("Scale & grounding")]
    public float targetCharacterHeight = 1.28f;
    public float floorY = 0f;
    public float footEmbed = 0.04f;
    public float characterStandZ = 0.05f;

    [Header("Mobile portrait camera (Tom-like)")]
    [Tooltip("角色约占画面高度的比例；略小一点好露出地面和两侧家具")]
    [Range(0.40f, 0.75f)]
    public float characterScreenHeightRatio = 0.52f;
    public float lookAtHeightRatio = 0.40f;
    public float cameraBelowLookAt = 0.18f;
    public float fieldOfView = 48f;
    public float cameraSideOffset = 0f;
    public float minDistance = 2.4f;
    public float maxDistance = 5.5f;

    SimpleRoomBuilder _room;
    Transform _character;
    Camera _cam;
    CharacterWavePlayer _wave;
    TomCatIdleMotion _idle;
    GameObject _blobShadow;

    void Awake()
    {
        ApplyPortraitOrientation();
        EnsureRoom();
        HideLegacyGround();
        SetupKeyLight();
    }

    void Start()
    {
        StartCoroutine(RunIntro());
    }

    static void ApplyPortraitOrientation()
    {
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
    }

    IEnumerator RunIntro()
    {
        yield return null;
        // 等一帧让竖屏分辨率生效，再算构图
        yield return null;

        _character = FindCharacter();
        if (_character == null)
        {
            Debug.LogWarning("[Stage] 找不到角色 " + characterObjectName, this);
            yield break;
        }

        FaceCharacterToCamera(_character);
        _character.position = new Vector3(0f, _character.position.y, characterStandZ);
        FitCharacterToRoom(_character);
        PlaceFeetOnFloor(_character);
        EnableCharacterShadows(_character);
        CreateBlobShadow(_character);
        SetupCharacterComponents(_character);
        FrameCameraPortrait(_character);
        PlaceFeetOnFloor(_character);
        FrameCameraPortrait(_character);

        if (_idle != null)
            _idle.IsActive = false;

        if (introDelay > 0f)
            yield return new WaitForSeconds(introDelay);

        if (_wave != null)
        {
            _wave.PlayWave();
            yield return null;
            var guard = 0f;
            while (_wave.IsPlaying && guard < _wave.duration + 1f)
            {
                guard += Time.deltaTime;
                yield return null;
            }
        }

        if (_idle != null)
        {
            _idle.ResolveTargets();
            _idle.IsActive = true;
        }
    }

    void LateUpdate()
    {
        if (_blobShadow == null || _character == null)
            return;
        var bounds = CalculateBounds(_character.gameObject);
        _blobShadow.transform.position = new Vector3(bounds.center.x, floorY + 0.008f, characterStandZ);
        var footprint = Mathf.Clamp(bounds.size.x * 0.55f, 0.35f, 0.9f);
        _blobShadow.transform.localScale = new Vector3(footprint, 0.008f, footprint * 0.65f);
    }

    void EnsureRoom()
    {
        var existing = FindObjectOfType<SimpleRoomBuilder>();
        if (existing != null)
        {
            // SampleScene 已烘焙 Room_TomLiving：保留，勿 Rebuild（会清空）
            _room = existing;
            if (existing.transform.childCount == 0)
                existing.BuildIfNeeded();
            return;
        }

        var go = new GameObject("Room_TomLiving");
        _room = go.AddComponent<SimpleRoomBuilder>();
        _room.BuildIfNeeded();
    }

    static void HideLegacyGround()
    {
        var ground = GameObject.Find("Ground");
        if (ground != null)
            ground.SetActive(false);
    }

    static void SetupKeyLight()
    {
        var lightGo = GameObject.Find("Directional Light");
        if (lightGo == null)
            return;
        var light = lightGo.GetComponent<Light>();
        if (light == null)
            return;

        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.97f, 0.90f);
        // 略从窗侧打光，像 Tom 客厅
        lightGo.transform.rotation = Quaternion.Euler(38f, -20f, 0f);
    }

    Transform FindCharacter()
    {
        var go = GameObject.Find(characterObjectName);
        if (go == null)
            go = GameObject.Find("LittleGirl");
        return go != null ? go.transform : null;
    }

    void SetupCharacterComponents(Transform character)
    {
        foreach (var anim in character.GetComponentsInChildren<Animator>(true))
            anim.enabled = false;

        if (character.GetComponent<CharacterPartClickController>() == null)
            character.gameObject.AddComponent<CharacterPartClickController>();

        _wave = character.GetComponent<CharacterWavePlayer>();
        if (_wave == null)
            _wave = character.gameObject.AddComponent<CharacterWavePlayer>();
        _wave.disableAnimatorWhileWaving = true;

        _idle = character.GetComponent<TomCatIdleMotion>();
        if (_idle == null)
            _idle = character.gameObject.AddComponent<TomCatIdleMotion>();
        _idle.ResolveTargets();
    }

    static void FaceCharacterToCamera(Transform character)
    {
        character.rotation = Quaternion.Euler(0f, 180f, 0f);
    }

    void FitCharacterToRoom(Transform character)
    {
        var bounds = CalculateBounds(character.gameObject);
        var height = Mathf.Max(bounds.size.y, 0.01f);
        if (height < 0.05f)
            return;

        var scale = Mathf.Clamp(targetCharacterHeight / height, 0.05f, 20f);
        character.localScale *= scale;
    }

    void PlaceFeetOnFloor(Transform character)
    {
        var footY = GetLowestFootWorldY(character);
        if (float.IsPositiveInfinity(footY))
            footY = CalculateBounds(character.gameObject).min.y;

        var deltaY = floorY - footEmbed - footY;
        character.position += new Vector3(0f, deltaY, 0f);
    }

    static float GetLowestFootWorldY(Transform character)
    {
        var lowest = float.PositiveInfinity;
        foreach (var t in character.GetComponentsInChildren<Transform>(true))
        {
            var n = t.name.ToLowerInvariant();
            if (!(n.Contains("foot") || n.Contains("toe") || n.Contains("ankle")))
                continue;
            if (t.position.y < lowest)
                lowest = t.position.y;
        }
        return lowest;
    }

    static void EnableCharacterShadows(Transform character)
    {
        foreach (var r in character.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
        }
    }

    void CreateBlobShadow(Transform character)
    {
        if (_blobShadow != null)
            Destroy(_blobShadow);

        _blobShadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _blobShadow.name = "FootBlobShadow";
        Object.Destroy(_blobShadow.GetComponent<Collider>());

        var renderer = _blobShadow.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Standard")
                     ?? Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");
        if (shader == null)
            return;
        var mat = new Material(shader);
        var c = new Color(0.1f, 0.08f, 0.06f, 0.5f);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", c);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    void FrameCameraPortrait(Transform character)
    {
        _cam = Camera.main;
        if (_cam == null)
            _cam = FindObjectOfType<Camera>();
        if (_cam == null)
            return;

        var bounds = CalculateBounds(character.gameObject);
        var height = Mathf.Max(bounds.size.y, 0.5f);
        var feetY = bounds.min.y;
        var lookAt = new Vector3(
            0f,
            feetY + height * lookAtHeightRatio,
            characterStandZ);

        // 按竖屏垂直 FOV：让角色高度约占画面 characterScreenHeightRatio
        _cam.fieldOfView = fieldOfView;
        var halfFovRad = fieldOfView * 0.5f * Mathf.Deg2Rad;
        var visibleHeight = height / Mathf.Clamp(characterScreenHeightRatio, 0.35f, 0.85f);
        var distance = (visibleHeight * 0.5f) / Mathf.Tan(halfFovRad);
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // Tom：镜头略低于注视点，轻微仰视，水平居中
        var camPos = new Vector3(
            lookAt.x + cameraSideOffset,
            lookAt.y - cameraBelowLookAt,
            lookAt.z - distance);

        // 脚附近要留一点地面：若算出来相机过低导致脚贴边，略抬高
        var minCamY = feetY + 0.15f;
        if (camPos.y < minCamY)
            camPos.y = minCamY;

        _cam.transform.position = camPos;
        _cam.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
        _cam.clearFlags = CameraClearFlags.SolidColor;
        _cam.backgroundColor = wallYellowFallback();
        _cam.nearClipPlane = 0.05f;
        _cam.farClipPlane = 40f;
    }

    static Color wallYellowFallback()
    {
        return new Color(0.97f, 0.89f, 0.58f);
    }

    static Bounds CalculateBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return new Bounds(go.transform.position + Vector3.up, Vector3.one * 2f);

        var b = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
