using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Unity.XR.CoreUtils.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>Hardware-free integration checks against the installed scene and real AR event types.</summary>
public static class MechaARPhase6Tests
{
    const string Pending = "MechaAR.Phase6.Tests.Pending";
    const string RestoreScene = "MechaAR.Phase6.Tests.RestoreScene";
    static int TestWidth { get { string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-mechaarTestWidth"); return i >= 0 ? int.Parse(args[i + 1]) : 1080; } } static int TestHeight
    {
        get
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-mechaarTestHeight");
            if (index < 0) return 1920;
            if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out int height) || height < 1080 || height > 4096)
                throw new ArgumentException("-mechaarTestHeight requires an integer between 1080 and 4096.");
            return height;
        }
    }
    static string Evidence => SessionState.GetString("MechaAR.Phase6.Evidence", ".agent-system/tasks/MECHA-PHASE6/evidence/" + TestHeight);
    static readonly List<string> Results = new List<string>();
    static IEnumerator checks;
    static double started;
    static ARImageTracking tracker;
    static EquipmentInfoUI ui;
    static MechaARPhase6TestDispatcher dispatcher;
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("MechaAR/Phase 6/Run integration checks")]
    public static void Baseline() { SessionState.SetBool("MechaAR.Phase6.Baseline", true); StartRun(); } public static void RunTests() { SessionState.SetBool("MechaAR.Phase6.Baseline", false); StartRun(); } static void StartRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play Mode before running MechaAR integration checks.");
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ResetRun();
        SessionState.SetString("MechaAR.Phase6.Evidence", ".agent-system/tasks/MECHA-PHASE6/evidence/" + (SessionState.GetBool("MechaAR.Phase6.Baseline", false) ? "before/" : "after/") + TestWidth + "x" + TestHeight + "/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Evidence);
        EditorSceneManager.OpenScene("Assets/Scenes/MechaAR_Main.unity");
        var savedSession = UnityEngine.Object.FindFirstObjectByType<ARSession>(FindObjectsInactive.Include);
        var savedCamera = UnityEngine.Object.FindFirstObjectByType<ARCameraManager>(FindObjectsInactive.Include);
        if (savedSession == null || savedCamera == null || savedSession.enabled || savedCamera.enabled)
            throw new InvalidOperationException("Saved scene must defer ARSession and ARCameraManager until first Scan.");
        SetPortraitGameView();
        // These edits remain unsaved; the actual scene retains all AR managers.
        foreach (var behaviour in UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour.GetType().Namespace == "UnityEngine.XR.ARFoundation" ||
                behaviour.GetType().Name == "ARRuntimeDiagnostics")
                behaviour.enabled = false;
        }
        SessionState.SetBool(Pending, true);
        SessionState.SetBool(RestoreScene, !Application.isBatchMode);
        EditorApplication.playModeStateChanged -= RestoreAfterPlay;
        EditorApplication.playModeStateChanged += RestoreAfterPlay;
        EditorApplication.isPlaying = true;
        Resume();
    }

    [InitializeOnLoadMethod]
    static void Resume()
    {
        if (SessionState.GetBool(RestoreScene, false))
        {
            EditorApplication.playModeStateChanged -= RestoreAfterPlay;
            EditorApplication.playModeStateChanged += RestoreAfterPlay;
        }
        if (!SessionState.GetBool(Pending, false)) return;
        ResetRun();
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void RestoreAfterPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RestoreScene, false)) return;
        SessionState.SetBool(RestoreScene, false);
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= RestoreAfterPlay;
        // User changes were saved or explicitly discarded before tests started.
        // Reload the saved scene so disabled test-only managers cannot be saved accidentally.
        EditorSceneManager.OpenScene("Assets/Scenes/MechaAR_Main.unity");
    }

    static void ResetRun()
    {
        checks = null;
        Results.Clear();
        tracker = null;
        ui = null;
        dispatcher = null;
        app = null;
    }

    static void SetPortraitGameView()
    {
        // Editor-only API: use a named fixed-size entry and reuse it on later runs.
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
        var display = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
        string presetName = "MechaAR Phase6 " + TestWidth + "x" + TestHeight;
        int index = Array.FindIndex(display, value => value.Contains(presetName));
        if (index < 0)
        {
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, Enum.Parse(modeType, "FixedResolution"), TestWidth, TestHeight, presetName);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = display.Length;
        }
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, index);
        view.Repaint();
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 150)
                throw new TimeoutException("Play Mode integration checks timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (dispatcher == null)
            {
                var host = new GameObject("MechaAR editor integration checks");
                host.hideFlags = HideFlags.DontSave;
                dispatcher = host.AddComponent<MechaARPhase6TestDispatcher>();
                dispatcher.Frame = PlayerFrame;
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    static void PlayerFrame()
    {
        try
        {
            if (checks == null) checks = SessionState.GetBool("MechaAR.Phase6.Baseline", false) ? BaselineFrames() : Verify();
            if (!checks.MoveNext()) Finish(true, null);
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    static MechaARUIManager app;
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, PrivateInstance).GetValue(owner);
    static IEnumerator BaselineFrames()
    {
        for (int i = 0; i < 15; i++) yield return null;
        app = UnityEngine.Object.FindFirstObjectByType<MechaARUIManager>();
        Check(app != null, "Application manager exists.");
        foreach (var frame in Capture("home")) yield return frame;
        app.ShowLibrary();
        foreach (var frame in Capture("library")) yield return frame;
        var input = Field<TMP_InputField>(app, "librarySearchInput");
        var content = Field<RectTransform>(app, "libraryContent");
        Results.Add("BASELINE search actual text=" + (input == null ? "MISSING" : input.text));
        Results.Add("BASELINE total bound cards=" + content.GetComponentsInChildren<EquipmentLibraryCard>(true).Length);
        Results.Add("BASELINE visible cards=" + content.GetComponentsInChildren<EquipmentLibraryCard>().Length);
        Results.Add("BASELINE database equipment=" + Field<EquipmentDatabase>(app, "equipmentDatabase").Equipments.Count);
        Check(!app.HasStartedAR, "Library baseline does not start AR.");
        typeof(MechaARUIManager).GetMethod("ApplyPage", PrivateInstance).Invoke(app, new object[] { MechaARUIManager.Page.Scanner });
        foreach (var frame in Capture("scanner-searching")) yield return frame;
        Check(!app.HasStartedAR, "Scanner UI baseline captured without starting AR subsystems.");
    }
    static IEnumerator Verify()
    {
        for (int i = 0; i < 15; i++) yield return null;
        app = UnityEngine.Object.FindFirstObjectByType<MechaARUIManager>();
        tracker = UnityEngine.Object.FindFirstObjectByType<ARImageTracking>();
        ui = UnityEngine.Object.FindFirstObjectByType<EquipmentInfoUI>(FindObjectsInactive.Include);
        Check(app != null && tracker != null && ui != null, "Existing manager, tracker and scanner UI remain.");
        var database = Field<EquipmentDatabase>(app, "equipmentDatabase");
        var catalog = new List<EquipmentData>();
        foreach (var data in database.Equipments) if (data != null && !catalog.Contains(data)) catalog.Add(data);
        Check(catalog.Count == 8, "Current unchanged database has eight equipment.");
        var input = Field<TMP_InputField>(app, "librarySearchInput");
        var content = Field<RectTransform>(app, "libraryContent");
        var template = Field<EquipmentLibraryCard>(app, "libraryCardTemplate");
        var scroll = Field<ScrollRect>(app, "libraryScroll");
        var buttons = Field<Button[]>(app, "libraryCategoryButtons");
        Check(input != null && content != null && template != null && scroll != null && buttons.Length == 4, "Saved Library references exist.");
        Check(input.text == "" && input.placeholder != null && !template.gameObject.activeSelf, "Cold query empty, placeholder separate, original template inactive.");
        Check(!app.HasStartedAR, "Cold Home does not start AR.");
        MissingReferences();
        foreach (var frame in Capture("home")) yield return frame;
        ClickAction("ShowLibrary"); Page(MechaARUIManager.Page.Library);
        AssertVisible(catalog, "Cold empty search and All");
        CheckArtwork(content);
        foreach (var frame in Capture("library-all")) yield return frame;
        LibraryLayout(input, scroll, buttons);
        var hits = new List<RaycastResult>();
        var inputRect = (RectTransform)input.transform;
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, inputRect.TransformPoint(inputRect.rect.center)) }, hits);
        Check(hits.Count > 0 && (hits[0].gameObject.transform == input.transform || hits[0].gameObject.transform.IsChildOf(input.transform)), "Search center raycast reaches TMP input.");
        Check(scroll.content.rect.height > scroll.viewport.rect.height, "All cards form scrollable content.");
        scroll.verticalNormalizedPosition = 1; Canvas.ForceUpdateCanvases();
        float initialY = scroll.content.anchoredPosition.y;
        scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
        Check(Mathf.Abs(initialY - scroll.content.anchoredPosition.y) > 10, "Scroll moves content to final cards.");
        foreach (var frame in Capture("library-bottom")) yield return frame;
        scroll.verticalNormalizedPosition = 1;
        foreach (var data in catalog)
        {
            input.text = "  " + data.equipmentId.ToLowerInvariant() + "  ";
            AssertVisible(new List<EquipmentData> { data }, "Case insensitive ID with surrounding whitespace " + data.equipmentId);
            Check(app.LibrarySearch == data.equipmentId.ToLowerInvariant(), "Input listener trims query.");
        }
        input.text = catalog[0].equipmentName.ToUpperInvariant(); AssertVisible(Matches(catalog, input.text), "English name case insensitive");
        input.text = catalog[0].thaiName; AssertVisible(Matches(catalog, input.text), "Thai name search");
        foreach (var frame in Capture("library-thai-search")) yield return frame;
        input.text = "Sensor"; AssertVisible(Matches(catalog, input.text), "Category searchable");
        input.text = "";
        string[] categories = { "", "Motor & Actuator", "Sensor", "Microcontroller & Controller" };
        int[] expectedCounts = { 8, 4, 2, 2 };
        string[] methods = { "ShowAllEquipment", "ShowMotorEquipment", "ShowSensorEquipment", "ShowControllerEquipment" };
        for (int category = 0; category < categories.Length; category++)
        {
            Check(HasAction(buttons[category], app, methods[category]), "Category persistent event " + methods[category]);
            Click(buttons[category]);
            var expected = Matches(catalog, "", categories[category]);
            Check(expected.Count == expectedCounts[category], "Current category membership " + categories[category]);
            AssertVisible(expected, "Category " + categories[category]);
            for (int b = 0; b < buttons.Length; b++)
            {
                Color color = b == category ? new Color32(0x14, 0x2B, 0x29, 255) : new Color32(0xE1, 0xEC, 0xE6, 255);
                Check(ColorDistance(buttons[b].targetGraphic.color, color) < .01f, "Selected filter visual style " + b);
            }
            foreach (var frame in Capture("library-category-" + category)) yield return frame;
        }
        Click(buttons[1]); input.text = "Sensor";
        AssertVisible(new List<EquipmentData>(), "Search intersects category to empty");
        foreach (var frame in Capture("library-empty")) yield return frame;
        input.text = "EQ001"; AssertVisible(new List<EquipmentData> { database.GetById("EQ001") }, "Legacy Electric Motor included in Motor filter");
        input.text = ""; Click(buttons[0]);
        input.text = "zz-no-equipment-zz"; AssertVisible(new List<EquipmentData>(), "Unknown query empty state");
        input.text = "   "; AssertVisible(catalog, "Whitespace query shows all"); input.text = "";
        for (int i = 0; i < 3; i++) yield return null;
        foreach (var data in catalog)
        {
            ClickCard(data); float savedPosition = scroll.verticalNormalizedPosition;
            Page(MechaARUIManager.Page.Detail);
            Check(app.SelectedEquipment == data && Field<TMP_Text>(app, "detailNameText").text == data.equipmentName && Field<TMP_Text>(app, "detailThaiNameText").text == data.thaiName && Field<TMP_Text>(app, "detailDescriptionText").text == data.description, "Original matching EquipmentData detail " + data.equipmentId);
            for (int i = 0; i < 4; i++) yield return null;
            var viewer = Field<Equipment3DViewer>(app, "equipment3DViewer"); var pivot = Field<Transform>(viewer, "rotationPivot");
            Check(pivot != null && Field<GameObject>(viewer, "activeModel").name == "Preview_" + data.equipmentId, "Existing 3D preview " + data.equipmentId);
            Quaternion initial = pivot.localRotation;
            viewer.OnDrag(new PointerEventData(EventSystem.current) { delta = new Vector2(75, 25) });
            Check(Quaternion.Angle(initial, pivot.localRotation) > 1, "Existing detail drag rotates " + data.equipmentId);
            var reset = FindAction(viewer, "ResetView"); Check(reset != null, "Original Detail ResetView binding remains."); Click(reset);
            Check(Quaternion.Angle(initial, pivot.localRotation) < .01f, "Detail Reset restores orientation " + data.equipmentId);
            if (data == catalog[0] || data.equipmentId == "EQ002") foreach (var frame in Capture("detail-" + data.equipmentId)) yield return frame;
            app.GoBack(); for (int i = 0; i < 3; i++) yield return null;
            Page(MechaARUIManager.Page.Library); AssertVisible(catalog, "Back preserves catalog " + data.equipmentId);
            Check(Mathf.Abs(savedPosition - scroll.verticalNormalizedPosition) < .015f, "Back preserves scroll " + data.equipmentId);
        }
        Click(buttons[1]); input.text = "Motor";
        var filtered = Matches(catalog, "Motor", "Motor & Actuator"); AssertVisible(filtered, "Combined Motor query and category");
        ClickCard(filtered[filtered.Count - 1]); float filteredPosition = scroll.verticalNormalizedPosition; app.GoBack();
        for (int i = 0; i < 3; i++) yield return null;
        Check(input.text == "Motor" && app.LibrarySearch == "Motor" && app.LibraryCategory == "Motor & Actuator", "Back preserves input/query/category.");
        AssertVisible(filtered, "Back preserves combined results");
        Check(Mathf.Abs(filteredPosition - scroll.verticalNormalizedPosition) < .015f, "Filtered Back preserves scroll.");
        input.text = ""; Click(buttons[0]);
        var safe = app.GetComponentInChildren<SafeAreaPanel>(true); Check(safe != null, "Existing safe-area adapter remains.");
        var safeRect = (RectTransform)safe.transform; var oldMin = safeRect.anchorMin; var oldMax = safeRect.anchorMax;
        safe.enabled = false; safeRect.anchorMin = new Vector2(0, 100f / TestHeight); safeRect.anchorMax = new Vector2(1, 1 - 120f / TestHeight);
        foreach (var frame in Capture("library-safe-area")) yield return frame;
        LibraryLayout(input, scroll, buttons);
        safeRect.anchorMin = oldMin; safeRect.anchorMax = oldMax; safe.enabled = true;
        for (int i = 0; i < 3; i++) yield return null;
        FutureCatalog(catalog, template);
        Check(!app.HasStartedAR, "Offline checks never start AR.");
        foreach (var frame in VerifyARReset(catalog)) yield return frame;
        app.ShowHome(); Page(MechaARUIManager.Page.Home);
        Check(!app.HasStartedAR, "Editor reset simulation avoids AR subsystem startup.");
    }

    static float ColorDistance(Color a, Color b) => Mathf.Abs(a.r-b.r) + Mathf.Abs(a.g-b.g) + Mathf.Abs(a.b-b.b) + Mathf.Abs(a.a-b.a);
    static bool HasAction(Button button, UnityEngine.Object target, string method)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (button.onClick.GetPersistentTarget(i) == target && button.onClick.GetPersistentMethodName(i) == method) return true;
        return false;
    }
    static List<EquipmentData> Matches(List<EquipmentData> catalog, string query, string category = "")
    {
        var result = new List<EquipmentData>();
        foreach (var data in catalog)
        {
            bool categoryMatch = category == "" || data.category == category || (category == "Motor & Actuator" && data.category == "Electric Motor");
            string q = (query ?? "").Trim(); bool queryMatch = q == "";
            foreach (var value in new[] { data.equipmentName, data.thaiName, data.equipmentId, data.category })
                queryMatch |= !string.IsNullOrEmpty(value) && value.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            if (categoryMatch && queryMatch) result.Add(data);
        }
        return result;
    }
    static void AssertVisible(List<EquipmentData> expected, string description)
    {
        var actual = new HashSet<EquipmentData>();
        foreach (var card in Field<RectTransform>(app, "libraryContent").GetComponentsInChildren<EquipmentLibraryCard>())
            Check(card.Equipment != null && actual.Add(card.Equipment), "Visible card uniquely bound " + card.name);
        Check(actual.SetEquals(expected) && app.LibraryVisibleCount == expected.Count, description + ": exact membership/count " + expected.Count);
        var count = Field<TMP_Text>(app, "libraryResultText");
        Check(count != null && count.gameObject.activeInHierarchy && count.text.StartsWith("แสดง " + expected.Count + " จาก ", StringComparison.Ordinal), description + ": visible result label matches visible count");
        Check(Field<TMP_Text>(app, "libraryEmptyText").gameObject.activeSelf == (expected.Count == 0), description + ": empty state visibility");
    }
    static void CheckArtwork(RectTransform content)
    {
        var textures = new HashSet<Texture>();
        foreach (var card in content.GetComponentsInChildren<EquipmentLibraryCard>())
        {
            var artwork = card.GetComponent<LibraryCardArtwork>(); Check(artwork != null, "Library artwork adapter " + card.Equipment.equipmentId);
            var image = Field<RawImage>(artwork, "image");
            Check(image != null && image.gameObject.activeInHierarchy && image.texture != null && textures.Add(image.texture), "Distinct real artwork " + card.Equipment.equipmentId);
            string path = AssetDatabase.GetAssetPath(image.texture);
            Check(path.StartsWith("Assets/UI/Images/Equipment/", StringComparison.Ordinal), "Artwork from existing equipment folder");
            var fitter = Field<AspectRatioFitter>(artwork, "aspectRatio");
            Check(fitter != null && Mathf.Abs(fitter.aspectRatio - (float)image.texture.width / image.texture.height) < .001f, "Artwork aspect ratio preserved");
            Results.Add("ARTWORK " + card.Equipment.equipmentId + " " + path);
        }
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]), max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    static void LibraryLayout(TMP_InputField input, ScrollRect scroll, Button[] buttons)
    {
        Canvas.ForceUpdateCanvases(); var page = (RectTransform)Field<GameObject>(app, "libraryPage").transform;
        Contains(page, (RectTransform)input.transform, "Search inside page");
        Check(((RectTransform)input.transform).rect.width > ((RectTransform)input.transform).rect.height * 3, "Search rounded rectangle proportions");
        foreach (var button in buttons) Contains(page, (RectTransform)button.transform, "Category inside page " + button.name);
        var nav = page.Find("BottomNavigation") as RectTransform; Check(nav != null, "Existing BottomNavigation retained.");
        Check(ScreenRect(scroll.viewport).yMin >= ScreenRect(nav).yMax - 1, "Library viewport above bottom navigation.");
        foreach (var text in page.GetComponentsInChildren<TMP_Text>())
        {
            if (text.GetComponentInParent<TMP_InputField>() != null) continue;
            text.ForceMeshUpdate();
            string path = text.name;
            for (Transform parent = text.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            Check(!text.isTextOverflowing, "Library TMP layout does not overflow " + path + " text=" + text.text.Replace("\n", " | ") +
                " rect=" + text.rectTransform.rect.size + " preferred=" + text.preferredWidth + "x" + text.preferredHeight + " font=" + text.fontSize);
        }
    }
    static void MissingReferences()
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                foreach (var component in transform.GetComponents<MonoBehaviour>())
                {
                    Check(component != null, "No missing script " + transform.name);
                    var property = new SerializedObject(component).GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference)
                            Check(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0, "No missing reference " + component.GetType().Name + "." + property.propertyPath);
                }
    }
    static void SetField(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(owner, value);
    static void FutureCatalog(List<EquipmentData> catalog, EquipmentLibraryCard template)
    {
        var root = new GameObject("Phase6 unsaved future catalog fixture", typeof(RectTransform)); root.SetActive(false);
        var future = ScriptableObject.CreateInstance<EquipmentData>(); future.name = "Transient equipment"; future.equipmentId = "EQ_FUTURE";
        future.equipmentName = "Future equipment"; future.thaiName = "อุปกรณ์ทดสอบ"; future.category = "Sensor";
        var expanded = new List<EquipmentData>(catalog) { future };
        var db = root.AddComponent<EquipmentDatabase>(); SetField(db, "equipments", expanded.ToArray());
        var fixture = root.AddComponent<MechaARUIManager>(); SetField(fixture, "equipmentDatabase", db);
        SetField(fixture, "libraryContent", (RectTransform)root.transform); SetField(fixture, "libraryCardTemplate", template);
        root.SetActive(true);
        Check(root.GetComponentsInChildren<EquipmentLibraryCard>().Length == expanded.Count && fixture.LibraryVisibleCount == expanded.Count, "Future transient ninth equipment builds without hardcoded count.");
        UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(future);
    }
    static IEnumerable VerifyARReset(List<EquipmentData> catalog)
    {
        var gesture = tracker.GetComponent<ARModelGestureController>();
        Check(gesture != null && Field<ARImageTracking>(gesture, "imageTracking") == tracker && Field<MechaARUIManager>(gesture, "uiManager") == app, "Existing XR Origin gesture references preserved.");
        Check(gesture.GetType().GetMethod("ResetCurrent", Type.EmptyTypes)?.ReturnType == typeof(void), "Existing public ResetCurrent void() signature.");
        typeof(MechaARUIManager).GetMethod("ApplyPage", PrivateInstance).Invoke(app, new object[] { MechaARUIManager.Page.Scanner });
        for (int i = 0; i < 4; i++) yield return null;
        var reset = FindAction(gesture, "ResetCurrent"); Check(reset != null && reset.name == "ARResetButton", "Persisted ARResetButton connects existing ResetCurrent.");
        Click(reset); Check(Models().Count == 0, "Reset without tracked model is safe.");
        foreach (var frame in Capture("scanner-reset-searching")) yield return frame;
        ulong id = 600; ARTrackedImage previous = null; GameObject previousModel = null;
        foreach (var data in catalog)
        {
            var image = Image(++id, data.referenceImageName, TrackingState.Tracking); Batch(new[] { image }, null, null);
            var model = Models()[image.trackableId];
            var selected = typeof(ARModelGestureController).GetMethod("SelectedModel", PrivateInstance).Invoke(gesture, null);
            Check(selected != null, "Tracker registers/focuses existing gesture state " + data.equipmentId);
            var type = selected.GetType(); var rotation = (Quaternion)type.GetField("OriginalRotation").GetValue(selected); var scale = (Vector3)type.GetField("OriginalScale").GetValue(selected);
            SetField(selected, "Yaw", 33f); SetField(selected, "Pitch", 12f); SetField(selected, "ScaleMultiplier", 1.7f);
            model.transform.localRotation = Quaternion.Euler(12, 33, 0) * rotation; model.transform.localScale = scale * 1.7f;
            SetField(gesture, "previousFingerCount", 2); SetField(gesture, "lastPinchDistance", 123f);
            Quaternion otherRotation = previousModel != null ? previousModel.transform.localRotation : Quaternion.identity;
            Vector3 otherScale = previousModel != null ? previousModel.transform.localScale : Vector3.zero;
            Click(reset);
            Check(Quaternion.Angle(model.transform.localRotation, rotation) < .01f && Vector3.Distance(model.transform.localScale, scale) < .00001f, "AR Reset restores original rotation/scale " + data.equipmentId);
            Check((float)type.GetField("Yaw").GetValue(selected) == 0 && (float)type.GetField("Pitch").GetValue(selected) == 0 && (float)type.GetField("ScaleMultiplier").GetValue(selected) == 1 && Field<int>(gesture, "previousFingerCount") == 0 && Field<float>(gesture, "lastPinchDistance") == 0, "Reset clears gesture state " + data.equipmentId);
            Check(typeof(ARModelGestureController).GetMethod("SelectedModel", PrivateInstance).Invoke(gesture, null) == selected, "Reset retains selected model state " + data.equipmentId);
            if (previousModel != null) Check(Quaternion.Angle(previousModel.transform.localRotation, otherRotation) < .01f && Vector3.Distance(previousModel.transform.localScale, otherScale) < .00001f, "Reset affects current model only " + data.equipmentId);
            ResetLayout(reset);
            if (data == catalog[0])
            {
                foreach (var frame in Capture("scanner-reset-detected")) yield return frame;
                Click(Field<Button>(ui, "expandButton")); for (int i = 0; i < 4; i++) yield return null;
                ResetLayout(reset); foreach (var frame in Capture("scanner-reset-expanded")) yield return frame;
                Click(Field<Button>(ui, "closeButton"));
            }
            State(image, TrackingState.Limited); Batch(null, new[] { image }, null); Click(reset);
            Check(!model.activeSelf, "Reset with lost target safe and preserves hidden state " + data.equipmentId);
            State(image, TrackingState.Tracking); Batch(null, new[] { image }, null);
            SetField(selected, "Yaw", 15f); SetField(selected, "ScaleMultiplier", 1.25f);
            model.transform.localRotation = Quaternion.Euler(0, 15, 0) * rotation; model.transform.localScale = scale * 1.25f;
            Check(model.activeSelf && Quaternion.Angle(model.transform.localRotation, rotation) > 1, "State supports further changes after reset; Android touch not simulated " + data.equipmentId);
            if (previous != null) { Batch(null, null, new[] { previous.trackableId }); UnityEngine.Object.Destroy(previous.gameObject); }
            previous = image; previousModel = model;
        }
        Batch(null, null, new[] { previous.trackableId }); UnityEngine.Object.Destroy(previous.gameObject); Click(reset);
        Check(Models().Count == 0, "Final removal and Reset leave no spawned models.");
    }
    static void ResetLayout(Button reset)
    {
        var bounds = ScreenRect((RectTransform)reset.transform); Check(bounds.width >= 80 && bounds.height >= 60, "AR Reset mobile touch target.");
        foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            if (button != reset && button.name == "ScannerHomeButton") Check(!bounds.Overlaps(ScreenRect((RectTransform)button.transform)), "Reset avoids Scanner Home.");
        var status = Field<TMP_Text>(ui, "scanStatusText"); if (status != null && status.gameObject.activeInHierarchy) Check(!bounds.Overlaps(ScreenRect(status.rectTransform)), "Reset avoids scan status.");
        var panel = Field<GameObject>(ui, "equipmentInfoPanel"); if (panel.activeInHierarchy) Check(!bounds.Overlaps(ScreenRect((RectTransform)panel.transform)), "Reset avoids information sheet.");
    }


    static Button FindAction(UnityEngine.Object owner, string method)
    {
        foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == owner && button.onClick.GetPersistentMethodName(i) == method) return button;
        return null;
    }
    static void Contains(RectTransform parent, RectTransform child, string description)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners);
        foreach (var world in corners)
        {
            var p = parent.InverseTransformPoint(world);
            Check(p.x >= parent.rect.xMin - .1f && p.x <= parent.rect.xMax + .1f &&
                p.y >= parent.rect.yMin - .1f && p.y <= parent.rect.yMax + .1f, description);
        }
    }
    static IEnumerable Capture(string name)
    {
        for (int i = 0; i < 4; ++i) yield return null;
        Canvas.ForceUpdateCanvases();
        Check(Screen.width == TestWidth && Screen.height == TestHeight, "Capture uses requested portrait resolution: " + name);
        foreach (var text in SessionState.GetBool("MechaAR.Phase6.Baseline", false) ? Array.Empty<TMP_Text>() : UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            string visible = text.text.Replace("\n", "").Replace("\r", "").Replace("\t", "").Replace("\u200B", "");
            Check(text.font != null && text.font.HasCharacters(visible, out uint[] missing, true, true), "Font glyph coverage " + name + "/" + text.name);
            Check(text.rectTransform.rect.height > 0 && text.rectTransform.rect.width > 0, "Nonzero text layout " + name + "/" + text.name);
        }
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Evidence + "/" + name + ".png"));
        for (int i = 0; i < 5; ++i) yield return null;
    }

    static void Page(MechaARUIManager.Page expected)
    {
        Check(app.CurrentPage == expected, "Navigation page: " + expected);
        int active = 0;
        foreach (string field in new[] { "homePage", "scannerPage", "libraryPage", "detailPage", "tutorialPage", "aboutPage" })
            if (Field<GameObject>(app, field).activeSelf) active++;
        Check(active == 1, "Exactly one application page is active.");
    }

    static void ClickCard(EquipmentData data)
    {
        foreach (var card in UnityEngine.Object.FindObjectsByType<EquipmentLibraryCard>(FindObjectsSortMode.None))
            if (card.Equipment == data) { Click(Field<Button>(card, "openButton")); return; }
        throw new InvalidOperationException("No visible equipment card for " + data.equipmentId);
    }

    static void ClickAction(string method)
    {
        foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); ++i)
                if (button.onClick.GetPersistentTarget(i) == app && button.onClick.GetPersistentMethodName(i) == method)
                { Click(button); return; }
        throw new InvalidOperationException("No visible button bound to " + method);
    }

    static void Click(Button button)
    {
        Check(button != null && button.isActiveAndEnabled && button.interactable, "Button active and interactable: " + (button != null ? button.name : "missing"));
        var scroll = button.GetComponentInParent<ScrollRect>();
        bool reachable = false;
        string diagnostic = "";
        for (int attempt = 0; attempt <= 20; ++attempt)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            diagnostic = " point=" + pointer.position + " hits=" + string.Join(",", hits.ConvertAll(h => h.gameObject.name)) + " graphic=" + button.targetGraphic + " raycast=" + (button.targetGraphic != null && button.targetGraphic.raycastTarget);
            reachable = hits.Count > 0 && (hits[0].gameObject.transform == button.transform || hits[0].gameObject.transform.IsChildOf(button.transform));
            if (reachable || scroll == null) break;
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1 - attempt / 20f;
        }
        Check(reachable, "Center raycast reaches button without overlay interception: " + button.name + diagnostic);
        button.onClick.Invoke();
    }
    static Dictionary<TrackableId, GameObject> Models() =>
        (Dictionary<TrackableId, GameObject>)typeof(ARImageTracking).GetField("spawnedModels", PrivateInstance).GetValue(tracker);

    static ARTrackedImage Image(ulong id, string name, TrackingState state)
    {
        var image = new GameObject("Test image " + id).AddComponent<ARTrackedImage>();
        var reference = new XRReferenceImage(default, default, new Vector2(0.15f, 0.15f), name, null);
        typeof(ARTrackedImage).GetProperty("referenceImage").SetValue(image, reference);
        SetData(image, new TrackableId(id, 1), state);
        return image;
    }

    static void State(ARTrackedImage image, TrackingState state) => SetData(image, image.trackableId, state);

    static void SetData(ARTrackedImage image, TrackableId id, TrackingState state)
    {
        var data = new XRTrackedImage(id, Guid.Empty, Pose.identity, new Vector2(0.15f, 0.15f), state, IntPtr.Zero);
        typeof(ARTrackable<XRTrackedImage, ARTrackedImage>).GetProperty("sessionRelativeData", PrivateInstance).SetValue(image, data);
    }

    static void Batch(ARTrackedImage[] added, ARTrackedImage[] updated, TrackableId[] removed)
    {
        var removals = new List<KeyValuePair<TrackableId, ARTrackedImage>>();
        if (removed != null)
            foreach (var id in removed) removals.Add(new KeyValuePair<TrackableId, ARTrackedImage>(id, null));
        var args = new ARTrackablesChangedEventArgs<ARTrackedImage>(
            new ReadOnlyList<ARTrackedImage>(new List<ARTrackedImage>(added ?? Array.Empty<ARTrackedImage>())),
            new ReadOnlyList<ARTrackedImage>(new List<ARTrackedImage>(updated ?? Array.Empty<ARTrackedImage>())),
            new ReadOnlyList<KeyValuePair<TrackableId, ARTrackedImage>>(removals));
        typeof(ARImageTracking).GetMethod("OnTrackedImagesChanged", PrivateInstance).Invoke(tracker, new object[] { args });
    }

    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        Results.Add("PASSED: " + description);
    }

    static void Finish(bool passed, string error)
    {
        EditorApplication.update -= Tick;
        if (dispatcher != null)
        {
            dispatcher.Frame = null;
            UnityEngine.Object.Destroy(dispatcher.gameObject);
        }
        SessionState.SetBool(Pending, false);
        Results.Insert(0, DateTime.UtcNow.ToString("O") + " " + (passed ? "PASSED" : "FAILED"));
        Results.Add("Executor: MechaARPhase6Tests real LateUpdate PlayerLoop; resolution " + TestWidth + "x" + TestHeight);
        var sourcePaths = new List<string>(Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories));
        sourcePaths.Add("Assets/Scenes/MechaAR_Main.unity");
        sourcePaths.Sort(StringComparer.Ordinal);
        using (var sha = SHA256.Create())
            foreach (string path in sourcePaths)
                Results.Add("SHA256 " + path.Replace('\\', '/') + " " + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""));
        Results.Add("Editor synthetic AR events; no physical device or AR camera verification.");
        if (error != null) Results.Add(error);
        File.WriteAllLines(Evidence + "/playmode-tests.txt", Results);
        if (passed) Debug.Log("[MechaAR Phase6] PLAYMODE TESTS PASSED");
        else Debug.LogError("[MechaAR Phase6] PLAYMODE TESTS FAILED: " + error);
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }
}



