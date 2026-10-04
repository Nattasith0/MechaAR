
using UnityEngine;

public class EquipmentDatabase : MonoBehaviour
{
    [Header("Equipment Catalog")]

    // รายการข้อมูลอุปกรณ์ทั้งหมด
    [SerializeField]
    private EquipmentData[] equipments;

    // UI reads the same catalog used by tracking; callers cannot replace entries.
    public System.Collections.Generic.IReadOnlyList<EquipmentData> Equipments =>
        System.Array.AsReadOnly(equipments ?? System.Array.Empty<EquipmentData>());


    // ค้นหาอุปกรณ์จากชื่อภาพ AR
    public EquipmentData GetByImageName(string imageName)
    {
        // ตรวจสอบว่ามีข้อมูลหรือไม่
        if (equipments == null)
        {
            return null;
        }

        // ค้นหาข้อมูลอุปกรณ์ทีละรายการ
        foreach (EquipmentData equipment in equipments)
        {
            if (equipment == null)
            {
                continue;
            }

            // หากชื่อภาพตรงกัน ส่งข้อมูลอุปกรณ์กลับไป
            if (equipment.referenceImageName == imageName)
            {
                return equipment;
            }
        }

        // หากไม่พบอุปกรณ์
        Debug.LogWarning(
            "ไม่พบข้อมูลอุปกรณ์: " + imageName
        );

        return null;
    }


    // ค้นหาอุปกรณ์จาก Equipment ID
    public EquipmentData GetById(string id)
    {
        if (equipments == null)
        {
            return null;
        }

        foreach (EquipmentData equipment in equipments)
        {
            if (equipment == null)
            {
                continue;
            }

            if (equipment.equipmentId == id)
            {
                return equipment;
            }
        }

        return null;
    }
}
