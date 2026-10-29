#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 把 Tom 客厅烘进当前场景，编辑模式 Hierarchy / Scene 视图也能直接看见。
/// 菜单：TomCat → Bake Tom Room Into Scene
/// </summary>
public static class BakeTomRoomIntoScene
{
    [MenuItem("TomCat/Bake Tom Room Into Scene")]
    public static void Bake()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Bake Room", "请先打开 SampleScene。", "OK");
            return;
        }

        // 旧房间清掉重建
        var oldRooms = Object.FindObjectsOfType<SimpleRoomBuilder>();
        foreach (var r in oldRooms)
            Object.DestroyImmediate(r.gameObject);

        var roomGo = new GameObject("Room_TomLiving");
        var builder = roomGo.AddComponent<SimpleRoomBuilder>();
        // Awake 在编辑器 AddComponent 时不一定按 Play 逻辑走，手动重建
        builder.Rebuild();

        var oldDirectors = Object.FindObjectsOfType<CharacterStageDirector>();
        foreach (var d in oldDirectors)
            Object.DestroyImmediate(d.gameObject);

        var directorGo = new GameObject("CharacterStageDirector");
        var director = directorGo.AddComponent<CharacterStageDirector>();
        director.characterObjectName = "Robot";

        // 隐藏旧 Ground
        var ground = GameObject.Find("Ground");
        if (ground != null)
            ground.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = roomGo;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();

        EditorUtility.DisplayDialog(
            "Bake Room",
            "已写入当前场景：\n• Room_TomLiving（客厅家具）\n• CharacterStageDirector（进场逻辑）\n\n" +
            "现在不按 Play，在 Scene 视图也能看到房子。\n" +
            "贴地/相机/挥手仍要按 Play 才会自动执行。",
            "OK");
    }
}
#endif
