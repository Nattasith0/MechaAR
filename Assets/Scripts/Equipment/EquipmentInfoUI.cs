using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

/// <summary>Persistent scene UI driven by equipment tracking transitions.</summary>
public sealed class EquipmentInfoUI : MonoBehaviour
{
    [SerializeField] ARImageTracking imageTracking;
    [SerializeField] EquipmentData[] equipmentCatalog;
    [SerializeField] GameObject equipmentInfoPanel;
    [SerializeField] TMP_Text arStatusText, scanStatusText, equipmentNameText, equipmentTypeText;
    [SerializeField] TMP_Text equipmentDescriptionText, equipmentPrincipleText, equipmentApplicationsText;
    [SerializeField] Button closeButton;
    [SerializeField] ScrollRect detailsScroll;
    [Header("Expandable scanner sheet")]
    [SerializeField] Button expandButton, reopenButton;
    [SerializeField] TMP_Text expandButtonText;
    [SerializeField] GameObject scanGuide;
    [SerializeField] float collapsedHeight = 300f;
    [SerializeField, Range(.3f, .65f)] float expandedHeightFraction = .55f;
    bool subscribed, dismissed, paused, expanded, targetTracked;
    string currentImage;
    static readonly Color Primary = Hex("159BB1"), Panel = Hex("123B50"), Secondary = Hex("B7DAE3");
    public bool IsPanelVisible => equipmentInfoPanel != null && equipmentInfoPanel.activeSelf;
    public bool IsExpanded => IsPanelVisible && expanded;
    void OnEnable()
    {
        ARSession.stateChanged += SessionChanged;
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        if (expandButton != null) expandButton.onClick.AddListener(ToggleExpanded);
        if (reopenButton != null) reopenButton.onClick.AddListener(ReopenPanel);
        Subscribe();
        UpdateARStatus();
    }
    void Start() { Subscribe(); }
    void OnDisable()
    {
        ARSession.stateChanged -= SessionChanged;
        if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
        if (expandButton != null) expandButton.onClick.RemoveListener(ToggleExpanded);
        if (reopenButton != null) reopenButton.onClick.RemoveListener(ReopenPanel);
        if (subscribed && imageTracking != null) imageTracking.EquipmentTrackingChanged -= TrackingChanged;
        subscribed = false;
        HideEquipment();
    }
    void Subscribe()
    {
        if (subscribed || imageTracking == null) return;
        imageTracking.EquipmentTrackingChanged += TrackingChanged;
        subscribed = true;
        TrackingChanged(imageTracking.TargetImageName, imageTracking.IsTrackingTarget);
    }
    void OnApplicationPause(bool isPaused)
    {
        paused = isPaused;
        if (paused) HideEquipment();
        else if (imageTracking != null) TrackingChanged(imageTracking.TargetImageName, imageTracking.IsTrackingTarget);
        UpdateARStatus();
    }
    void SessionChanged(ARSessionStateChangedEventArgs args) { UpdateARStatus(); }
    void UpdateARStatus()
    {
        if (arStatusText == null) return;
        arStatusText.color = Secondary;
        if (paused) { arStatusText.text = "AR paused"; return; }
        switch (ARSession.state)
        {
            case ARSessionState.SessionTracking: arStatusText.text = "AR active"; arStatusText.color = Hex("22C55E"); break;
            case ARSessionState.Unsupported: arStatusText.text = "AR unavailable"; break;
            case ARSessionState.NeedsInstall: arStatusText.text = "AR setup required"; break;
            case ARSessionState.Installing: arStatusText.text = "Installing AR"; break;
            case ARSessionState.SessionInitializing: arStatusText.text = "Starting AR"; break;
            default: arStatusText.text = "Preparing AR"; break;
        }
    }
    void TrackingChanged(string imageName, bool tracked)
    {
        if (!tracked || paused) { HideEquipment(); return; }
        EquipmentData data = FindEquipment(imageName);
        if (data == null) { HideEquipment(); return; }
        ShowEquipment(data);
    }
    EquipmentData FindEquipment(string imageName)
    {
        // Same database as the 3D model selection: no separate list to maintain.
        if (imageTracking != null && imageTracking.Database != null)
            return imageTracking.Database.GetByImageName(imageName);

        // Preserve Phase 2 scenes that have not connected a database yet.
        if (equipmentCatalog == null) return null;
        foreach (var data in equipmentCatalog)
            if (data != null && string.Equals(data.referenceImageName, imageName, StringComparison.Ordinal)) return data;
        return null;
    }
    public void ShowEquipment(EquipmentData data)
    {
        if (data == null) { HideEquipment(); return; }
        if (paused) return;
        bool changed = !targetTracked || !string.Equals(currentImage, data.referenceImageName, StringComparison.Ordinal);
        if (changed) { dismissed = false; expanded = false; }
        targetTracked = true;
        currentImage = data.referenceImageName;
        SetText(scanStatusText, "ตรวจพบอุปกรณ์ · " + data.equipmentName);
        if (scanGuide != null) scanGuide.SetActive(false);
        if (dismissed) { ApplySheet(); return; }
        SetText(equipmentNameText, data.equipmentName);
        SetText(equipmentTypeText, data.thaiName + "  /  " + data.category);
        SetText(equipmentDescriptionText, data.description);
        SetText(equipmentPrincipleText, data.workingPrinciple);
        SetText(equipmentApplicationsText, data.applications == null ? "" : "• " + string.Join("\n• ", data.applications));
        ApplySheet();
        if (changed && detailsScroll != null)
        {
            detailsScroll.StopMovement();
            detailsScroll.verticalNormalizedPosition = 1f;
        }
    }
    public void HideEquipment()
    {
        dismissed = false;
        expanded = false;
        targetTracked = false;
        currentImage = null;
        if (equipmentInfoPanel != null) equipmentInfoPanel.SetActive(false);
        if (reopenButton != null) reopenButton.gameObject.SetActive(false);
        if (scanGuide != null) scanGuide.SetActive(true);
        SetText(scanStatusText, "ส่องกล้องไปยังภาพอุปกรณ์");
    }
    public void ClosePanel()
    {
        dismissed = true;
        ApplySheet();
    }
    public void ToggleExpanded() { SetExpanded(!expanded); }
    public void SetExpanded(bool value)
    {
        if (!targetTracked || dismissed || paused) return;
        expanded = value;
        ApplySheet();
    }
    public void ReopenPanel()
    {
        if (!targetTracked || paused) return;
        dismissed = false;
        expanded = false;
        ApplySheet();
    }
    void OnRectTransformDimensionsChange() { ApplySheet(); }
    void ApplySheet()
    {
        bool visible = targetTracked && !paused && !dismissed;
        if (equipmentInfoPanel != null) equipmentInfoPanel.SetActive(visible);
        if (reopenButton != null) reopenButton.gameObject.SetActive(targetTracked && !paused && dismissed);
        // Older scenes without sheet controls keep their original full information layout.
        if (expandButton == null || equipmentInfoPanel == null) return;
        if (detailsScroll != null) detailsScroll.gameObject.SetActive(expanded);
        SetText(expandButtonText, expanded ? "ย่อข้อมูล" : "อ่านข้อมูลอุปกรณ์");
        var rect = equipmentInfoPanel.transform as RectTransform;
        var parent = rect != null ? rect.parent as RectTransform : null;
        if (parent == null || parent.rect.height <= 0f) return;
        float bottom = rect.offsetMin.y;
        float height = expanded ? parent.rect.height * expandedHeightFraction : collapsedHeight;
        height = Mathf.Min(height, Mathf.Max(0f, parent.rect.height - bottom));
        rect.anchorMax = new Vector2(rect.anchorMax.x, 0f);
        rect.offsetMax = new Vector2(rect.offsetMax.x, bottom + height);
    }
    static void SetText(TMP_Text target, string value) { if (target != null) target.text = value ?? ""; }
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var result); return result; }

    /// <summary>Editor setup entry point; objects and references are saved into the scene.</summary>
    public void BuildUI(ARImageTracking tracker, TMP_FontAsset font, EquipmentData[] catalog)
    {
        if (transform.childCount != 0) throw new InvalidOperationException("BuildUI requires an empty Canvas.");
        imageTracking = tracker;
        equipmentCatalog = catalog;
        var canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        var safe = Rect("SafeArea", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        safe.gameObject.AddComponent<SafeAreaPanel>();
        var top = Card("TopBar", safe, new Vector2(0, 1), Vector2.one, new Vector2(28, -140), new Vector2(-28, -24), Hex("102C3D"));
        Label("AppNameText", top, font, "MechaAR", 46, Color.white, new Vector2(30, 15), new Vector2(-370, -15));
        arStatusText = Label("ARStatusText", top, font, "Preparing AR", 27, Secondary, new Vector2(0, 15), new Vector2(-28, -15));
        arStatusText.rectTransform.anchorMin = new Vector2(.5f, 0);
        arStatusText.alignment = TextAlignmentOptions.MidlineRight;
        var scan = Card("ScanStatusPanel", safe, new Vector2(0, 1), Vector2.one, new Vector2(80, -244), new Vector2(-80, -166), Hex("136F83"));
        scanStatusText = Label("ScanStatusText", scan, font, "ส่องกล้องไปยังภาพอุปกรณ์", 32, Color.white, new Vector2(20, 5), new Vector2(-20, -5));
        scanStatusText.alignment = TextAlignmentOptions.Center;
        var info = Card("EquipmentInfoPanel", safe, Vector2.zero, new Vector2(1, .46f), new Vector2(28, 28), new Vector2(-28, 0), Panel);
        equipmentInfoPanel = info.gameObject;
        var stripe = Card("Accent", info, new Vector2(.43f, 1), new Vector2(.57f, 1), new Vector2(0, -18), new Vector2(0, -10), Primary);
        stripe.GetComponent<Graphic>().raycastTarget = false;
        equipmentNameText = Label("EquipmentNameText", info, font, "Equipment", 43, Color.white, Vector2.zero, Vector2.zero);
        Position(equipmentNameText.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(32, -92), new Vector2(-155, -25));
        equipmentTypeText = Label("EquipmentTypeText", info, font, "", 28, Secondary, Vector2.zero, Vector2.zero);
        Position(equipmentTypeText.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(32, -150), new Vector2(-168, -88));
        var close = Card("CloseButton", info, Vector2.one, Vector2.one, new Vector2(-148, -148), new Vector2(-24, -24), Hex("136F83"));
        closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = close.GetComponent<Graphic>();
        var closeText = Label("Label", close, font, "Close", 25, Color.white, Vector2.zero, Vector2.zero);
        closeText.alignment = TextAlignmentOptions.Center;
        var scroll = Rect("DetailsScroll", info, Vector2.zero, Vector2.one, new Vector2(32, 30), new Vector2(-32, -168));
        detailsScroll = scroll.gameObject.AddComponent<ScrollRect>();
        detailsScroll.horizontal = false;
        detailsScroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = Rect("Viewport", scroll, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24, 0));
        viewport.gameObject.AddComponent<RectMask2D>();
        var hitArea = viewport.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        var content = Rect("Content", viewport, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
        content.pivot = new Vector2(.5f, 1);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.padding = new RectOffset(0, 12, 4, 20);
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Section(content, font, "หน้าที่");
        equipmentDescriptionText = Body(content, font, "EquipmentDescriptionText");
        Section(content, font, "หลักการทำงาน");
        equipmentPrincipleText = Body(content, font, "EquipmentWorkingPrincipleText");
        Section(content, font, "ตัวอย่างการใช้งาน");
        equipmentApplicationsText = Body(content, font, "EquipmentApplicationsText");
        detailsScroll.viewport = viewport;
        detailsScroll.content = content;
        var barRect = Rect("ScrollIndicator", scroll, new Vector2(1, 0), Vector2.one, new Vector2(-8, 0), Vector2.zero);
        var bar = barRect.gameObject.AddComponent<Scrollbar>();
        var handle = Card("Handle", barRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Primary);
        bar.handleRect = handle;
        bar.targetGraphic = handle.GetComponent<Graphic>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        detailsScroll.verticalScrollbar = bar;
        detailsScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        equipmentInfoPanel.SetActive(false);
    }
    static void Section(Transform parent, TMP_FontAsset font, string text)
    {
        var label = Body(parent, font, "SectionHeading");
        label.text = text;
        label.fontSize = 28;
        label.color = Hex("65DAE7");
        label.margin = new Vector4(0, 12, 0, 0);
    }
    static TMP_Text Body(Transform parent, TMP_FontAsset font, string name)
    {
        var text = Label(name, parent, font, "", 31, Color.white, Vector2.zero, Vector2.zero);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.lineSpacing = 10;
        return text;
    }
    static RectTransform Card(string name, Transform parent, Vector2 min, Vector2 max, Vector2 low, Vector2 high, Color color)
    {
        var rect = Rect(name, parent, min, max, low, high);
        rect.gameObject.AddComponent<RoundedPanelGraphic>().color = color;
        return rect;
    }
    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, string value, float size, Color color, Vector2 low, Vector2 high)
    {
        var rect = Rect(name, parent, Vector2.zero, Vector2.one, low, high);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        text.richText = false;
        return text;
    }
    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Position(rect, min, max, low, high);
        return rect;
    }
    static void Position(RectTransform rect, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high;
    }
}
