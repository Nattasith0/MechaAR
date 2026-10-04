using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit, additive patch of the user's current scene. Never invokes any previous installer.</summary>
public static class MechaARPhase4Polish
{
    const string ScenePath = "Assets/Scenes/MechaAR_Main.unity";
    static readonly Color Ink = Hex("142B29"), Lime = Hex("D4F268"), Ivory = Hex("F4F5ED"), Sage = Hex("DCE8DF"), Muted = Hex("52675D");
    static TMP_FontAsset font;

    [MenuItem("MechaAR/Phase 4/Polish existing UI")]
    public static void Apply()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode before applying UI polish.");
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            Require(!EditorSceneManager.GetSceneAt(i).isDirty, "Save pending scene changes first.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var manager = UnityEngine.Object.FindFirstObjectByType<MechaARUIManager>(FindObjectsInactive.Include);
        var info = UnityEngine.Object.FindFirstObjectByType<EquipmentInfoUI>(FindObjectsInactive.Include);
        var database = UnityEngine.Object.FindFirstObjectByType<EquipmentDatabase>(FindObjectsInactive.Include);
        Require(manager != null && info != null && database != null, "Existing scene UI/database required.");
        var tutorial = Find(manager.transform, "TutorialPage");
        var about = Find(manager.transform, "AboutPage");
        var artwork = Find(manager.transform, "DetailArtwork");
        var reset = Find(artwork, "Reset3DButton").GetComponent<Button>();
        Require(reset != null && reset.onClick.GetPersistentEventCount() > 0, "Existing reset callback missing.");
        Require(Enumerable.Range(0, reset.onClick.GetPersistentEventCount()).Any(i => reset.onClick.GetPersistentMethodName(i) == "ResetView" && reset.onClick.GetPersistentTarget(i) is Equipment3DViewer), "Reset must target the existing viewer.");
        font = Find(tutorial, "TutorialHeadline").GetComponent<TMP_Text>().font;
        Require(font != null, "Existing Thai font required.");
        var infoConfig = new SerializedObject(info);
        foreach (string field in new[] { "expandButton", "reopenButton", "expandButtonText", "collapsedHeight", "expandedHeightFraction", "scanGuide" })
            Require(infoConfig.FindProperty(field) != null, "Install coordinated UI runtime update first: " + field);
        // Each explicit invocation backs up the exact input scene outside Assets.
        string backup = ".agent-system/tasks/MECHA-PHASE4/backups/ui-patch/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/MechaAR_Main.unity");
        File.Copy(ScenePath + ".meta", backup + "/MechaAR_Main.unity.meta");
        FixReset(reset);
        PolishTutorial(tutorial);
        PolishAbout(about, database);
        PolishScanner(info, infoConfig);
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "UI scene save failed.");
        Debug.Log("[MechaAR Phase4] UI PATCH PASSED: existing Home, viewer, navigation, database and AR objects retained.");
    }

    static void FixReset(Button button)
    {
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.sizeDelta = new Vector2(80, 80);
        rect.anchoredPosition = new Vector2(-20, 20);
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();
        Get<LayoutElement>(rect).ignoreLayout = true;
        // Keep the existing Image and callback; its sliced sprite already supports rounded corners.
        button.targetGraphic.color = Lime;
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
        var icon = Node(rect, "ResetArrowIcon");
        Anchor(icon, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
        var graphic = Get<ResetViewIcon>(icon); graphic.color = Ink; graphic.raycastTarget = false;
    }

    static void PolishTutorial(Transform page)
    {
        var content = Find(page, "TutorialScroll").GetComponent<ScrollRect>().content;
        Vertical(content, 22, new RectOffset(0, 0, 8, 36));
        var headline = Find(page, "TutorialHeadline").GetComponent<TMP_Text>();
        TextStyle(headline, 52, Ink); Natural(headline); headline.transform.SetSiblingIndex(0);
        for (int i = 1; i <= 4; i++)
        {
            string number = i.ToString("00");
            var heading = Find(page, "Step" + number + "Heading").GetComponent<TMP_Text>();
            var body = Find(page, "Step" + number + "Body").GetComponent<TMP_Text>();
            var card = Node(content, "Phase4Step" + number);
            Get<RoundedPanelGraphic>(card).color = i % 2 == 1 ? Sage : Color.white;
            Get<RoundedPanelGraphic>(card).raycastTarget = false;
            Vertical(card, 14, new RectOffset(28, 28, 24, 28));
            heading.transform.SetParent(card, false); body.transform.SetParent(card, false);
            heading.transform.SetSiblingIndex(0); body.transform.SetSiblingIndex(1);
            TextStyle(heading, 35, Ink); heading.fontStyle = FontStyles.Bold; Natural(heading);
            TextStyle(body, 33, Muted); Natural(body);
            if (i == 2) body.text = body.text.Replace("ปุ่ม Reset", "ปุ่มลูกศรวน");
            card.SetSiblingIndex(i);
        }
        var help = Find(page, "PermissionHelp").GetComponent<TMP_Text>();
        TextStyle(help, 30, Muted); Natural(help); help.transform.SetAsLastSibling();
        Find(page, "TutorialScanButton").SetAsLastSibling();
        // Navigation and original step text are reused, including user-authored examples.
    }

    static void PolishAbout(Transform page, EquipmentDatabase database)
    {
        var content = Find(page, "AboutScroll").GetComponent<ScrollRect>().content;
        Vertical(content, 20, new RectOffset(0, 0, 8, 36));
        int index = 0;
        var brand = Find(page, "AboutBrand").GetComponent<TMP_Text>();
        TextStyle(brand, 72, Ink); Natural(brand); Order(brand.transform, content, ref index);
        var tagline = Find(page, "AboutTagline").GetComponent<TMP_Text>();
        tagline.text = "Interactive 3D & AR\nEngineering Learning"; TextStyle(tagline, 38, Muted); Natural(tagline); Order(tagline.transform, content, ref index);
        var concept = Section(content, "Phase4Concept", "01  /  แนวคิดโครงการ"); Order(concept, content, ref index);
        var description = Find(page, "AboutDescription").GetComponent<TMP_Text>();
        description.transform.SetParent(concept, false);
        description.text = "MechaAR เป็นแอปพลิเคชันเพื่อการเรียนรู้ด้านวิศวกรรมและอิเล็กทรอนิกส์ ผ่านโมเดล 3D ที่หมุนสำรวจและอ่านหลักการทำงานได้โดยไม่ต้องเปิดกล้อง\n\nเมื่อเลือกใช้ Augmented Reality ผู้ใช้สามารถส่องภาพเป้าหมายเพื่อแสดงโมเดลในสภาพแวดล้อมจริง ช่วยให้เข้าใจอุปกรณ์ได้ง่ายขึ้นผ่านการสำรวจและมีปฏิสัมพันธ์";
        TextStyle(description, 32, Ink); Natural(description);
        var features = Section(content, "Phase4Features", "02  /  เรียนรู้ได้หลายมุมมอง"); Order(features, content, ref index);
        Body(features, "FeaturesBody", "• สำรวจโมเดล 3D แบบโต้ตอบ\n• หมุนและคืนมุมมองเริ่มต้น\n• แสดงโมเดล AR บนภาพเป้าหมาย\n• คลังข้อมูลและเนื้อหาภาษาไทย\n• อ่านข้อมูลอุปกรณ์ได้แบบออฟไลน์");
        var collection = Section(content, "Phase4Collection", "03  /  คอลเลกชันอุปกรณ์"); Order(collection, content, ref index);
        Body(collection, "CollectionBody", string.Join("\n", database.Equipments.Where(x => x != null).Select((x, i) => (i + 1).ToString("00") + "  " + x.equipmentName)));
        var technology = Section(content, "Phase4Technology", "04  /  เทคโนโลยีที่ใช้"); Order(technology, content, ref index);
        Body(technology, "TechnologyBody", "Unity 6 • C# • Universal Render Pipeline\nAR Foundation 6 • Google ARCore\nTextMeshPro • glTFast");
        var credits = Section(content, "Phase4Credits", "05  /  เครดิตโมเดล 3D"); Order(credits, content, ref index);
        var oldTitle = Find(page, "CreditsTitle").GetComponent<TMP_Text>();
        oldTitle.text = "Model Credits & Attribution"; oldTitle.transform.SetParent(credits, false); TextStyle(oldTitle, 27, Muted); Natural(oldTitle);
        var oldCredits = Find(page, "CreditsModels").GetComponent<TMP_Text>();
        oldCredits.transform.SetParent(credits, false); TextStyle(oldCredits, 31, Ink); Natural(oldCredits);
        // Original attribution and transformation disclosure retained verbatim.
        MoveLink(page, credits, "StepperSourceButton"); MoveLink(page, credits, "ArduinoSourceButton");
        Body(credits, "ServoCredit", "Servo Motor SG90\nโดย peddintiudaykiran176 / Sketchfab\nCC BY 4.0");
        Link(credits, "ServoSourceButton", "แหล่งที่มา Servo Motor SG90", "https://sketchfab.com/3d-models/servo-motor-sg-90-527862090927476fbb1f525b1ba93046");
        Body(credits, "HCSR04Credit", "HC-SR04 Ultrasonic Sensor\nโดย peddintiudaykiran176 / Sketchfab\nCC BY 4.0");
        Link(credits, "HCSR04SourceButton", "แหล่งที่มา HC-SR04", "https://sketchfab.com/3d-models/hc-sr04-e8a6adcef8fd4f45bf27b8d7718ed489");
        var licenses = Section(content, "Phase4Licenses", "06  /  สัญญาอนุญาตและตัวอักษร"); Order(licenses, content, ref index);
        Body(licenses, "LicenseSummary",
    "โมเดล 3D ที่ใช้ใน MechaAR มาจากผู้สร้างภายนอก โดยมีการปรับขนาดและตำแหน่งเพื่อแสดงผลในแอป กรุณาตรวจสอบเครดิตและสัญญาอนุญาตของแต่ละโมเดลจากแหล่งที่มาก่อนนำไปเผยแพร่");
        MoveLink(page, licenses, "LicenseButton");
        var fontCredit = Find(page, "FontCredit").GetComponent<TMP_Text>(); fontCredit.transform.SetParent(licenses, false); TextStyle(fontCredit, 29, Muted); Natural(fontCredit);
        Link(licenses, "FontLicenseButton", "อ่าน SIL Open Font License 1.1", "https://openfontlicense.org/open-font-license-official-text/");
        var versionSection = Section(content, "Phase4Version", "07  /  เวอร์ชันแอปพลิเคชัน"); Order(versionSection, content, ref index);
        var version = Find(page, "AboutVersion").GetComponent<TMP_Text>(); version.transform.SetParent(versionSection, false); version.text = "MechaAR  " + Application.version; TextStyle(version, 32, Ink); Natural(version);
        var privacy = Find(page, "AboutPrivacy").GetComponent<TMP_Text>(); privacy.transform.SetParent(versionSection, false); TextStyle(privacy, 30, Muted); Natural(privacy);
    }

    static void PolishScanner(EquipmentInfoUI info, SerializedObject config)
    {
        var safe = Find(info.transform, "SafeArea");
        var sheet = Find(safe, "EquipmentInfoPanel");
        Get<RoundedPanelGraphic>(sheet).color = Ink;
        Anchor(sheet, Vector2.zero, new Vector2(1, 0), new Vector2(24, 24), new Vector2(-24, 324));
        var name = Find(sheet, "EquipmentNameText").GetComponent<TMP_Text>(); TextStyle(name, 38, Ivory);
        Anchor(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(28, -112), new Vector2(-158, -22));
        name.alignment = TextAlignmentOptions.MidlineLeft;
        var type = Find(sheet, "EquipmentTypeText").GetComponent<TMP_Text>(); TextStyle(type, 28, Sage);
        Anchor(type.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(28, -190), new Vector2(-28, -118));
        var close = Find(sheet, "CloseButton"); Anchor(close, Vector2.one, Vector2.one, new Vector2(-136, -136), new Vector2(-24, -24));
        Get<RoundedPanelGraphic>(close).color = Sage;
        var closeLabel = close.GetComponentInChildren<TMP_Text>(true); closeLabel.text = "ปิด"; TextStyle(closeLabel, 30, Ink); closeLabel.alignment = TextAlignmentOptions.Center;
        var expand = Button(sheet, "ExpandInfoButton", "อ่านรายละเอียด", Lime, Ink);
        Anchor((RectTransform)expand.transform, new Vector2(0, 1), Vector2.one, new Vector2(28, -292), new Vector2(-28, -196));
        var scroll = Find(sheet, "DetailsScroll").GetComponent<ScrollRect>();
        Anchor((RectTransform)scroll.transform, Vector2.zero, Vector2.one, new Vector2(28, 28), new Vector2(-28, -316));
        foreach (var text in scroll.GetComponentsInChildren<TMP_Text>(true)) TextStyle(text, text.name == "SectionHeading" ? 32 : 32, text.name == "SectionHeading" ? Lime : Ivory);
        var reopen = Button(safe, "ReopenInfoButton", "ดูข้อมูลอุปกรณ์", Lime, Ink);
        Anchor((RectTransform)reopen.transform, Vector2.zero, new Vector2(1, 0), new Vector2(80, 28), new Vector2(-80, 140));
        reopen.gameObject.SetActive(false);
        var top = Find(safe, "TopBar"); Get<RoundedPanelGraphic>(top).color = Ink;
        var arStatus = Find(top, "ARStatusText").GetComponent<TMP_Text>(); TextStyle(arStatus, 24, Sage); arStatus.alignment = TextAlignmentOptions.MidlineRight;
        var scanPanel = Find(safe, "ScanStatusPanel"); Get<RoundedPanelGraphic>(scanPanel).color = Ink;
        Anchor(scanPanel, new Vector2(0, 1), Vector2.one, new Vector2(80, -294), new Vector2(-80, -166));
        var scanText = Find(scanPanel, "ScanStatusText").GetComponent<TMP_Text>(); TextStyle(scanText, 31, Ivory); scanText.alignment = TextAlignmentOptions.Center;
        var guide = Node(safe, "ScanGuide");
        Anchor(guide, new Vector2(.5f, .53f), new Vector2(.5f, .53f), new Vector2(-225, -200), new Vector2(225, 200));
        for (int i = 0; i < 4; i++)
        {
            bool right = i % 2 == 1, upper = i > 1;
            Vector2 corner = new Vector2(right ? 1 : 0, upper ? 1 : 0);
            var horizontal = Node(guide, "Corner" + i + "Horizontal");
            Anchor(horizontal, corner, corner, new Vector2(right ? -62 : 0, upper ? -4 : 0), new Vector2(right ? 0 : 62, upper ? 0 : 4));
            var vertical = Node(guide, "Corner" + i + "Vertical");
            Anchor(vertical, corner, corner, new Vector2(right ? -4 : 0, upper ? -62 : 0), new Vector2(right ? 0 : 4, upper ? 0 : 62));
            foreach (var line in new[] { horizontal, vertical }) { var image = Get<Image>(line); image.color = Lime; image.raycastTarget = false; }
        }
        var hint = Text(guide, "ScanGuideHint", "ส่องภาพเป้าหมายของอุปกรณ์", 28, Ivory);
        Anchor(hint.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(-120, -82), new Vector2(120, -14)); hint.alignment = TextAlignmentOptions.Center;
        guide.SetSiblingIndex(scanPanel.GetSiblingIndex() + 1); sheet.SetAsLastSibling(); reopen.transform.SetAsLastSibling();
        Set(config, "expandButton", expand); Set(config, "reopenButton", reopen); Set(config, "expandButtonText", expand.GetComponentInChildren<TMP_Text>()); Set(config, "scanGuide", guide.gameObject);
        config.FindProperty("collapsedHeight").floatValue = 300;
        config.FindProperty("expandedHeightFraction").floatValue = .56f;
        config.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform Section(Transform parent, string name, string title)
    {
        var section = Node(parent, name); Vertical(section, 18, new RectOffset(26, 26, 24, 28));
        var graphic = Get<RoundedPanelGraphic>(section); graphic.color = Sage; graphic.raycastTarget = false;
        var heading = Text(section, "SectionTitle", title, 34, Ink); heading.fontStyle = FontStyles.Bold; Natural(heading); heading.transform.SetAsFirstSibling();
        return section;
    }
    static void Body(Transform parent, string name, string value) { var t = Text(parent, name, value, 32, Ink); Natural(t); }
    static void MoveLink(Transform page, Transform parent, string name) { var link = Find(page, name); link.SetParent(parent, false); FixedHeight(link, 100); var graphic = link.GetComponent<RoundedPanelGraphic>(); if (graphic != null) graphic.color = Ivory; foreach (var text in link.GetComponentsInChildren<TMP_Text>()) { TextStyle(text, 29, Ink); text.alignment = TextAlignmentOptions.Center; } }
    static void Link(Transform parent, string name, string label, string url)
    {
        var button = Button(parent, name, label + "   >", Ivory, Ink); FixedHeight((RectTransform)button.transform, 100);
        if (button.onClick.GetPersistentEventCount() == 0) UnityEventTools.AddStringPersistentListener(button.onClick, Application.OpenURL, url);
        button.GetComponentInChildren<TMP_Text>().fontSize = 29;
    }
    static Button Button(Transform parent, string name, string label, Color background, Color foreground)
    {
        var rect = Node(parent, name); var graphic = Get<RoundedPanelGraphic>(rect); graphic.color = background;
        var button = Get<Button>(rect); button.targetGraphic = graphic;
        var colors = button.colors; colors.pressedColor = new Color(.78f, .86f, .74f); colors.fadeDuration = .1f; button.colors = colors;
        var text = Text(rect, "Label", label, 31, foreground); Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 8), new Vector2(-16, -8)); text.alignment = TextAlignmentOptions.Center;
        return button;
    }
    static TMP_Text Text(Transform parent, string name, string value, float size, Color color)
    { var rect = Node(parent, name); var t = Get<TextMeshProUGUI>(rect); if (t.font == null) t.font = font; t.text = value; TextStyle(t, size, color); return t; }
    static void TextStyle(TMP_Text text, float size, Color color)
    { text.fontSize = size; text.enableAutoSizing = false; text.color = color; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow; text.alignment = TextAlignmentOptions.TopLeft; text.lineSpacing = 7; text.raycastTarget = false; text.richText = false; }
    static void Natural(TMP_Text text)
    { var layout = text.GetComponent<LayoutElement>(); if (layout != null) { layout.minHeight = -1; layout.preferredHeight = -1; layout.flexibleHeight = -1; layout.ignoreLayout = false; } }
    static void FixedHeight(RectTransform rect, float height) { var layout = Get<LayoutElement>(rect); layout.minHeight = layout.preferredHeight = height; layout.flexibleHeight = 0; }
    static void Vertical(RectTransform rect, float spacing, RectOffset padding)
    { var group = Get<VerticalLayoutGroup>(rect); group.spacing = spacing; group.padding = padding; group.childControlWidth = group.childControlHeight = true; group.childForceExpandWidth = true; group.childForceExpandHeight = false; Get<ContentSizeFitter>(rect).verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
    static void Order(Transform child, Transform parent, ref int index) { child.SetParent(parent, false); child.SetSiblingIndex(index++); }
    static T Get<T>(Component component) where T : Component { return component.GetComponent<T>() ?? component.gameObject.AddComponent<T>(); }
    static RectTransform Node(Transform parent, string name)
    { var existing = parent.Find(name); if (existing != null) return (RectTransform)existing; var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer; Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); return rect; }
    static RectTransform Find(Transform parent, string name)
    { var matches = parent.GetComponentsInChildren<RectTransform>(true).Where(x => x.name == name).ToArray(); Require(matches.Length == 1, "Expected exactly one existing " + name + ", found " + matches.Length); return matches[0]; }
    static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high; }
    static void Set(SerializedObject config, string field, UnityEngine.Object value) { config.FindProperty(field).objectReferenceValue = value; }
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
