using UnityEngine;

/// <summary>挂在碰撞体上，标记这是头还是脚。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class BodyPartHitbox : MonoBehaviour
{
    public BodyPartType part = BodyPartType.Head;
}
