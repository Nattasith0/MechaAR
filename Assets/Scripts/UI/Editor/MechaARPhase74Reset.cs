using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Adds only the scanner reset control, calling the existing gesture API.</summary>
public static class MechaARPhase74Reset
{
    [MenuItem("MechaAR/Phase 7.4/Add existing AR reset control")]
    public static void Apply()
    {
        const string path = "Assets/Scenes/MechaAR_Main.unity";
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode first.");
        for (int i = 0; i < EditorSceneManager.sceneCount; i++) Require(!EditorSceneManager.GetSceneAt(i).isDirty, "Save pending scene changes first.");
        string backup = ".agent-system/tasks/MECHA-PHASE6/backups/reset-patch/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup); File.Copy(path, backup + "/MechaAR_Main.unity"); File.Copy(path + ".meta", backup + "/MechaAR_Main.unity.meta");
        var scene = EditorSceneManager.OpenScene(path);
        var managers = UnityEngine.Object.FindObjectsByType<MechaARUIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var gestures = UnityEngine.Object.FindObjectsByType<ARModelGestureController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Require(managers.Length == 1 && gestures.Length == 1, "Expected existing UI and gesture controllers.");
        var config = new SerializedObject(managers[0]);
        var page = ((GameObject)config.FindProperty("scannerPage").objectReferenceValue).transform;
        var existing = page.GetComponentsInChildren<RectTransform>(true).Where(x => x.name == "ARResetButton").ToArray();
        Require(existing.Length <= 1, "Duplicate ARResetButton.");
        var rect = existing.Length == 1 ? existing[0] : Node(page, "ARResetButton");
        rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-28, -318); rect.sizeDelta = new Vector2(240, 112); rect.localScale = Vector3.one;
        var panel = Get<RoundedPanelGraphic>(rect); ColorUtility.TryParseHtmlString("#D4F268", out var lime); ColorUtility.TryParseHtmlString("#142B29", out var ink); panel.color = lime;
        var button = Get<Button>(rect); button.targetGraphic = panel;
        var colors = button.colors; colors.normalColor = colors.selectedColor = Color.white; colors.pressedColor = new Color(.8f, .88f, .76f); button.colors = colors;
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, gestures[0].ResetCurrent);
        var icon = Node(rect, "ResetArrowIcon"); Anchor(icon, new Vector2(0, 0), new Vector2(0, 1), new Vector2(8, 16), new Vector2(80, -16));
        var graphic = Get<ResetViewIcon>(icon); graphic.color = ink; graphic.raycastTarget = false;
        var label = Get<TextMeshProUGUI>(Node(rect, "ResetLabel"));
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/MechaARThai SDF.asset"); Require(label.font != null, "Existing Thai font required.");
        label.text = "คืนมุมมอง"; label.fontSize = 27; label.color = ink; label.raycastTarget = false; label.alignment = TextAlignmentOptions.Center;
        Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(78, 10), new Vector2(-10, -10));
        rect.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(scene); Require(EditorSceneManager.SaveScene(scene), "Reset control save failed.");
        Debug.Log("[MechaAR Phase7.4] RESET BUTTON PATCH PASSED: existing ResetCurrent callback; gesture and AR source unchanged.");
    }
    static T Get<T>(Component c) where T : Component { return c.GetComponent<T>() ?? c.gameObject.AddComponent<T>(); }
    static RectTransform Node(Transform parent, string name)
    { var existing = parent.Find(name); if (existing != null) return (RectTransform)existing; var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); r.gameObject.layer = parent.gameObject.layer; return r; }
    static void Anchor(RectTransform r, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    { r.anchorMin = min; r.anchorMax = max; r.offsetMin = low; r.offsetMax = high; }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
