using UnityEngine;

/// <summary>
/// 场景启动时给指定角色挂上分区点击。避免手改 Prefab 实例。
/// </summary>
public class CharacterPartClickBootstrap : MonoBehaviour
{
    public string characterObjectName = "Robot";

    void Awake()
    {
        var character = GameObject.Find(characterObjectName);
        if (character == null)
            character = GameObject.Find("LittleGirl");

        if (character == null)
        {
            Debug.LogWarning($"[CharacterPartClick] 场景里找不到角色（期望名：{characterObjectName}）。", this);
            return;
        }

        if (character.GetComponent<CharacterPartClickController>() == null)
            character.AddComponent<CharacterPartClickController>();
    }
}
