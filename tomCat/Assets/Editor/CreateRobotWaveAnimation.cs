#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 检查 Robot 挥手所需骨骼，并确保挂上 CharacterWavePlayer。
/// 菜单：TomCat → Setup Robot Wave Player
/// </summary>
public static class CreateRobotWaveAnimation
{
    [MenuItem("TomCat/Setup Robot Wave Player")]
    public static void Setup()
    {
        var robot = GameObject.Find("Robot");
        if (robot == null)
        {
            EditorUtility.DisplayDialog("Wave Setup", "场景里找不到 Robot。请先打开 SampleScene。", "OK");
            return;
        }

        var arm = FindBone(robot.transform, "RightArm");
        var fore = FindBone(robot.transform, "RightForeArm");
        var hand = FindBone(robot.transform, "RightHand");

        var click = robot.GetComponent<CharacterPartClickController>();
        if (click == null)
            click = robot.AddComponent<CharacterPartClickController>();

        var wave = robot.GetComponent<CharacterWavePlayer>();
        if (wave == null)
            wave = robot.AddComponent<CharacterWavePlayer>();

        wave.rightArm = arm;
        wave.rightForeArm = fore;
        wave.rightHand = hand;
        wave.AutoFindBones();

        EditorUtility.SetDirty(robot);

        var msg =
            "已挂上 CharacterWavePlayer。\n\n" +
            "RightArm: " + NameOf(arm) + "\n" +
            "RightForeArm: " + NameOf(fore) + "\n" +
            "RightHand: " + NameOf(hand) + "\n\n" +
            "Play 后点击头部应挥手。\n" +
            "若方向不对：选中 Robot，在 Inspector 里调 Arm Raise Euler / Forearm Swing Euler。";
        EditorUtility.DisplayDialog("Wave Setup", msg, "OK");
    }

    static string NameOf(Transform t) => t == null ? "(未找到)" : t.name;

    static Transform FindBone(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return null;
    }
}
#endif
