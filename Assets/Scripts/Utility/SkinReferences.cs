using UnityEngine;

public class SkinReferences : MonoBehaviour
{
    [Tooltip("The Animator component on this specific 3D model")]
    public Animator skinAnimator;

    [Tooltip("The empty GameObject inside this character's right hand")]
    public Transform weaponSocket;
}