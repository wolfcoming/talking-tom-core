using UnityEngine;

/// <summary>
/// My Talking Tom 手机竖屏客厅：角色居中，窗在身后，椅/电视在两侧入画。
/// 地板顶面 y=0。适合竖屏 FOV 构图。
/// </summary>
[DisallowMultipleComponent]
public class SimpleRoomBuilder : MonoBehaviour
{
    // 竖屏：房间略窄略浅，家具靠两侧，保证手机画面里能看到
    public Vector3 roomSize = new Vector3(5.2f, 2.85f, 4.2f);

    public Color wallYellow = new Color(0.97f, 0.89f, 0.58f);
    public Color wallYellowDeep = new Color(0.94f, 0.84f, 0.50f);
    public Color floorWood = new Color(0.74f, 0.54f, 0.34f);
    public Color floorWoodDark = new Color(0.60f, 0.42f, 0.26f);
    public Color ceilingWhite = new Color(0.98f, 0.96f, 0.90f);
    public Color trimWhite = new Color(0.99f, 0.97f, 0.93f);
    public Color skyBlue = new Color(0.52f, 0.76f, 0.94f);
    public Color curtainColor = new Color(0.93f, 0.48f, 0.28f);
    public Color armchairColor = new Color(0.88f, 0.26f, 0.16f);
    public Color armchairDark = new Color(0.72f, 0.16f, 0.10f);
    public Color tvBody = new Color(0.22f, 0.48f, 0.30f);
    public Color tvScreen = new Color(0.12f, 0.16f, 0.20f);
    public Color lampShade = new Color(0.99f, 0.93f, 0.72f);
    public Color lampPole = new Color(0.78f, 0.78f, 0.75f);
    public Color rugColor = new Color(0.40f, 0.60f, 0.38f);
    public Color frameWood = new Color(0.55f, 0.38f, 0.22f);
    public Color pictureColor = new Color(0.50f, 0.68f, 0.82f);
    public Color plantPot = new Color(0.80f, 0.55f, 0.35f);
    public Color plantLeaf = new Color(0.32f, 0.56f, 0.30f);

    bool _built;

    void Awake()
    {
        // 场景里已烘焙好的房间直接用，不要运行时 Destroy+重建
        // （Destroy 是延迟的，同帧 childCount 仍 > 0，会导致建不出来、房间变空）
        if (transform.childCount > 0)
        {
            _built = true;
            return;
        }

        BuildIfNeeded();
    }

    [ContextMenu("Rebuild Room")]
    public void Rebuild()
    {
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            // 必须立即删掉，否则同帧 BuildIfNeeded 会误判「已有子物体」而跳过建造
            DestroyImmediate(child);
        }
        _built = false;
        BuildIfNeeded();
    }

    public void BuildIfNeeded()
    {
        if (_built || transform.childCount > 0)
        {
            _built = true;
            return;
        }

        var w = roomSize.x;
        var h = roomSize.y;
        var d = roomSize.z;
        var back = d * 0.5f;
        var left = -w * 0.5f;
        var right = w * 0.5f;

        // 结构
        Box("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(w, 0.1f, d), floorWood, 0.05f, 0.2f);
        Box("Floor_BackBand", new Vector3(0f, 0.001f, back * 0.28f), new Vector3(w - 0.15f, 0.01f, d * 0.42f), floorWoodDark, 0.02f, 0.18f);
        Box("Ceiling", new Vector3(0f, h + 0.04f, 0f), new Vector3(w, 0.08f, d), ceilingWhite, 0f, 0.5f);
        Box("Wall_Back", new Vector3(0f, h * 0.5f, back), new Vector3(w, h, 0.1f), wallYellowDeep, 0f, 0.45f);
        Box("Wall_Left", new Vector3(left, h * 0.5f, 0f), new Vector3(0.1f, h, d), wallYellow, 0f, 0.45f);
        Box("Wall_Right", new Vector3(right, h * 0.5f, 0f), new Vector3(0.1f, h, d), wallYellow, 0f, 0.45f);

        Box("Base_Back", new Vector3(0f, 0.07f, back - 0.07f), new Vector3(w - 0.25f, 0.14f, 0.05f), trimWhite, 0f, 0.4f);
        Box("Base_Left", new Vector3(left + 0.07f, 0.07f, 0f), new Vector3(0.05f, 0.14f, d - 0.2f), trimWhite, 0f, 0.4f);
        Box("Base_Right", new Vector3(right - 0.07f, 0.07f, 0f), new Vector3(0.05f, 0.14f, d - 0.2f), trimWhite, 0f, 0.4f);

        // 大窗居中：竖屏里正好在角色脑后上方
        var winY = 1.45f;
        var winZ = back - 0.08f;
        Box("Sky", new Vector3(0f, winY, winZ + 0.07f), new Vector3(2.35f, 1.45f, 0.02f), skyBlue, 0f, 0.9f);
        Box("Window_Frame", new Vector3(0f, winY, winZ), new Vector3(2.55f, 1.65f, 0.09f), trimWhite, 0f, 0.4f);
        Box("Window_Glass", new Vector3(0f, winY, winZ - 0.03f), new Vector3(2.2f, 1.3f, 0.03f), new Color(0.78f, 0.90f, 0.97f), 0f, 0.85f);
        Box("Window_Bar_H", new Vector3(0f, winY, winZ - 0.02f), new Vector3(2.2f, 0.06f, 0.04f), trimWhite, 0f, 0.4f);
        Box("Window_Bar_V", new Vector3(0f, winY, winZ - 0.02f), new Vector3(0.06f, 1.3f, 0.04f), trimWhite, 0f, 0.4f);
        Box("Curtain_L", new Vector3(-1.4f, winY - 0.05f, winZ - 0.07f), new Vector3(0.38f, 1.75f, 0.07f), curtainColor, 0f, 0.25f);
        Box("Curtain_R", new Vector3(1.4f, winY - 0.05f, winZ - 0.07f), new Vector3(0.38f, 1.75f, 0.07f), curtainColor, 0f, 0.25f);

        // 地毯：角色站立区（居中）
        Box("Rug", new Vector3(0f, 0.01f, 0.05f), new Vector3(2.2f, 0.02f, 1.7f), rugColor, 0f, 0.15f);

        // 左侧扶手椅（竖屏边缘能扫到）
        BuildArmchair(new Vector3(-1.55f, 0f, 0.75f));

        // 右侧电视
        Box("TV_Stand", new Vector3(1.55f, 0.2f, 0.85f), new Vector3(0.95f, 0.4f, 0.48f), floorWoodDark, 0.05f, 0.25f);
        Box("TV_Body", new Vector3(1.55f, 0.68f, 0.85f), new Vector3(0.85f, 0.62f, 0.4f), tvBody, 0.1f, 0.35f);
        Box("TV_Screen", new Vector3(1.55f, 0.7f, 0.63f), new Vector3(0.68f, 0.45f, 0.04f), tvScreen, 0f, 0.7f);
        Box("TV_Antenna_L", new Vector3(1.4f, 1.12f, 0.85f), new Vector3(0.035f, 0.4f, 0.035f), lampPole, 0.3f, 0.5f);
        Box("TV_Antenna_R", new Vector3(1.7f, 1.12f, 0.85f), new Vector3(0.035f, 0.4f, 0.035f), lampPole, 0.3f, 0.5f);

        // 落地灯（右后，不挡角色）
        Box("Lamp_Base", new Vector3(1.15f, 0.05f, 1.35f), new Vector3(0.28f, 0.07f, 0.28f), floorWoodDark, 0.05f, 0.3f);
        Box("Lamp_Pole", new Vector3(1.15f, 0.8f, 1.35f), new Vector3(0.06f, 1.45f, 0.06f), lampPole, 0.4f, 0.55f);
        Sphere("Lamp_Shade", new Vector3(1.15f, 1.65f, 1.35f), 0.48f, lampShade, 0f, 0.35f);

        // 相框偏左墙
        Box("Frame", new Vector3(-1.55f, 1.75f, back - 0.09f), new Vector3(0.7f, 0.58f, 0.05f), frameWood, 0.05f, 0.3f);
        Box("Picture", new Vector3(-1.55f, 1.75f, back - 0.12f), new Vector3(0.55f, 0.42f, 0.02f), pictureColor, 0f, 0.4f);

        // 窗边植物
        Box("PlantPot", new Vector3(1.7f, 0.18f, back - 0.7f), new Vector3(0.32f, 0.36f, 0.32f), plantPot, 0.02f, 0.3f);
        Sphere("PlantLeaf", new Vector3(1.7f, 0.5f, back - 0.7f), 0.4f, plantLeaf, 0f, 0.3f);

        Box("SideTable", new Vector3(-0.85f, 0.26f, 0.55f), new Vector3(0.45f, 0.07f, 0.45f), floorWoodDark, 0.05f, 0.28f);
        Box("SideTable_Leg", new Vector3(-0.85f, 0.13f, 0.55f), new Vector3(0.1f, 0.26f, 0.1f), floorWoodDark, 0.05f, 0.28f);

        _built = true;
    }

    void BuildArmchair(Vector3 origin)
    {
        Box("Chair_Seat", origin + new Vector3(0f, 0.34f, 0f), new Vector3(0.95f, 0.24f, 0.85f), armchairColor, 0f, 0.2f);
        Box("Chair_Back", origin + new Vector3(0f, 0.75f, 0.32f), new Vector3(0.95f, 0.72f, 0.18f), armchairColor, 0f, 0.2f);
        Box("Chair_Arm_L", origin + new Vector3(-0.46f, 0.48f, 0f), new Vector3(0.15f, 0.38f, 0.85f), armchairDark, 0f, 0.2f);
        Box("Chair_Arm_R", origin + new Vector3(0.46f, 0.48f, 0f), new Vector3(0.15f, 0.38f, 0.85f), armchairDark, 0f, 0.2f);
        Box("Chair_Base", origin + new Vector3(0f, 0.1f, 0f), new Vector3(0.88f, 0.16f, 0.75f), armchairDark, 0f, 0.2f);
    }

    void Box(string name, Vector3 pos, Vector3 scale, Color color, float metallic, float smoothness)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Finish(go, name, pos, scale, color, metallic, smoothness);
    }

    void Sphere(string name, Vector3 pos, float diameter, Color color, float metallic, float smoothness)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Finish(go, name, pos, Vector3.one * diameter, color, metallic, smoothness);
    }

    void Finish(GameObject go, string name, Vector3 pos, Vector3 scale, Color color, float metallic, float smoothness)
    {
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;

        var renderer = go.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;

        var shader = Shader.Find("Standard")
                     ?? Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Mobile/Diffuse")
                     ?? Shader.Find("Diffuse")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("[Room] 找不到可用 Shader，跳过材质: " + name, go);
            return;
        }
        var mat = new Material(shader);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", smoothness);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", smoothness);
        renderer.sharedMaterial = mat;
    }
}
