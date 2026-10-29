using UnityEngine;

/// <summary>
/// 场景启动时挂上点击、挥手，并启动舞台导演（房间/相机/开场挥手/待机）。
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
        }
        else
        {
            if (character.GetComponent<CharacterPartClickController>() == null)
                character.AddComponent<CharacterPartClickController>();

            if (character.GetComponent<CharacterWavePlayer>() == null)
                character.AddComponent<CharacterWavePlayer>();

            if (character.GetComponent<TomCatIdleMotion>() == null)
                character.AddComponent<TomCatIdleMotion>();
        }

        var director = FindObjectOfType<CharacterStageDirector>();
        if (director == null)
        {
            var go = new GameObject("CharacterStageDirector");
            director = go.AddComponent<CharacterStageDirector>();
        }
        director.characterObjectName = characterObjectName;
    }
}
