
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARTapToPlace : MonoBehaviour
{
    // ระบบตรวจจับตำแหน่งบนพื้นผิว AR
    [Header("AR Settings")]
    [SerializeField]
    private ARRaycastManager raycastManager;

    // โมเดลอุปกรณ์ที่ต้องการแสดง
    [Header("Equipment Model")]
    [SerializeField]
    private GameObject equipmentPrefab;

    // เก็บโมเดลที่สร้างขึ้นมาแล้ว
    private GameObject placedEquipment;

    // เก็บผลลัพธ์จากการตรวจจับพื้นผิว
    private static readonly List<ARRaycastHit> hits =
        new List<ARRaycastHit>();

    void Update()
    {
        // ตรวจสอบว่าผู้ใช้แตะหน้าจอหรือไม่
        if (Input.touchCount == 0)
        {
            return;
        }

        // อ่านข้อมูลการแตะนิ้วแรก
        Touch touch = Input.GetTouch(0);

        // ทำงานเฉพาะตอนเริ่มแตะ
        if (touch.phase != TouchPhase.Began)
        {
            return;
        }

        // ตรวจสอบว่าเชื่อมต่อ Component ครบหรือไม่
        if (raycastManager == null ||
            equipmentPrefab == null)
        {
            Debug.LogWarning(
                "ยังไม่ได้กำหนด AR Raycast Manager หรือ Prefab"
            );

            return;
        }

        // ตรวจสอบว่าจุดที่แตะตรงกับพื้นผิว AR หรือไม่
        bool foundPlane = raycastManager.Raycast(
            touch.position,
            hits,
            TrackableType.PlaneWithinPolygon
        );

        // หากไม่พบพื้นผิว ไม่ต้องสร้างโมเดล
        if (!foundPlane)
        {
            return;
        }

        // รับตำแหน่งและทิศทางของพื้นผิว
        Pose hitPose = hits[0].pose;

        // ถ้ายังไม่มีโมเดล ให้สร้างโมเดลใหม่
        if (placedEquipment == null)
        {
            placedEquipment = Instantiate(
                equipmentPrefab,
                hitPose.position,
                hitPose.rotation
            );

            Debug.Log("สร้างโมเดล AR สำเร็จ");
        }
        else
        {
            // หากมีโมเดลแล้ว ให้ย้ายตำแหน่ง
            placedEquipment.transform.SetPositionAndRotation(
                hitPose.position,
                hitPose.rotation
            );

            Debug.Log("ย้ายตำแหน่งโมเดล AR สำเร็จ");
        }
    }
}