using UnityEngine;

[CreateAssetMenu(fileName = "PortalData", menuName = "VR/Portal Data")]
public class PortalData : ScriptableObject
{
    [Header("Portal Identity")]
    public string portalID;
    public string displayName;
    
    [Header("Destination")]
    public string targetSceneName;
    
    [Header("Return Settings")]
    public Vector3 returnPosition;
    public float returnRotationY;
    
    [Header("Visual")]
    public Color glowColor = Color.cyan;
}
