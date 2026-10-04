
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARImageTracking : MonoBehaviour
{
    [Header("Equipment Settings")]

    // โมเดลมอเตอร์ที่ต้องการแสดง
    [SerializeField]
    private GameObject stepperMotorPrefab;

    // ค้นหาโมเดลจากข้อมูลอุปกรณ์ (เก็บ Prefab เดิมไว้เป็นตัวสำรอง)
    [Header("Equipment Database")]
    [SerializeField]
    private EquipmentDatabase equipmentDatabase;

    // ชื่อภาพที่กำหนดไว้ใน Reference Image Library
    [SerializeField]
    private string targetImageName = "StepperMotor";

    [Header("Model Settings")]

    // ระยะยกโมเดลขึ้นจากกระดาษ หน่วยเป็นเมตร
    [SerializeField]
    private float heightOffset = 0.03f;

    // ตัวคูณขนาดของโมเดล
    [SerializeField]
    private float modelScale = 1f;

    // ระบบตรวจจับรูปภาพ AR
    private ARTrackedImageManager imageManager;

    // Phase 7: optional companion on the same XR Origin.
    private ARModelGestureController gestureController;

    public event System.Action<string, bool> EquipmentTrackingChanged;

    public bool IsTrackingTarget { get; private set; }
    // Kept for compatibility with EquipmentInfoUI in Phase 2.
    public string TargetImageName => IsTrackingTarget ? activeImageName : targetImageName;
    public EquipmentDatabase Database => equipmentDatabase;

    // The UI displays the most recently newly-tracked known image.
    private string activeImageName;
    private readonly Dictionary<TrackableId, string> trackedImageNames =
        new Dictionary<TrackableId, string>();
    private readonly List<TrackableId> trackingOrder = new List<TrackableId>();
    private readonly HashSet<TrackableId> missingPrefabWarnings =
        new HashSet<TrackableId>();

    // เก็บโมเดลที่สร้างขึ้นตาม ID ของภาพ
    private Dictionary<TrackableId, GameObject> spawnedModels =
        new Dictionary<TrackableId, GameObject>();


    void Awake()
    {
        // รับ Component สำหรับตรวจจับรูปภาพ
        imageManager = GetComponent<ARTrackedImageManager>();
        gestureController = GetComponent<ARModelGestureController>();
    }


    void OnEnable()
    {
        // เริ่มรับเหตุการณ์เมื่อมีการตรวจจับภาพ
        if (imageManager != null)
        {
            imageManager.trackablesChanged.AddListener(
                OnTrackedImagesChanged
            );

            // Re-enabling does not generate added events for existing images.
            if (imageManager.isActiveAndEnabled)
            {
                var existingIds = new HashSet<TrackableId>();
                foreach (ARTrackedImage image in imageManager.trackables)
                {
                    if (image == null)
                        continue;

                    existingIds.Add(image.trackableId);
                    UpdateImage(image);
                }

                var staleIds = new List<TrackableId>();
                foreach (var entry in spawnedModels)
                {
                    if (!existingIds.Contains(entry.Key))
                        staleIds.Add(entry.Key);
                }
                foreach (TrackableId id in staleIds)
                    RemoveImage(id);

                PublishTrackingState();
            }
        }

    }


    void OnDestroy()
    {
        foreach (var entry in spawnedModels)
        {
            gestureController?.Forget(entry.Key);
            if (entry.Value != null)
                Destroy(entry.Value);
        }
        spawnedModels.Clear();
    }

    void OnDisable()
    {
        // หยุดรับเหตุการณ์เมื่อปิด Script
        if (imageManager != null)
        {
            imageManager.trackablesChanged.RemoveListener(
                OnTrackedImagesChanged
            );
        }

        foreach (var entry in spawnedModels)
        {
            gestureController?.SetTracking(entry.Key, false);
            if (entry.Value != null)
                entry.Value.SetActive(false);
        }
        trackedImageNames.Clear();
        trackingOrder.Clear();
        missingPrefabWarnings.Clear();
        PublishTrackingState();
    }


    // ฟังก์ชันทำงานเมื่อสถานะรูปภาพเปลี่ยนแปลง
    void OnTrackedImagesChanged(
        ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        // ตรวจสอบภาพที่เพิ่งตรวจพบ
        foreach (ARTrackedImage image in eventArgs.added)
        {
            UpdateImage(image);
        }

        // ตรวจสอบภาพที่มีการเปลี่ยนแปลง
        foreach (ARTrackedImage image in eventArgs.updated)
        {
            UpdateImage(image);
        }

        // ลบโมเดลของภาพที่ถูกนำออกจากระบบติดตาม
        foreach (var removedImage in eventArgs.removed)
        {
            // The removed Unity object may already be destroyed; use its ID.
            RemoveImage(removedImage.Key);
        }

        // Publish only after the whole batch, so replacing one tracked image
        // with another cannot briefly hide the equipment information panel.
        PublishTrackingState();
    }


    void RemoveImage(TrackableId imageId)
    {
        // Forget before destroying the spawned model.
        gestureController?.Forget(imageId);
        UntrackImage(imageId);
        missingPrefabWarnings.Remove(imageId);
        if (spawnedModels.TryGetValue(imageId, out GameObject model))
        {
            if (model != null)
                Destroy(model);
            spawnedModels.Remove(imageId);
        }
    }

    void UntrackImage(TrackableId id)
    {
        trackedImageNames.Remove(id);
        trackingOrder.Remove(id);
    }

    void PublishTrackingState()
    {
        // Prefer the most recently newly-tracked image. If it disappears,
        // show the previous image that is still being tracked.
        string nextName = null;
        for (int i = trackingOrder.Count - 1; i >= 0; --i)
        {
            if (trackedImageNames.TryGetValue(trackingOrder[i], out nextName))
                break;
            trackingOrder.RemoveAt(i);
            nextName = null;
        }

        bool isTracking = !string.IsNullOrEmpty(nextName);
        if (IsTrackingTarget == isTracking &&
            string.Equals(activeImageName, nextName, System.StringComparison.Ordinal))
            return;

        IsTrackingTarget = isTracking;
        activeImageName = nextName;
        // Keep Phase 2's event signature unchanged so Close and UI work.
        EquipmentTrackingChanged?.Invoke(
            isTracking ? activeImageName : targetImageName, isTracking);
    }

    // Check every image registered in MechaAR_ImageLibrary.
    void UpdateImage(ARTrackedImage trackedImage)
    {
        if (trackedImage == null)
            return;

        string imageName = trackedImage.referenceImage.name;
        TrackableId imageId = trackedImage.trackableId;
        bool isTracking = trackedImage.trackingState == TrackingState.Tracking;

        if (!isTracking)
        {
            UntrackImage(imageId);
            gestureController?.SetTracking(imageId, false);
            if (spawnedModels.TryGetValue(imageId, out GameObject hiddenModel) &&
                hiddenModel != null)
                hiddenModel.SetActive(false);
            return;
        }

        if (!spawnedModels.TryGetValue(imageId, out GameObject model) || model == null)
        {
            // In this phase every scanned image selects its own model.
            EquipmentData data = equipmentDatabase != null
                ? equipmentDatabase.GetByImageName(imageName)
                : null;
            GameObject selectedPrefab = data != null ? data.modelPrefab : null;

            // Legacy fallback is ONLY for the old StepperMotor image,
            // never show a motor when an Arduino image has no prefab.
            if (selectedPrefab == null && imageName == targetImageName)
                selectedPrefab = stepperMotorPrefab;

            if (selectedPrefab == null)
            {
                // Do not publish tracking for unsupported/incomplete equipment.
                if (missingPrefabWarnings.Add(imageId))
                    Debug.LogWarning("ไม่มีโมเดลสำหรับภาพ: " + imageName);
                UntrackImage(imageId);
                return;
            }

            model = Instantiate(selectedPrefab, trackedImage.transform);

            // Preserve the previously tested offset, rotation and scale.
            model.transform.localPosition = new Vector3(0, heightOffset, 0);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * modelScale;

            missingPrefabWarnings.Remove(imageId);
            spawnedModels[imageId] = model;
            Debug.Log("ตรวจพบอุปกรณ์: " + imageName);
        }

        model.SetActive(true);
        // Register on creation and resume; the gesture component preserves rotation/scale.
        gestureController?.RegisterModel(trackedImage, model);

        // Keep its entry stable during ordinary per-frame updates.
        if (!trackedImageNames.ContainsKey(imageId))
            trackingOrder.Add(imageId);
        trackedImageNames[imageId] = imageName;
    }
}
