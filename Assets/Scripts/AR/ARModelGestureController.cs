using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Phase 7: rotates and zooms spawned AR equipment without moving ARTrackedImage.
/// Standalone until ARImageTracking.RegisterModel/SetTracking/Forget are wired up.
/// </summary>
public sealed class ARModelGestureController : MonoBehaviour
{
    [Header("Existing MechaAR references")]
    [SerializeField] private ARImageTracking imageTracking;
    [SerializeField] private MechaARUIManager uiManager;

    [Header("Gesture settings")]
    [SerializeField, Range(0.05f, 1f)] private float rotationDegreesPerPixel = 0.25f;
    [SerializeField, Min(0.1f)] private float minimumScaleMultiplier = 0.5f;
    [SerializeField, Min(0.1f)] private float maximumScaleMultiplier = 2f;

    private sealed class ModelState
    {
        public GameObject Model;
        public string ImageName;
        public Quaternion OriginalRotation;
        public Vector3 OriginalScale;
        public float Yaw;
        public float Pitch;
        public float ScaleMultiplier = 1f;
        public bool IsTracking;
    }

    private readonly Dictionary<TrackableId, ModelState> models =
        new Dictionary<TrackableId, ModelState>();
    private readonly List<TrackableId> registrationOrder = new List<TrackableId>();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private TrackableId? selectedId;
    private int previousFingerCount;
    private Vector2 lastFingerPosition;
    private float lastPinchDistance;

    private void Awake()
    {
        if (imageTracking == null)
            imageTracking = GetComponent<ARImageTracking>();
    }

    private void OnEnable()
    {
        if (imageTracking != null)
        {
            imageTracking.EquipmentTrackingChanged += OnEquipmentTrackingChanged;
            FocusImage(imageTracking.IsTrackingTarget ? imageTracking.TargetImageName : null);
        }
    }

    private void OnDisable()
    {
        if (imageTracking != null)
            imageTracking.EquipmentTrackingChanged -= OnEquipmentTrackingChanged;
        selectedId = null;
        ClearGesture();
    }

    /// <summary>Call after spawning a prefab, and again when tracking resumes.</summary>
    public void RegisterModel(ARTrackedImage image, GameObject model)
    {
        if (image == null || model == null) return;

        TrackableId id = image.trackableId;
        if (!models.TryGetValue(id, out ModelState state) || state.Model != model)
        {
            state = new ModelState
            {
                Model = model,
                ImageName = image.referenceImage.name,
                OriginalRotation = model.transform.localRotation,
                OriginalScale = model.transform.localScale
            };
            models[id] = state;
            registrationOrder.Remove(id);
            registrationOrder.Add(id);
        }
        state.IsTracking = true;

        // ARTrackedImage updates may arrive every frame. Do not reset an ongoing
        // drag/pinch when the same registered model is already selected.
        if (imageTracking != null && imageTracking.IsTrackingTarget &&
            imageTracking.TargetImageName == state.ImageName &&
            (!selectedId.HasValue || selectedId.Value != id))
            FocusImage(state.ImageName);
    }

    /// <summary>Call when the tracked image is temporarily not Tracking.</summary>
    public void SetTracking(TrackableId id, bool isTracking)
    {
        if (!models.TryGetValue(id, out ModelState state)) return;
        state.IsTracking = isTracking;
        if (!isTracking && selectedId.HasValue && selectedId.Value == id)
        {
            selectedId = null;
            ClearGesture();
        }
    }

    /// <summary>Call before destroying the spawned model for a removed image.</summary>
    public void Forget(TrackableId id)
    {
        models.Remove(id);
        registrationOrder.Remove(id);
        if (selectedId.HasValue && selectedId.Value == id)
        {
            selectedId = null;
            ClearGesture();
        }
    }

    /// <summary>Wire the Scanner reset button to this method later.</summary>
    public void ResetCurrent()
    {
        ModelState state = SelectedModel();
        if (state == null) return;
        state.Yaw = 0f;
        state.Pitch = 0f;
        state.ScaleMultiplier = 1f;
        state.Model.transform.localRotation = state.OriginalRotation;
        state.Model.transform.localScale = state.OriginalScale;
        ClearGesture();
    }

    private void OnEquipmentTrackingChanged(string imageName, bool isTracking)
    {
        FocusImage(isTracking ? imageName : null);
    }

    private void FocusImage(string imageName)
    {
        selectedId = null;
        ClearGesture();
        if (string.IsNullOrEmpty(imageName)) return;

        for (int i = registrationOrder.Count - 1; i >= 0; i--)
        {
            TrackableId id = registrationOrder[i];
            if (models.TryGetValue(id, out ModelState state) &&
                state.IsTracking && state.Model != null &&
                state.ImageName == imageName)
            {
                selectedId = id;
                break;
            }
        }
    }

    private ModelState SelectedModel()
    {
        if (!selectedId.HasValue) return null;
        if (!models.TryGetValue(selectedId.Value, out ModelState state) ||
            !state.IsTracking || state.Model == null || !state.Model.activeInHierarchy)
            return null;
        return state;
    }

    private void Update()
    {
        // Never manipulate an AR model while Library/Home/Detail UI is open.
        if (uiManager != null && uiManager.CurrentPage != MechaARUIManager.Page.Scanner)
        {
            ClearGesture();
            return;
        }

        ModelState state = SelectedModel();
        if (state == null)
        {
            ClearGesture();
            return;
        }

        int fingerCount = 0;
        Vector2 first = Vector2.zero;
        Vector2 second = Vector2.zero;

#if ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < Input.touchCount && fingerCount < 3; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled)
                continue;
            if (fingerCount == 0) first = touch.position;
            else if (fingerCount == 1) second = touch.position;
            fingerCount++;
        }
#elif ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.isPressed) continue;
                Vector2 position = touch.position.ReadValue();
                if (fingerCount == 0) first = position;
                else if (fingerCount == 1) second = position;
                fingerCount++;
                if (fingerCount >= 3) break;
            }
        }
#endif

        if (fingerCount < 1 || fingerCount > 2 || IsOverUI(first) ||
            (fingerCount == 2 && IsOverUI(second)))
        {
            ClearGesture();
            return;
        }

        if (fingerCount != previousFingerCount)
        {
            previousFingerCount = fingerCount;
            lastFingerPosition = first;
            lastPinchDistance = fingerCount == 2 ? Vector2.Distance(first, second) : 0f;
            return;
        }

        if (fingerCount == 1)
        {
            Vector2 delta = first - lastFingerPosition;
            lastFingerPosition = first;
            state.Yaw -= delta.x * rotationDegreesPerPixel;
            state.Pitch += delta.y * rotationDegreesPerPixel;
            state.Model.transform.localRotation =
                Quaternion.Euler(state.Pitch, state.Yaw, 0f) * state.OriginalRotation;
        }
        else
        {
            float distance = Vector2.Distance(first, second);
            if (lastPinchDistance > 1f && distance > 1f)
            {
                float min = Mathf.Min(minimumScaleMultiplier, maximumScaleMultiplier);
                float max = Mathf.Max(minimumScaleMultiplier, maximumScaleMultiplier);
                state.ScaleMultiplier = Mathf.Clamp(
                    state.ScaleMultiplier * distance / lastPinchDistance, min, max);
                state.Model.transform.localScale =
                    state.OriginalScale * state.ScaleMultiplier;
            }
            lastPinchDistance = distance;
        }
    }

    private bool IsOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };
        uiHits.Clear();
        EventSystem.current.RaycastAll(eventData, uiHits);
        return uiHits.Count > 0;
    }

    private void ClearGesture()
    {
        previousFingerCount = 0;
        lastPinchDistance = 0f;
    }
}
