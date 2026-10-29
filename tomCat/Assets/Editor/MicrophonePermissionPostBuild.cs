#if UNITY_EDITOR && UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// 打包时补上录音权限。Unity 主清单里没有时才写入。
/// </summary>
public class MicrophonePermissionPostBuild : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 0;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning("[Repeat] 找不到 AndroidManifest，未写入 RECORD_AUDIO: " + manifestPath);
            return;
        }

        var text = File.ReadAllText(manifestPath);
        if (text.Contains("android.permission.RECORD_AUDIO"))
            return;

        const string perm = "    <uses-permission android:name=\"android.permission.RECORD_AUDIO\" />\n";
        var insertAt = text.IndexOf("<application", System.StringComparison.Ordinal);
        if (insertAt < 0)
        {
            Debug.LogWarning("[Repeat] AndroidManifest 没有 application 节点");
            return;
        }

        File.WriteAllText(manifestPath, text.Insert(insertAt, perm));
        Debug.Log("[Repeat] 已写入 RECORD_AUDIO");
    }
}
#endif
