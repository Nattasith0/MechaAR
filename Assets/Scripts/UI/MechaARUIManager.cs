using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Owns application pages while the existing tracker owns all AR content.</summary>
public sealed class MechaARUIManager : MonoBehaviour
{
    public enum Page { Home, Scanner, Library, Detail, Tutorial, About }
    [SerializeField] EquipmentDatabase equipmentDatabase;
    [SerializeField] GameObject offlineBackdrop;
    [SerializeField] GameObject homePage, scannerPage, libraryPage, detailPage, tutorialPage, aboutPage;
    [SerializeField] EquipmentInfoUI equipmentInfoUI;
    [SerializeField] ARSession arSession;
    [SerializeField] ARCameraManager arCameraManager;
    [SerializeField] RectTransform libraryContent, homePreviewContent;
    [SerializeField] EquipmentLibraryCard libraryCardTemplate, homeCardTemplate;
    [SerializeField] TMP_Text libraryEmptyText, homeEmptyText;
    [SerializeField] TMP_InputField librarySearchInput;
    [SerializeField] TMP_Text libraryResultText;
    [SerializeField] ScrollRect libraryScroll;
    // All, Motor & Actuator, Sensor, Microcontroller & Controller.
    [SerializeField] Button[] libraryCategoryButtons = Array.Empty<Button>();
    [Serializable]
    sealed class LibraryArtworkEntry
    {
        public EquipmentData equipmentData;
        public Texture texture;
    }
    [SerializeField] LibraryArtworkEntry[] libraryArtwork = Array.Empty<LibraryArtworkEntry>();
    [SerializeField] TMP_Text detailNameText, detailThaiNameText, detailCategoryText;
    [SerializeField] TMP_Text detailDescriptionText, detailPrincipleText, detailApplicationsText;
    [SerializeField] ScrollRect detailScroll;
    [SerializeField] Equipment3DViewer equipment3DViewer;
    // Phase 6: Keep a reference to Library cards only. Home cards and AR are untouched.
    sealed class LibraryEntry
    {
        public EquipmentData Data;
        public EquipmentLibraryCard Card;
    }

    readonly List<LibraryEntry> libraryEntries = new List<LibraryEntry>();
    string librarySearch = "";
    string libraryCategory = "";

    readonly List<Page> history = new List<Page>();
    ARRuntimeDiagnostics diagnostics;
    public Page CurrentPage { get; private set; } = Page.Home;
    public EquipmentData SelectedEquipment { get; private set; }
    public bool HasStartedAR { get; private set; }
    public int LibraryVisibleCount { get; private set; }
    public string LibrarySearch => librarySearch;
    public string LibraryCategory => libraryCategory;

    void Awake()
    {
        BuildCatalog();
        if (librarySearchInput != null)
        {
            // Search is session state. Hint text belongs to the TMP placeholder,
            // never the serialized input value used to filter the initial catalog.
            librarySearchInput.SetTextWithoutNotify("");
            librarySearchInput.onValueChanged.AddListener(SetLibrarySearch);
        }
        ApplyPage(Page.Home);
    }

    void OnDestroy()
    {
        if (librarySearchInput != null)
            librarySearchInput.onValueChanged.RemoveListener(SetLibrarySearch);
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) GoBack();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) GoBack();
#endif
    }

    void LateUpdate()
    {
        // Diagnostics is attached by sceneLoaded, after this manager's Awake.
        // Resolve after Start so its existing camera/session references are initialized.
        if (diagnostics != null) return;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            diagnostics = root.GetComponentInChildren<ARRuntimeDiagnostics>(true);
            if (diagnostics != null)
            {
                diagnostics.enabled = CurrentPage == Page.Scanner;
                break;
            }
        }
    }

    public void StartScan()
    {
        if (!HasStartedAR)
        {
            // Only the first scan starts the existing components. Do not reset or
            // recreate the session when returning Home or revisiting the scanner.
            if (arCameraManager != null) arCameraManager.enabled = true;
            if (arSession != null) arSession.enabled = true;
            HasStartedAR = true;
        }
        Navigate(Page.Scanner);
    }

    public void ShowHome() { history.Clear(); SelectedEquipment = null; ApplyPage(Page.Home); }
    public void ShowLibrary() { Navigate(Page.Library); }
    public void ShowTutorial() { Navigate(Page.Tutorial); }
    public void ShowAbout() { Navigate(Page.About); }

    public void ShowEquipment(EquipmentData data)
    {
        if (data == null) return;
        SelectedEquipment = data;
        SetText(detailNameText, data.equipmentName);
        SetText(detailThaiNameText, data.thaiName);
        SetText(detailCategoryText, data.category);
        SetText(detailDescriptionText, data.description);
        SetText(detailPrincipleText, data.workingPrinciple);
        SetText(detailApplicationsText, data.applications == null || data.applications.Length == 0
            ? "ยังไม่มีข้อมูลตัวอย่างการใช้งาน" : "• " + string.Join("\n• ", data.applications));
        Navigate(Page.Detail);
        if (equipment3DViewer != null) equipment3DViewer.Show(data);
        Canvas.ForceUpdateCanvases();
        if (detailScroll != null)
        {
            detailScroll.StopMovement();
            detailScroll.verticalNormalizedPosition = 1f;
        }
    }

    public void GoBack()
    {
        if (CurrentPage == Page.Scanner || history.Count == 0) { ShowHome(); return; }
        int index = history.Count - 1;
        Page previous = history[index];
        history.RemoveAt(index);
        SelectedEquipment = null;
        ApplyPage(previous);
    }

    void Navigate(Page next)
    {
        if (CurrentPage == next) return;
        // Reopening an earlier page unwinds to it instead of stacking duplicates.
        int existing = history.IndexOf(next);
        if (existing >= 0) history.RemoveRange(existing, history.Count - existing);
        else history.Add(CurrentPage);
        ApplyPage(next);
    }

    void ApplyPage(Page page)
    {
        CurrentPage = page;
        if (page != Page.Detail && equipment3DViewer != null) equipment3DViewer.Hide();
        bool scanning = page == Page.Scanner;
        SetActive(offlineBackdrop, !scanning);
        SetActive(homePage, page == Page.Home);
        SetActive(scannerPage, scanning);
        SetActive(libraryPage, page == Page.Library);
        SetActive(detailPage, page == Page.Detail);
        SetActive(tutorialPage, page == Page.Tutorial);
        SetActive(aboutPage, page == Page.About);
        if (equipmentInfoUI != null)
        {
            if (!scanning) equipmentInfoUI.HideEquipment();
            SetActive(equipmentInfoUI.gameObject, scanning);
        }
        if (diagnostics != null) diagnostics.enabled = scanning;
    }

    // Wire the four filter buttons to these methods using Button -> On Click (Dynamic not required).
    public void ShowAllEquipment() { SetLibraryCategory(""); }
    public void ShowMotorEquipment() { SetLibraryCategory("Motor & Actuator"); }
    public void ShowSensorEquipment() { SetLibraryCategory("Sensor"); }
    public void ShowControllerEquipment() { SetLibraryCategory("Microcontroller & Controller"); }

    public void SetLibrarySearch(string value)
    {
        string query = (value ?? "").Trim();
        if (librarySearch == query) return;
        librarySearch = query;
        ApplyLibraryFilters();
        ResetLibraryScroll();
    }

    void SetLibraryCategory(string value)
    {
        string category = value ?? "";
        if (libraryCategory == category) return;
        libraryCategory = category;
        ApplyLibraryFilters();
        ResetLibraryScroll();
    }

    void ApplyLibraryFilters()
    {
        int visible = 0;
        foreach (LibraryEntry entry in libraryEntries)
        {
            EquipmentData data = entry.Data;
            if (data == null || entry.Card == null) continue;

            bool categoryMatches = string.IsNullOrEmpty(libraryCategory) ||
                string.Equals(data.category, libraryCategory, StringComparison.OrdinalIgnoreCase) ||
                (libraryCategory == "Motor & Actuator" &&
                    string.Equals(data.category, "Electric Motor", StringComparison.OrdinalIgnoreCase));
            bool searchMatches = string.IsNullOrEmpty(librarySearch) ||
                ContainsSearch(data.equipmentName, librarySearch) ||
                ContainsSearch(data.thaiName, librarySearch) ||
                ContainsSearch(data.equipmentId, librarySearch) ||
                ContainsSearch(data.category, librarySearch);

            bool show = categoryMatches && searchMatches;
            SetActive(entry.Card.gameObject, show);
            if (show) visible++;
        }

        LibraryVisibleCount = visible;
        SetText(libraryResultText, $"แสดง {visible} จาก {libraryEntries.Count} อุปกรณ์");
        if (libraryEmptyText != null)
        {
            if (visible == 0)
                libraryEmptyText.text = libraryEntries.Count == 0
                    ? "ยังไม่มีอุปกรณ์ในคลัง" : "ไม่พบอุปกรณ์ที่ค้นหา\nลองใช้คำอื่น หรือเลือกหมวดหมู่ทั้งหมด";
            SetActive(libraryEmptyText.gameObject, visible == 0);
        }
        UpdateCategoryButtons();
        if (libraryContent != null) LayoutRebuilder.MarkLayoutForRebuild(libraryContent);
    }

    void ResetLibraryScroll()
    {
        if (libraryScroll == null) return;
        if (libraryContent != null && libraryContent.gameObject.activeInHierarchy)
            LayoutRebuilder.ForceRebuildLayoutImmediate(libraryContent);
        libraryScroll.StopMovement();
        libraryScroll.verticalNormalizedPosition = 1f;
    }

    void UpdateCategoryButtons()
    {
        if (libraryCategoryButtons == null) return;
        int selected = libraryCategory == "Motor & Actuator" ? 1 :
            libraryCategory == "Sensor" ? 2 : libraryCategory == "Microcontroller & Controller" ? 3 : 0;
        Color primary = new Color32(0x14, 0x2B, 0x29, 0xFF);
        Color accent = new Color32(0xD4, 0xF2, 0x68, 0xFF);
        Color card = new Color32(0xE1, 0xEC, 0xE6, 0xFF);
        for (int i = 0; i < libraryCategoryButtons.Length; i++)
        {
            Button button = libraryCategoryButtons[i];
            if (button == null) continue;
            if (button.targetGraphic != null) button.targetGraphic.color = i == selected ? primary : card;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = i == selected ? accent : primary;
        }
    }

    static bool ContainsSearch(string text, string query)
    {
        return !string.IsNullOrEmpty(text) &&
            text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void BuildCatalog()
    {
        if (libraryCardTemplate != null) libraryCardTemplate.gameObject.SetActive(false);
        if (homeCardTemplate != null) homeCardTemplate.gameObject.SetActive(false);
        libraryEntries.Clear();
        int count = 0;
        var seen = new HashSet<EquipmentData>();
        if (equipmentDatabase != null)
        {
            foreach (EquipmentData data in equipmentDatabase.Equipments)
            {
                if (data == null || !seen.Add(data)) continue;
                EquipmentLibraryCard libraryCard = AddCard(libraryCardTemplate, libraryContent, data, count);
                if (libraryCard != null)
                {
                    libraryEntries.Add(new LibraryEntry { Data = data, Card = libraryCard });
                    LibraryCardArtwork artwork = libraryCard.GetComponent<LibraryCardArtwork>();
                    if (artwork != null) artwork.Bind(FindLibraryArtwork(data));
                }
                if (count < 2) AddCard(homeCardTemplate, homePreviewContent, data, count);
                count++;
            }
        }
        ApplyLibraryFilters();
        if (homeEmptyText != null) homeEmptyText.gameObject.SetActive(count == 0);
    }

    EquipmentLibraryCard AddCard(EquipmentLibraryCard template, RectTransform parent, EquipmentData data, int index)
    {
        if (template == null || parent == null) return null;
        var card = Instantiate(template, parent);
        card.name = "EquipmentCard_" + data.equipmentId;
        card.Bind(data, index, ShowEquipment);
        card.gameObject.SetActive(true);
        return card;
    }

    Texture FindLibraryArtwork(EquipmentData data)
    {
        if (libraryArtwork != null)
            foreach (LibraryArtworkEntry entry in libraryArtwork)
                if (entry != null && entry.equipmentData == data) return entry.texture;
        return null;
    }

    static void SetText(TMP_Text target, string value) { if (target != null) target.text = value ?? ""; }
    static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }
}
