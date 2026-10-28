using UnityEngine;

/// <summary>
/// 场景启动时给指定角色挂上分区点击。避免手改 Prefab 实例。
/// </summary>
public class CharacterPartClickBootstrap : MonoBehaviour
{
    public string characterObjectName = "LittleGirl";

    void Awake()
    {
        var character = GameObject.Find(characterObjectName);
        if (character == null)
        {
            Debug.LogWarning($"[CharacterPartClick] 场景里找不到名为 {characterObjectName} 的物体。", this);
            return;
        }

        if (character.GetComponent<CharacterPartClickController>() == null)
            character.AddComponent<CharacterPartClickController>();
    }
}
