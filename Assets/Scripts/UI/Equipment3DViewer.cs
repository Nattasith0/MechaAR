using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A non-AR preview for the existing EquipmentData.modelPrefab.
/// Attach this to DetailArtwork/Equipment3DPreview (RawImage).
/// </summary>
public sealed class Equipment3DViewer : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Header("Existing Preview Setup")]
    [SerializeField] private Camera previewCamera;
    [SerializeField] private Transform previewRoot;

    [Header("Framing")]
    [SerializeField, Range(0.4f, 0.95f)] private float frameFill = 0.8f;
    [SerializeField] private Vector3 viewingDirection = new Vector3(0.5f, 0.25f, -1f);

    [Header("Interaction")]
    [SerializeField, Range(0.05f, 1f)] private float dragSensitivity = 0.25f;

    private GameObject activeModel;
    private Transform rotationPivot;
    private Quaternion initialRotation;
    private Renderer[] previewRenderers;

    public void Show(EquipmentData data)
    {
        Hide();

        if (previewCamera == null || previewRoot == null || data == null || data.modelPrefab == null)
        {
            Debug.LogWarning("[MechaAR 3D Viewer] Missing camera, root or equipment model prefab.", this);
            return;
        }

        int previewLayer = LayerMask.NameToLayer("EquipmentPreview");
        if (previewLayer < 0)
        {
            Debug.LogError("[MechaAR 3D Viewer] Create the EquipmentPreview layer first.", this);
            return;
        }

        GameObject pivotObject = new GameObject("ActivePreviewPivot");
        rotationPivot = pivotObject.transform;
        rotationPivot.SetParent(previewRoot, false);
        rotationPivot.localPosition = Vector3.zero;
        rotationPivot.localRotation = Quaternion.identity;
        rotationPivot.localScale = Vector3.one;
        initialRotation = rotationPivot.localRotation;

        activeModel = Instantiate(data.modelPrefab, rotationPivot);
        activeModel.name = "Preview_" + data.equipmentId;
        activeModel.transform.localPosition = Vector3.zero;
        activeModel.transform.localRotation = data.modelPrefab.transform.localRotation;
        activeModel.transform.localScale = data.modelPrefab.transform.localScale;
        SetLayerRecursively(activeModel.transform, previewLayer);

        if (!TryGetVisibleBounds(activeModel, out Bounds bounds))
        {
            Debug.LogWarning("[MechaAR 3D Viewer] No visible mesh in " + data.equipmentId, this);
            Hide();
            return;
        }

        // Move the instantiated model inside its rotation pivot. This makes it spin
        // around the visible mesh center even when the imported GLB pivot is offset.
        activeModel.transform.position += rotationPivot.position - bounds.center;
        previewRenderers = activeModel.GetComponentsInChildren<Renderer>(true);
        FitPreview();
        previewCamera.enabled = true;
    }

    private void FitPreview()
    {
        if (previewCamera == null || rotationPivot == null || previewRenderers == null) return;
        Vector3 direction = viewingDirection.sqrMagnitude > 0.001f
            ? viewingDirection.normalized : Vector3.back;
        Vector3 target = rotationPivot.position;
        Quaternion cameraRotation = Quaternion.LookRotation(-direction, Vector3.up);
        Vector3 right = cameraRotation * Vector3.right;
        Vector3 up = cameraRotation * Vector3.up;
        Vector3 forward = cameraRotation * Vector3.forward;
        float aspect = previewCamera.targetTexture != null
            ? (float)previewCamera.targetTexture.width / previewCamera.targetTexture.height
            : previewCamera.aspect;
        float tanVertical = Mathf.Tan(.5f * previewCamera.fieldOfView * Mathf.Deg2Rad) * Mathf.Clamp(frameFill, .4f, .95f);
        float tanHorizontal = tanVertical * Mathf.Max(.01f, aspect);
        float distance = .0001f, minDepth = float.PositiveInfinity, maxDepth = float.NegativeInfinity;
        // Fit each visible renderer's oriented local bounds, not a sphere constrained
        // by the short viewport height. Refit only on user interaction so slender
        // equipment fills the wide viewer while remaining visible after rotation.
        foreach (Renderer renderer in previewRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            Bounds local = renderer.localBounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new Vector3((corner & 1) == 0 ? -local.extents.x : local.extents.x,
                    (corner & 2) == 0 ? -local.extents.y : local.extents.y,
                    (corner & 4) == 0 ? -local.extents.z : local.extents.z);
                Vector3 relative = renderer.transform.TransformPoint(local.center + offset) - target;
                float depth = Vector3.Dot(relative, forward);
                minDepth = Mathf.Min(minDepth, depth);
                maxDepth = Mathf.Max(maxDepth, depth);
                distance = Mathf.Max(distance, Mathf.Abs(Vector3.Dot(relative, right)) / tanHorizontal - depth,
                    Mathf.Abs(Vector3.Dot(relative, up)) / tanVertical - depth);
            }
        }
        if (float.IsPositiveInfinity(minDepth)) return;
        float margin = Mathf.Max(.00001f, (maxDepth - minDepth) * .05f);
        distance = Mathf.Max(distance, -minDepth + margin * 2f);
        previewCamera.transform.position = target + direction * distance;
        previewCamera.transform.rotation = cameraRotation;
        previewCamera.nearClipPlane = Mathf.Max(.00001f, distance + minDepth - margin);
        previewCamera.farClipPlane = Mathf.Max(previewCamera.nearClipPlane + .001f, distance + maxDepth + margin);
    }

    public void Hide()
    {
        if (previewCamera != null) previewCamera.enabled = false;
        if (rotationPivot != null) Destroy(rotationPivot.gameObject);
        rotationPivot = null;
        activeModel = null;
        previewRenderers = null;
    }

    public void ResetView()
    {
        if (rotationPivot != null) rotationPivot.localRotation = initialRotation;
        FitPreview();
    }

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (rotationPivot == null || previewCamera == null) return;
        rotationPivot.Rotate(Vector3.up, -eventData.delta.x * dragSensitivity, Space.World);
        rotationPivot.Rotate(previewCamera.transform.right,
            eventData.delta.y * dragSensitivity, Space.World);
        FitPreview();
    }

    private void OnDisable()
    {
        Hide();
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    private static bool TryGetVisibleBounds(GameObject root, out Bounds combined)
    {
        combined = new Bounds(root.transform.position, Vector3.zero);
        bool found = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            // Do not include hidden GLB decorations such as Arduino's @Floor.
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (!found)
            {
                combined = renderer.bounds;
                found = true;
            }
            else combined.Encapsulate(renderer.bounds);
        }
        return found;
    }
}
