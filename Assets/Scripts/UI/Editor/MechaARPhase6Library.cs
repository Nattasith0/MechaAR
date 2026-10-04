using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Explicit, idempotent patch of the existing Library only; never recreates the application canvas.</summary>
public static class MechaARPhase6Library
{
    const string ScenePath = "Assets/Scenes/MechaAR_Main.unity";
    static readonly Color Ink = Hex("142B29"), Lime = Hex("D4F268"), Card = Hex("E1ECE6"), Muted = Hex("52675D");
    static TMP_FontAsset font;

    // Explicit combined entry for the currently authorized Library and scanner-button scope.
    public static void ApplyCurrentScope()
    {
        Apply();
        MechaARPhase74Reset.Apply();
    }

    [MenuItem("MechaAR/Phase 6/Update existing Equipment Library")]
    public static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode first.");
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            Require(!EditorSceneManager.GetSceneAt(i).isDirty, "Save pending scene changes first.");
        string backup = ".agent-system/tasks/MECHA-PHASE6/backups/library-patch/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/MechaAR_Main.unity");
        File.Copy(ScenePath + ".meta", backup + "/MechaAR_Main.unity.meta");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var managers = UnityEngine.Object.FindObjectsByType<MechaARUIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Require(managers.Length == 1, "Expected one existing application manager.");
        var manager = managers[0];
        var config = new SerializedObject(manager);
        var page = ((GameObject)config.FindProperty("libraryPage").objectReferenceValue).transform;
        var input = (TMP_InputField)config.FindProperty("librarySearchInput").objectReferenceValue;
        var template = (EquipmentLibraryCard)config.FindProperty("libraryCardTemplate").objectReferenceValue;
        var database = (EquipmentDatabase)config.FindProperty("equipmentDatabase").objectReferenceValue;
        Require(input != null && template != null && database != null, "Existing Library bindings required.");
        var scroll = Find(page, "LibraryScroll").GetComponent<ScrollRect>();
        font = Find(page, "LibraryIntro").GetComponent<TMP_Text>().font;
        Require(font != null, "Existing Thai font required.");
        var controls = Node(page, "LibraryControls");
        Top(controls, 46, -46, 174, 520);
        MoveText(page, controls, "LibraryIntro", "สำรวจคลังอุปกรณ์", 44, Ink, 0, 70);
        MoveText(page, controls, "LibraryDescription", "ค้นหาและเลือกอุปกรณ์ เพื่อเรียนรู้ผ่านข้อมูลและโมเดล 3D", 29, Muted, 72, 76);
        input.transform.SetParent(controls, false);
        var searchRect = (RectTransform)input.transform;
        Top(searchRect, 0, 0, 160, 104);
        var layout = input.GetComponent<LayoutElement>(); if (layout != null) layout.ignoreLayout = true;
        var oldImage = input.GetComponent<Image>(); if (oldImage != null) UnityEngine.Object.DestroyImmediate(oldImage);
        input.targetGraphic = Rounded(searchRect, Card, 22);
        input.SetTextWithoutNotify("");
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.characterLimit = 128;
        input.pointSize = 32;
        input.customCaretColor = true; input.caretColor = Ink;
        input.selectionColor = new Color(Lime.r, Lime.g, Lime.b, .65f);
        Anchor(input.textViewport, Vector2.zero, Vector2.one, new Vector2(28, 12), new Vector2(-28, -12));
        Style(input.textComponent, 32, Ink); input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        var placeholder = input.placeholder as TMP_Text;
        Require(placeholder != null, "Existing TMP placeholder required.");
        Style(placeholder, 30, Muted); placeholder.fontStyle = FontStyles.Normal;
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.text = "ค้นหาชื่อ รหัส หรือหมวดหมู่...";
        // The manager owns the single runtime search subscription. Preserve unrelated callbacks.
        for (int i = input.onValueChanged.GetPersistentEventCount() - 1; i >= 0; i--)
            if (input.onValueChanged.GetPersistentTarget(i) == manager && input.onValueChanged.GetPersistentMethodName(i) == "SetLibrarySearch")
                UnityEventTools.RemovePersistentListener(input.onValueChanged, i);
        var categories = Node(controls, "LibraryCategories"); Top(categories, 0, 0, 282, 186);
        string[] names = { "CategoryAll", "CategoryMotor", "CategorySensor", "CategoryController" };
        string[] labels = { "ทั้งหมด", "Motor & Actuator", "Sensor", "Microcontroller &\nController" };
        UnityAction[] callbacks = { manager.ShowAllEquipment, manager.ShowMotorEquipment, manager.ShowSensorEquipment, manager.ShowControllerEquipment };
        var buttons = config.FindProperty("libraryCategoryButtons"); Require(buttons != null, "Phase 6 runtime fields required."); buttons.arraySize = 4;
        for (int i = 0; i < names.Length; i++)
        {
            var rect = Node(categories, names[i]); float x = i % 2 * .5f; float top = i / 2 * 98;
            Anchor(rect, new Vector2(x, 1), new Vector2(x + .5f, 1), new Vector2(i % 2 == 0 ? 0 : 7, -top - 88), new Vector2(i % 2 == 0 ? -7 : 0, -top));
            var button = Get<Button>(rect); button.targetGraphic = Rounded(rect, i == 0 ? Ink : Card, 18);
            var colors = button.colors; colors.normalColor = colors.selectedColor = Color.white; colors.highlightedColor = new Color(.95f, .98f, .93f); colors.pressedColor = new Color(.8f, .88f, .76f); colors.fadeDuration = .1f; button.colors = colors;
            var label = Text(rect, "Label", labels[i], i == 3 ? 22 : 27, i == 0 ? Lime : Ink);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4)); label.alignment = TextAlignmentOptions.Center;
            for (int j = button.onClick.GetPersistentEventCount() - 1; j >= 0; j--) UnityEventTools.RemovePersistentListener(button.onClick, j);
            UnityEventTools.AddPersistentListener(button.onClick, callbacks[i]); buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
        }
        var results = Text(controls, "LibraryResultText", "", 26, Muted); Top(results.rectTransform, 0, 0, 474, 42);
        Set(config, "libraryResultText", results); Set(config, "libraryScroll", scroll);
        Anchor((RectTransform)scroll.transform, Vector2.zero, Vector2.one, new Vector2(46, 158), new Vector2(-46, -704));
        scroll.horizontal = false; scroll.vertical = true;
        var empty = (TMP_Text)config.FindProperty("libraryEmptyText").objectReferenceValue;
        Style(empty, 31, Muted); FixedHeight(empty.rectTransform, 180);
        empty.alignment = TextAlignmentOptions.Center;
        var tip = Find(page, "LibraryTip").GetComponent<TMP_Text>();
        tip.text = "เลือกอุปกรณ์เพื่ออ่านข้อมูลและสำรวจโมเดล 3D"; Style(tip, 27, Muted); FixedHeight(tip.rectTransform, 90);
        PolishCard(template);
        var artwork = config.FindProperty("libraryArtwork"); Require(artwork != null, "Phase 6 artwork field required.");
        var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/UI/Images/Equipment" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var items = database.Equipments.Where(x => x != null).Distinct().ToArray();
        artwork.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            var row = artwork.GetArrayElementAtIndex(i); row.FindPropertyRelative("equipmentData").objectReferenceValue = items[i];
            var matches = paths.Where(x => Path.GetFileName(x).StartsWith(items[i].equipmentId + "_", StringComparison.OrdinalIgnoreCase)).ToArray();
            Require(matches.Length <= 1, "Ambiguous existing equipment artwork: " + items[i].equipmentId);
            row.FindPropertyRelative("texture").objectReferenceValue = matches.Length == 1 ? AssetDatabase.LoadAssetAtPath<Texture>(matches[0]) : null;
        }
        config.ApplyModifiedPropertiesWithoutUndo();
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Library scene save failed.");
        Debug.Log("[MechaAR Phase6] LIBRARY PATCH PASSED: existing canvas, template, database, other pages and AR preserved.");
    }

    static void PolishCard(EquipmentLibraryCard card)
    {
        var rect = (RectTransform)card.transform; FixedHeight(rect, 250); Rounded(rect, Card, 28);
        var index = Find(rect, "IndexText"); Anchor(index, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(24, -75), new Vector2(174, 75));
        var frame = Node(rect, "EquipmentThumbnailFrame"); Anchor(frame, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(24, -75), new Vector2(174, 75));
        var thumbnail = Node(frame, "EquipmentThumbnail"); Anchor(thumbnail, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var image = Get<RawImage>(thumbnail); image.raycastTarget = false;
        var aspect = Get<AspectRatioFitter>(thumbnail); aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 1;
        var binding = new SerializedObject(Get<LibraryCardArtwork>(rect)); Set(binding, "image", image); Set(binding, "aspectRatio", aspect); Set(binding, "fallback", index.gameObject); binding.ApplyModifiedPropertiesWithoutUndo();
        var name = Find(rect, "NameText").GetComponent<TMP_Text>(); Style(name, 32, Ink); Top(name.rectTransform, 202, -58, 20, 98);
        var thai = Find(rect, "ThaiNameText").GetComponent<TMP_Text>(); Style(thai, 28, Ink); Top(thai.rectTransform, 202, -58, 118, 62);
        var category = Find(rect, "CategoryText").GetComponent<TMP_Text>(); Style(category, 23, Muted); Top(category.rectTransform, 202, -58, 184, 60);
        card.gameObject.SetActive(false);
    }
    static void MoveText(Transform page, Transform parent, string name, string value, float size, Color color, float top, float height)
    { var text = Find(page, name).GetComponent<TMP_Text>(); text.transform.SetParent(parent, false); Style(text, size, color); text.text = value; Top(text.rectTransform, 0, 0, top, height); var layout = text.GetComponent<LayoutElement>(); if (layout != null) layout.ignoreLayout = true; }
    static TMP_Text Text(Transform parent, string name, string value, float size, Color color)
    { var text = Get<TextMeshProUGUI>(Node(parent, name)); Style(text, size, color); text.text = value; return text; }
    static void Style(TMP_Text text, float size, Color color)
    { text.font = font; text.fontSize = size; text.enableAutoSizing = false; text.color = color; text.richText = false; text.raycastTarget = false; text.lineSpacing = 0; text.alignment = TextAlignmentOptions.MidlineLeft; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis; }
    static RoundedPanelGraphic Rounded(Component component, Color color, float radius)
    { var graphic = Get<RoundedPanelGraphic>(component); graphic.color = color; var s = new SerializedObject(graphic); s.FindProperty("radius").floatValue = radius; s.ApplyModifiedPropertiesWithoutUndo(); return graphic; }
    static void Top(RectTransform rect, float left, float right, float top, float height)
    { Anchor(rect, new Vector2(0, 1), Vector2.one, new Vector2(left, -top - height), new Vector2(right, -top)); }
    static void FixedHeight(RectTransform rect, float height)
    { var layout = Get<LayoutElement>(rect); layout.minHeight = layout.preferredHeight = height; layout.flexibleHeight = 0; }
    static T Get<T>(Component component) where T : Component { return component.GetComponent<T>() ?? component.gameObject.AddComponent<T>(); }
    static RectTransform Node(Transform parent, string name)
    { var existing = parent.Find(name); if (existing != null) return (RectTransform)existing; var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer; Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); return rect; }
    static RectTransform Find(Transform parent, string name)
    { var matches = parent.GetComponentsInChildren<RectTransform>(true).Where(x => x.name == name).ToArray(); Require(matches.Length == 1, "Expected one " + name + ", found " + matches.Length); return matches[0]; }
    static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    { rect.localScale = Vector3.one; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high; }
    static void Set(SerializedObject config, string field, UnityEngine.Object value)
    { var property = config.FindProperty(field); Require(property != null, "Missing runtime field " + field); property.objectReferenceValue = value; }
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
