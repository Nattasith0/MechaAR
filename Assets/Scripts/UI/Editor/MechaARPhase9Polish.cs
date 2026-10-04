using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit UI-only patch of the current V1 scene, with no previous installer dependency.</summary>
public static class MechaARPhase9Polish
{
    const string ScenePath = "Assets/Scenes/MechaAR_Main.unity";
    static readonly Color Ink = Hex("142B29"), Pale = Hex("E1ECE6"), Muted = Hex("52675D"), Cream = Hex("F4F5ED");

    [MenuItem("MechaAR/Phase 9/Polish current V1 UI")]
    public static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode first.");
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            Require(!EditorSceneManager.GetSceneAt(i).isDirty, "Save pending scene changes first.");
        string backup = ".agent-system/tasks/MECHA-PHASE9/snapshots/ui-patch-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/MechaAR_Main.unity");
        File.Copy(ScenePath + ".meta", backup + "/MechaAR_Main.unity.meta");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var manager = UnityEngine.Object.FindFirstObjectByType<MechaARUIManager>(FindObjectsInactive.Include);
        var info = UnityEngine.Object.FindFirstObjectByType<EquipmentInfoUI>(FindObjectsInactive.Include);
        Require(manager != null && info != null, "Existing application and scanner UI required.");
        var config = new SerializedObject(manager);
        var database = (EquipmentDatabase)config.FindProperty("equipmentDatabase").objectReferenceValue;
        Require(database != null, "Existing EquipmentDatabase required.");
        foreach (string field in new[] { "libraryPage", "tutorialPage", "aboutPage" })
            Header(((GameObject)config.FindProperty(field).objectReferenceValue).transform);
        Library(config);
        Tutorial(((GameObject)config.FindProperty("tutorialPage").objectReferenceValue).transform);
        About(((GameObject)config.FindProperty("aboutPage").objectReferenceValue).transform, database);
        Scanner(((GameObject)config.FindProperty("scannerPage").objectReferenceValue).transform, info);
        // Home, Detail including the user-fixed Reset3DButton, nav callbacks and runtime controllers are untouched.
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Could not save current scene.");
        Debug.Log("[MechaAR Phase9] UI POLISH PASSED: existing Scene and functional references preserved.");
    }

    static void Header(Transform page)
    {
        var header = Find(page, "Header");
        Anchor(header, new Vector2(0, 1), Vector2.one, new Vector2(46, -158), new Vector2(-46, -20));
        var back = Find(header, "BackButton");
        // Keep the original 110 x 138 hit region; inset only its visible surface and icon.
        var button = back.GetComponent<Button>();
        Require(button != null && button.onClick.GetPersistentEventCount() > 0, "Back navigation binding missing.");
        var original = back.GetComponent<RoundedPanelGraphic>();
        original.color = Color.clear;
        var surface = Node(back, "BackVisual");
        Anchor(surface, Vector2.zero, Vector2.one, new Vector2(7, 21), new Vector2(-7, -21));
        var graphic = Get<RoundedPanelGraphic>(surface); graphic.color = Pale; graphic.raycastTarget = false; Radius(graphic, 24);
        button.targetGraphic = graphic;
        foreach (var text in back.GetComponentsInChildren<TMP_Text>(true)) text.gameObject.SetActive(false);
        Chevron(surface, "Chevron", true, Ink, 20, 32);
    }

    static void Library(SerializedObject config)
    {
        var template = (EquipmentLibraryCard)config.FindProperty("libraryCardTemplate").objectReferenceValue;
        Require(template != null, "Existing LibraryCardTemplate required.");
        var artwork = template.GetComponent<LibraryCardArtwork>();
        Require(artwork != null, "Existing artwork binding required.");
        var frame = Find(template.transform, "EquipmentThumbnailFrame");
        Anchor(frame, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(24, -75), new Vector2(174, 75));
        var artworkConfig = new SerializedObject(artwork);
        var crops = artworkConfig.FindProperty("artworkCrops");
        Require(crops != null, "UI-only artwork crop fields required.");
        var entries = config.FindProperty("libraryArtwork");
        crops.arraySize = entries.arraySize;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var texture = entries.GetArrayElementAtIndex(i).FindPropertyRelative("texture").objectReferenceValue as Texture;
            var crop = crops.GetArrayElementAtIndex(i);
            crop.FindPropertyRelative("texture").objectReferenceValue = texture;
            crop.FindPropertyRelative("uv").rectValue = AlphaBounds(texture);
        }
        artworkConfig.ApplyModifiedPropertiesWithoutUndo();
        Find(template.transform, "Arrow").gameObject.SetActive(false);
        var arrow = Chevron(template.transform, "NavigationChevron", false, Ink, 16, 28);
        Anchor(arrow, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-41, -14), new Vector2(-25, 14));
    }

    static Rect AlphaBounds(Texture texture)
    {
        if (texture == null) return new Rect(0, 0, 1, 1);
        string path = AssetDatabase.GetAssetPath(texture);
        if (!File.Exists(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return new Rect(0, 0, 1, 1);
        var readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Require(ImageConversion.LoadImage(readable, File.ReadAllBytes(path)), "Could not inspect artwork: " + path);
            var pixels = readable.GetPixels32();
            int minX = readable.width, minY = readable.height, maxX = -1, maxY = -1;
            for (int y = 0; y < readable.height; y++)
                for (int x = 0; x < readable.width; x++)
                    if (pixels[y * readable.width + x].a > 8)
                    { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
            if (maxX < minX) return new Rect(0, 0, 1, 1);
            // Equal transparent breathing space around each device, preserving its aspect ratio.
            int padding = Mathf.CeilToInt(Mathf.Max(maxX - minX + 1, maxY - minY + 1) * .07f);
            minX = Math.Max(0, minX - padding); minY = Math.Max(0, minY - padding);
            maxX = Math.Min(readable.width - 1, maxX + padding); maxY = Math.Min(readable.height - 1, maxY + padding);
            return new Rect((float)minX / readable.width, (float)minY / readable.height,
                (float)(maxX - minX + 1) / readable.width, (float)(maxY - minY + 1) / readable.height);
        }
        finally { UnityEngine.Object.DestroyImmediate(readable); }
    }

    static void Tutorial(Transform page)
    {
        PolishScroll(page, "TutorialScroll");
        string[] headings = { "01  /  เลือกอุปกรณ์จากคลัง", "02  /  สำรวจใน 3D Viewer", "03  /  ส่องภาพด้วย AR Scanner", "04  /  หมุนและซูมโมเดล AR" };
        string[] bodies = {
            "เปิดคลังอุปกรณ์ ค้นหาชื่อภาษาไทย ภาษาอังกฤษ หรือรหัสอุปกรณ์ แล้วเลือกการ์ดเพื่ออ่านข้อมูลและเปิดโมเดล 3D",
            "ลากนิ้วบนโมเดลเพื่อหมุนดูรอบด้าน ใช้ Reset View (ปุ่มลูกศรวน) เพื่อคืนมุมมองเริ่มต้น โดยไม่ต้องเปิดกล้อง",
            "แตะเริ่มสแกนและอนุญาตการใช้กล้อง ส่องภาพเป้าหมายของอุปกรณ์ให้มีแสงเพียงพอ เมื่อพบภาพ โมเดล 3D จะปรากฏ ระบบใช้ภาพเป้าหมายที่กำหนด ไม่ใช่การตรวจจับวัตถุทั่วไป",
            "เมื่อพบโมเดล ลากนิ้วเพื่อหมุน และใช้สองนิ้วบีบหรือกาง (Pinch) เพื่อซูม กด “คืนมุมมอง” เพื่อคืนการหมุนและขนาดเริ่มต้น แตะอ่านรายละเอียดเพื่อดูข้อมูลอุปกรณ์"
        };
        for (int i = 0; i < 4; i++)
        {
            string number = (i + 1).ToString("00");
            Find(page, "Step" + number + "Heading").GetComponent<TMP_Text>().text = headings[i];
            Find(page, "Step" + number + "Body").GetComponent<TMP_Text>().text = bodies[i];
            PolishTextCard(Find(page, "Phase4Step" + number));
        }
        // Preserve the original Camera Permission / Google Play Services for AR guidance.
    }

    static void About(Transform page, EquipmentDatabase database)
    {
        PolishScroll(page, "AboutScroll");
        var equipments = database.Equipments.Where(x => x != null).Distinct().ToArray();
        Find(page, "CollectionBody").GetComponent<TMP_Text>().text = "อุปกรณ์ในคลัง " + equipments.Length + " รายการ\n\n" +
            string.Join("\n", equipments.Select((x, i) => (i + 1).ToString("00") + "  " + x.equipmentName));
        foreach (var card in Find(page, "AboutScroll").GetComponent<ScrollRect>().content.Cast<Transform>())
            if (card.name.StartsWith("Phase4", StringComparison.Ordinal)) PolishTextCard((RectTransform)card);
        foreach (var button in page.GetComponentsInChildren<Button>(true))
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null || !label.text.TrimEnd().EndsWith(">", StringComparison.Ordinal)) continue;
            label.text = label.text.TrimEnd().TrimEnd('>').TrimEnd();
            label.rectTransform.offsetMin = new Vector2(20, label.rectTransform.offsetMin.y);
            label.rectTransform.offsetMax = new Vector2(-50, label.rectTransform.offsetMax.y);
            var chevron = Chevron(button.transform, "NavigationChevron", false, Ink, 16, 28);
            Anchor(chevron, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-36, -14), new Vector2(-20, 14));
        }
    }

    static void PolishScroll(Transform page, string name)
    {
        var scroll = Find(page, name).GetComponent<ScrollRect>();
        // The viewport ends above the unchanged nav; padding lets the final card fully clear it.
        Anchor((RectTransform)scroll.transform, Vector2.zero, Vector2.one, new Vector2(46, 174), new Vector2(-46, -174));
        var group = scroll.content.GetComponent<VerticalLayoutGroup>();
        Require(group != null, "Existing vertical content layout required.");
        group.spacing = 22; group.padding.bottom = 48;
        scroll.verticalNormalizedPosition = 1;
    }

    static void PolishTextCard(RectTransform card)
    {
        var group = card.GetComponent<VerticalLayoutGroup>();
        if (group != null) { group.spacing = 16; group.padding = new RectOffset(28, 28, 26, 30); }
        var panel = card.GetComponent<RoundedPanelGraphic>();
        if (panel != null) Radius(panel, 28);
        foreach (var text in card.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<Button>() != null) continue;
            bool heading = text.name.EndsWith("Heading", StringComparison.Ordinal) || text.name == "SectionTitle";
            text.fontSize = heading ? 34 : 32;
            text.lineSpacing = 6;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            var layout = text.GetComponent<LayoutElement>();
            if (layout != null) { layout.minHeight = -1; layout.preferredHeight = -1; layout.flexibleHeight = -1; }
        }
    }

    static void Scanner(Transform page, EquipmentInfoUI info)
    {
        var safe = Find(info.transform, "SafeArea");
        var top = Find(safe, "TopBar");
        Anchor(top, new Vector2(0, 1), Vector2.one, new Vector2(28, -120), new Vector2(-298, -24));
        var title = Find(top, "AppNameText").GetComponent<TMP_Text>(); title.fontSize = 34;
        Anchor(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(24, 12), new Vector2(-230, -12));
        var status = Find(top, "ARStatusText").GetComponent<TMP_Text>(); status.fontSize = 23;
        Anchor(status.rectTransform, new Vector2(.48f, 0), Vector2.one, new Vector2(0, 12), new Vector2(-24, -12));
        var home = Find(page, "ScannerHomeButton");
        // Original 118-high raycast target retained; visible button aligns with the compact header.
        var oldSurface = home.GetComponent<RoundedPanelGraphic>(); oldSurface.color = Color.clear;
        var surface = Node(home, "HomeVisual");
        Anchor(surface, Vector2.zero, Vector2.one, new Vector2(0, 22), Vector2.zero);
        var panel = Get<RoundedPanelGraphic>(surface); panel.color = Ink; panel.raycastTarget = false; Radius(panel, 24);
        home.GetComponent<Button>().targetGraphic = panel;
        surface.SetAsFirstSibling();
        var label = Find(home, "Label").GetComponent<TMP_Text>(); label.text = "หน้าหลัก"; label.fontSize = 30;
        Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(50, 22), new Vector2(-12, 0));
        var back = Chevron(home, "HomeChevron", true, Cream, 16, 28);
        Anchor(back, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(22, -3), new Vector2(38, 25));
        var instruction = Find(safe, "ScanStatusPanel");
        Anchor(instruction, new Vector2(0, 1), Vector2.one, new Vector2(28, -240), new Vector2(-28, -138));
        var instructionText = Find(instruction, "ScanStatusText").GetComponent<TMP_Text>(); instructionText.fontSize = 29;
        Find(safe, "ScanGuideHint").gameObject.SetActive(false);
        // Keep the runtime-managed status text and centered reticle; no replacement status logic.
        var reset = Find(page, "ARResetButton");
        var button = reset.GetComponent<Button>();
        Require(Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i =>
            button.onClick.GetPersistentTarget(i) is ARModelGestureController && button.onClick.GetPersistentMethodName(i) == "ResetCurrent"), "Existing ResetCurrent callback required.");
        reset.anchoredPosition = new Vector2(-28, -264);
        // No callbacks are removed or rebuilt anywhere in this patch.
    }

    static RectTransform Chevron(Transform parent, string name, bool left, Color color, float width, float height)
    {
        var rect = Node(parent, name);
        Anchor(rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-width * .5f, -height * .5f), new Vector2(width * .5f, height * .5f));
        Get<CanvasRenderer>(rect);
        var icon = Get<MechaARChevronGraphic>(rect); icon.color = color; icon.raycastTarget = false;
        var config = new SerializedObject(icon); config.FindProperty("pointLeft").boolValue = left; config.ApplyModifiedPropertiesWithoutUndo();
        return rect;
    }
    static void Radius(RoundedPanelGraphic graphic, float value)
    { var config = new SerializedObject(graphic); config.FindProperty("radius").floatValue = value; config.ApplyModifiedPropertiesWithoutUndo(); }
    static T Get<T>(Component component) where T : Component { return component.GetComponent<T>() ?? component.gameObject.AddComponent<T>(); }
    static RectTransform Node(Transform parent, string name)
    { var existing = parent.Find(name); if (existing != null) return (RectTransform)existing; var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer; return rect; }
    static RectTransform Find(Transform parent, string name)
    { var matches = parent.GetComponentsInChildren<RectTransform>(true).Where(x => x.name == name).ToArray(); Require(matches.Length == 1, "Expected one " + name + ", found " + matches.Length); return matches[0]; }
    static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high; }
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
