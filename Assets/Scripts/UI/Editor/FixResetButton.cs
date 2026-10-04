using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Quick standalone fix for the Reset3DButton clipping issue.
/// Run via MechaAR > Fix Reset Button Only. Safe to run multiple times.</summary>
public static class FixResetButton
{
    [MenuItem("MechaAR/Fix Reset Button Only")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[FixResetButton] Exit Play Mode first.");
            return;
        }

        // Find the button in the open scene
        var viewer = Object.FindFirstObjectByType<Equipment3DViewer>(FindObjectsInactive.Include);
        if (viewer == null)
        {
            Debug.LogError("[FixResetButton] Equipment3DViewer not found. Open MechaAR_Main scene.");
            return;
        }

        var artwork = viewer.transform.parent; // DetailArtwork
        var resetButton = artwork.Find("Reset3DButton");
        if (resetButton == null)
        {
            Debug.LogError("[FixResetButton] Reset3DButton not found under DetailArtwork.");
            return;
        }

        Undo.RecordObject(resetButton, "Fix Reset3DButton RectTransform");

        // --- RectTransform Fix ---
        var rect = (RectTransform)resetButton;
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0); // Bottom-Right anchor
        rect.pivot = new Vector2(1, 0); // Pivot at bottom-right corner
        rect.sizeDelta = new Vector2(80, 80); // 80px at 1080 reference = good touch target
        rect.anchoredPosition = new Vector2(-20, 20); // 20px padding from edges
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling(); // Render on top of Equipment3DPreview

        // Ignore parent VerticalLayoutGroup
        var layout = rect.GetComponent<LayoutElement>();
        if (layout == null) layout = rect.gameObject.AddComponent<LayoutElement>();
        Undo.RecordObject(layout, "Fix Reset3DButton LayoutElement");
        layout.ignoreLayout = true;

        // --- Image (background) ---
        var image = rect.GetComponent<Image>();
        if (image != null)
        {
            Undo.RecordObject(image, "Fix Reset3DButton Image color");
            ColorUtility.TryParseHtmlString("#D4F268", out var lime);
            image.color = lime;
        }

        // --- Hide old TMP Text (the ↺ character may not render) ---
        var tmpText = resetButton.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmpText != null)
        {
            Undo.RecordObject(tmpText.gameObject, "Disable old TMP text");
            tmpText.gameObject.SetActive(false);
        }

        // --- Add or find ResetViewIcon (vector graphic) ---
        var iconTransform = rect.Find("ResetArrowIcon");
        RectTransform iconRect;
        if (iconTransform != null)
        {
            iconRect = (RectTransform)iconTransform;
        }
        else
        {
            var iconGO = new GameObject("ResetArrowIcon", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(iconGO, "Create ResetArrowIcon");
            iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.SetParent(rect, false);
            iconRect.gameObject.layer = rect.gameObject.layer;
        }

        // Stretch fill with 10px inset
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(10, 10);
        iconRect.offsetMax = new Vector2(-10, -10);

        var icon = iconRect.GetComponent<ResetViewIcon>();
        if (icon == null) icon = iconRect.gameObject.AddComponent<ResetViewIcon>();
        ColorUtility.TryParseHtmlString("#142B29", out var ink);
        icon.color = ink;
        icon.raycastTarget = false;

        // --- Verify Button OnClick ---
        var button = rect.GetComponent<Button>();
        if (button != null)
        {
            bool hasResetView = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentMethodName(i) == "ResetView"
                    && button.onClick.GetPersistentTarget(i) is Equipment3DViewer)
                {
                    hasResetView = true;
                    break;
                }
            }
            if (!hasResetView)
                Debug.LogWarning("[FixResetButton] Button.OnClick does NOT have ResetView! Check manually.");
            else
                Debug.Log("[FixResetButton] Button.OnClick → Equipment3DViewer.ResetView() ✓");
        }

        // Mark scene dirty
        EditorSceneManager.MarkSceneDirty(resetButton.gameObject.scene);
        Debug.Log("[FixResetButton] ✅ Reset3DButton fixed successfully!\n" +
                  "  • RectTransform: Anchor(1,0) Pivot(1,0) Size(80,80) Pos(-20,20)\n" +
                  "  • LayoutElement: ignoreLayout = true\n" +
                  "  • Image color: #D4F268 (lime)\n" +
                  "  • ResetViewIcon: vector ↺ in #142B29 (ink)\n" +
                  "  • TMP Text: hidden (replaced by vector icon)\n" +
                  "  Save the scene with Ctrl+S to persist changes.");
    }
}
