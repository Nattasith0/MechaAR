using UnityEngine;

[CreateAssetMenu(
    fileName = "Equipment",
    menuName = "MechaAR/Equipment Data"
)]
public sealed class EquipmentData : ScriptableObject
{
    [Header("Equipment Identity")]

    public string equipmentId;
    public string referenceImageName;
    public string equipmentName;
    public string category;
    public string thaiName;

    [Header("Equipment Information")]

    [TextArea(3, 8)]
    public string description;

    [TextArea(3, 8)]
    public string workingPrinciple;

    public string[] applications;

    [Header("AR Model")]

    // โมเดล 3D ที่จะแสดงเมื่อสแกนอุปกรณ์นี้
    public GameObject modelPrefab;
}