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
public static class MechaARPhase4Tests
{
    const string Pending = "MechaAR.Phase4.Tests.Pending";
    const string RestoreScene = "MechaAR.Phase4.Tests.RestoreScene";
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
    static string Evidence => SessionState.GetString("MechaAR.Phase4.Evidence", ".agent-system/tasks/MECHA-PHASE4/evidence/" + TestHeight);
    static readonly List<string> Results = new List<string>();
    static IEnumerator checks;
    static double started;
    static ARImageTracking tracker;
    static EquipmentInfoUI ui;
    static MechaARPhase4TestDispatcher dispatcher;
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("MechaAR/Phase 4/Run integration checks")]
    public static void Baseline() { SessionState.SetBool("MechaAR.Phase4.Baseline", true); StartRun(); } public static void RunTests() { SessionState.SetBool("MechaAR.Phase4.Baseline", false); StartRun(); } static void StartRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play Mode before running MechaAR integration checks.");
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ResetRun();
        SessionState.SetString("MechaAR.Phase4.Evidence", ".agent-system/tasks/MECHA-PHASE4/evidence/" + (SessionState.GetBool("MechaAR.Phase4.Baseline", false) ? "before/" : "after/") + TestWidth + "x" + TestHeight + "/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
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
        string presetName = "MechaAR Phase4 " + TestWidth + "x" + TestHeight;
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
                dispatcher = host.AddComponent<MechaARPhase4TestDispatcher>();
                dispatcher.Frame = PlayerFrame;
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    static void PlayerFrame()
    {
        try
        {
            if (checks == null) checks = SessionState.GetBool("MechaAR.Phase4.Baseline", false) ? BaselineFrames() : Verify();
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
        tracker = UnityEngine.Object.FindFirstObjectByType<ARImageTracking>();
        ui = UnityEngine.Object.FindFirstObjectByType<EquipmentInfoUI>(FindObjectsInactive.Include);
        foreach (var frame in Capture("home")) yield return frame;
        app.ShowTutorial(); foreach (var frame in Capture("tutorial")) yield return frame;
        app.ShowAbout(); foreach (var frame in Capture("about")) yield return frame;
        foreach (EquipmentData data in tracker.Database.Equipments)
        {
            if (data == null) continue;
            app.ShowEquipment(data);
            foreach (var frame in Capture("detail-" + data.equipmentId)) yield return frame;
        }
        app.StartScan();
        foreach (var behaviour in UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (behaviour.GetType().Namespace == "UnityEngine.XR.ARFoundation" || behaviour is ARRuntimeDiagnostics) behaviour.enabled = false;
        foreach (var frame in Capture("scanner-searching")) yield return frame;
        var equipment = tracker.Database.GetById("EQ004");
        var tracked = Image(404, equipment.referenceImageName, TrackingState.Tracking);
        Batch(new[] { tracked }, null, null);
        foreach (var frame in Capture("scanner-detected")) yield return frame;
        Batch(null, null, new[] { tracked.trackableId });
        UnityEngine.Object.Destroy(tracked.gameObject);
    }
    static IEnumerator Verify()
    {
        for (int i = 0; i < 15; i++) yield return null;
        app = UnityEngine.Object.FindFirstObjectByType<MechaARUIManager>();
        tracker = UnityEngine.Object.FindFirstObjectByType<ARImageTracking>();
        ui = UnityEngine.Object.FindFirstObjectByType<EquipmentInfoUI>(FindObjectsInactive.Include);
        Check(app != null && tracker != null && ui != null, "Saved scene has existing navigation, tracker and equipment UI.");
        var session = Field<ARSession>(app, "arSession");
        var camera = Field<ARCameraManager>(app, "arCameraManager");
        var diagnostics = UnityEngine.Object.FindFirstObjectByType<ARRuntimeDiagnostics>(FindObjectsInactive.Include);
        var catalog = tracker.Database.Equipments;
        Check(catalog.Count == 4, "Current real database retains all four equipment.");
        string[] ids = { "EQ001", "EQ002", "EQ003", "EQ004" };
        string[] targets = { "StepperMotor", "ArduinoUNO", "ServoMotorSG90", "HC_SR04" };
        var prefabs = new HashSet<GameObject>();
        for (int i = 0; i < ids.Length; i++)
        {
            var data = tracker.Database.GetById(ids[i]);
            Check(data != null && data.referenceImageName == targets[i] && tracker.Database.GetByImageName(targets[i]) == data &&
                data.modelPrefab != null && prefabs.Add(data.modelPrefab), "Real unique database/prefab mapping " + ids[i] + "/" + targets[i]);
        }
        Check(!app.HasStartedAR && !session.enabled && !camera.enabled, "Cold Home does not start AR or camera.");
        Page(MechaARUIManager.Page.Home);
        foreach (var frame in Capture("home")) yield return frame;
        ClickAction("ShowLibrary"); Page(MechaARUIManager.Page.Library);
        foreach (var frame in Capture("library")) yield return frame;
        foreach (var data in catalog)
        {
            ClickCard(data); Page(MechaARUIManager.Page.Detail);
            Check(app.SelectedEquipment == data &&
                Field<TMP_Text>(app, "detailNameText").text == data.equipmentName &&
                Field<TMP_Text>(app, "detailDescriptionText").text == data.description &&
                Field<TMP_Text>(app, "detailPrincipleText").text == data.workingPrinciple,
                "Detail text comes from real SO " + data.equipmentId);
            for (int i = 0; i < 4; i++) yield return null;
            var viewer = Field<Equipment3DViewer>(app, "equipment3DViewer");
            var pivot = Field<Transform>(viewer, "rotationPivot");
            Check(pivot != null && Field<GameObject>(viewer, "activeModel").name == "Preview_" + data.equipmentId,
                "Existing 3D viewer shows " + data.equipmentId);
            Quaternion initial = pivot.localRotation;
            viewer.OnDrag(new PointerEventData(EventSystem.current) { delta = new Vector2(75, 25) });
            Check(Quaternion.Angle(initial, pivot.localRotation) > 1, "Existing drag rotates model " + data.equipmentId);
            var reset = FindAction(viewer, "ResetView");
            Check(reset != null, "Reset is bound to original viewer ResetView.");
            Contains((RectTransform)reset.transform.parent, (RectTransform)reset.transform, "Reset stays inside artwork");
            Click(reset);
            Check(Quaternion.Angle(initial, pivot.localRotation) < .01f, "Reset returns exact initial orientation " + data.equipmentId);
            PreviewMetrics(viewer, data.equipmentId);
            foreach (var frame in Capture("detail-" + data.equipmentId)) yield return frame;
            app.GoBack(); Page(MechaARUIManager.Page.Library);
        }
        app.GoBack(); Page(MechaARUIManager.Page.Home);
        ClickAction("ShowTutorial"); Page(MechaARUIManager.Page.Tutorial);
        foreach (var frame in Capture("tutorial")) yield return frame;
        foreach (var frame in CapturePageBottom("tutorial-bottom")) yield return frame;
        app.GoBack(); Page(MechaARUIManager.Page.Home);
        ClickAction("ShowAbout"); Page(MechaARUIManager.Page.About);
        foreach (var frame in Capture("about")) yield return frame;
        foreach (var frame in CapturePageBottom("about-bottom")) yield return frame;
        app.GoBack(); Page(MechaARUIManager.Page.Home);
        Check(!app.HasStartedAR && !session.enabled && !camera.enabled, "All offline learning works before AR startup.");

        var safe = app.GetComponentInChildren<SafeAreaPanel>(true);
        Check(safe != null, "Application retains safe area adapter.");
        var safeRect = (RectTransform)safe.transform;
        var oldMin = safeRect.anchorMin; var oldMax = safeRect.anchorMax;
        safe.enabled = false;
        safeRect.anchorMin = new Vector2(0, 100f / TestHeight);
        safeRect.anchorMax = new Vector2(1, 1 - 120f / TestHeight);
        foreach (var frame in Capture("home-safe-area")) yield return frame;
        ClickAction("ShowTutorial"); app.GoBack();
        safeRect.anchorMin = oldMin; safeRect.anchorMax = oldMax; safe.enabled = true;

        int sessions = UnityEngine.Object.FindObjectsByType<ARSession>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int origins = UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        ClickAction("StartScan"); Page(MechaARUIManager.Page.Scanner);
        Check(app.HasStartedAR && session.enabled && camera.enabled, "Scan enables existing AR managers once.");
        session.enabled = false; camera.enabled = false; if (diagnostics != null) diagnostics.enabled = false;
        foreach (var frame in Capture("scanner-searching")) yield return frame;
        ulong next = 401;
        ARTrackedImage previous = null;
        foreach (var data in catalog)
        {
            var tracked = Image(next++, data.referenceImageName, TrackingState.Tracking);
            Batch(new[] { tracked }, null, null);
            var model = Models()[tracked.trackableId];
            Check(tracker.TargetImageName == data.referenceImageName && model.activeSelf &&
                model.name.StartsWith(data.modelPrefab.name), "Synthetic target creates matching model " + data.equipmentId);
            Check(Field<TMP_Text>(ui, "equipmentNameText").text == data.equipmentName &&
                Field<TMP_Text>(ui, "equipmentDescriptionText").text == data.description &&
                Field<TMP_Text>(ui, "equipmentPrincipleText").text == data.workingPrinciple &&
                Field<TMP_Text>(ui, "equipmentApplicationsText").text == (data.applications == null ? "" : "• " + string.Join("\n• ", data.applications)),
                "All scanner fields match selected SO " + data.equipmentId);
            if (previous != null)
            {
                Batch(null, null, new[] { previous.trackableId });
                UnityEngine.Object.Destroy(previous.gameObject);
            }
            for (int i = 0; i < 10; i++) Batch(null, new[] { tracked }, null);
            Check(Models().Count == 1 && Models()[tracked.trackableId] == model, "Updates reuse model " + data.equipmentId);
            foreach (var frame in VerifySheet(data, tracked, model)) yield return frame;
            State(tracked, TrackingState.Limited); Batch(null, new[] { tracked }, null);
            Check(!tracker.IsTrackingTarget && !ui.IsPanelVisible && !model.activeSelf, "Loss hides model and sheet " + data.equipmentId);
            Check(!Field<TMP_Text>(ui, "scanStatusText").text.Contains("ตรวจพบ"), "Loss does not claim detection.");
            State(tracked, TrackingState.Tracking); Batch(null, new[] { tracked }, null);
            Check(ui.IsPanelVisible && model.activeSelf && Models()[tracked.trackableId] == model, "Reacquire reuses model and restores sheet " + data.equipmentId);
            previous = tracked;
        }
        for (int i = 0; i < 3; i++)
        {
            var model = Models()[previous.trackableId];
            app.GoBack(); Page(MechaARUIManager.Page.Home);
            Check(!ui.gameObject.activeInHierarchy && model.activeSelf, "Home hides scanner UI while preserving tracked model.");
            app.StartScan(); Page(MechaARUIManager.Page.Scanner);
            if (diagnostics != null) diagnostics.enabled = false;
            Check(!session.enabled && !camera.enabled && Models()[previous.trackableId] == model && ui.IsPanelVisible,
                "Scanner reentry restores information without AR reset or model duplication.");
        }
        var scannerSafe = ui.GetComponentInChildren<SafeAreaPanel>(true);
        Check(scannerSafe != null, "Scanner retains safe-area adapter.");
        var scannerSafeRect = (RectTransform)scannerSafe.transform;
        var scannerMin = scannerSafeRect.anchorMin; var scannerMax = scannerSafeRect.anchorMax;
        scannerSafe.enabled = false;
        scannerSafeRect.anchorMin = new Vector2(0, 100f / TestHeight);
        scannerSafeRect.anchorMax = new Vector2(1, 1 - 120f / TestHeight);
        Click(Field<Button>(ui, "expandButton"));
        foreach (var frame in Capture("scanner-safe-area-expanded")) yield return frame;
        Contains(scannerSafeRect, (RectTransform)Field<GameObject>(ui, "equipmentInfoPanel").transform, "Expanded sheet respects simulated safe-area insets");
        Click(Field<Button>(ui, "closeButton")); Click(Field<Button>(ui, "reopenButton"));
        scannerSafeRect.anchorMin = scannerMin; scannerSafeRect.anchorMax = scannerMax; scannerSafe.enabled = true;
        Batch(null, null, new[] { previous.trackableId });
        Check(Models().Count == 0 && !tracker.IsTrackingTarget && !ui.IsPanelVisible, "Removal clears models and sheet.");
        UnityEngine.Object.Destroy(previous.gameObject);
        Check(sessions == UnityEngine.Object.FindObjectsByType<ARSession>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length &&
            origins == UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length &&
            cameras == UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            "No duplicate AR session, XR origin or camera.");
        app.GoBack(); Page(MechaARUIManager.Page.Home);
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
    static IEnumerable CapturePageBottom(string name)
    {
        foreach (var scroll in UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None))
        {
            Canvas.ForceUpdateCanvases();
            if (scroll.content == null || scroll.viewport == null || scroll.content.rect.height <= scroll.viewport.rect.height) continue;
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 0;
        }
        foreach (var frame in Capture(name)) yield return frame;
    }
    static void PreviewMetrics(Equipment3DViewer viewer, string id)
    {
        var camera = Field<Camera>(viewer, "previewCamera");
        var model = Field<GameObject>(viewer, "activeModel");
        Vector2 low = Vector2.one * float.PositiveInfinity, high = Vector2.one * float.NegativeInfinity;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            Bounds b = renderer.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 world = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = camera.WorldToViewportPoint(world);
                Check(p.z > camera.nearClipPlane && p.z < camera.farClipPlane, "Preview bounds within camera depth " + id);
                low = Vector2.Min(low, p); high = Vector2.Max(high, p);
            }
        }
        Results.Add("METRIC preview " + id + " projected-renderer-bounds viewport min=" + low.ToString("F4") + " max=" + high.ToString("F4") + " extent=" + (high-low).ToString("F4"));
        Check(low.x >= -.02f && low.y >= -.02f && high.x <= 1.02f && high.y <= 1.02f, "Preview projected bounds fit " + id);
    }


    static IEnumerable VerifySheet(EquipmentData data, ARTrackedImage tracked, GameObject model)
    {
        Check(ui.IsPanelVisible && !ui.IsExpanded, "New target starts collapsed " + data.equipmentId);
        var panel = (RectTransform)Field<GameObject>(ui, "equipmentInfoPanel").transform;
        float compact = panel.rect.height;
        Check(compact < Screen.height * .35f / ui.GetComponent<Canvas>().scaleFactor, "Collapsed sheet reserves camera space.");
        foreach (var frame in Capture("scanner-" + data.equipmentId + "-collapsed")) yield return frame;
        Click(Field<Button>(ui, "expandButton"));
        Check(ui.IsExpanded, "Expand control opens full information.");
        for (int i = 0; i < 4; i++) yield return null;
        Check(panel.rect.height > compact, "Expanded panel grows.");
        Contains((RectTransform)panel.parent, panel, "Sheet remains inside safe area");
        var scroll = Field<ScrollRect>(ui, "detailsScroll");
        Check(scroll.gameObject.activeInHierarchy, "Expanded body is available.");
        foreach (var frame in Capture("scanner-" + data.equipmentId + "-expanded")) yield return frame;
        Canvas.ForceUpdateCanvases();
        if (scroll.content.rect.height > scroll.viewport.rect.height + 1)
        {
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; Canvas.ForceUpdateCanvases();
            float start = scroll.content.anchoredPosition.y;
            scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
            Check(Mathf.Abs(start - scroll.content.anchoredPosition.y) > 1, "Expanded information scrolls to applications.");
        }
        Click(Field<Button>(ui, "closeButton"));
        Check(!ui.IsPanelVisible && model.activeSelf, "Close preserves tracked 3D model.");
        for (int i = 0; i < 10; i++) Batch(null, new[] { tracked }, null);
        Check(!ui.IsPanelVisible, "Repeated tracking does not immediately reopen dismissed sheet.");
        foreach (var frame in Capture("scanner-" + data.equipmentId + "-closed")) yield return frame;
        Click(Field<Button>(ui, "reopenButton"));
        Check(ui.IsPanelVisible && !ui.IsExpanded && model.activeSelf, "Reopen restores compact sheet without changing model.");
    }


    static IEnumerable Capture(string name)
    {
        for (int i = 0; i < 4; ++i) yield return null;
        Canvas.ForceUpdateCanvases();
        Check(Screen.width == TestWidth && Screen.height == TestHeight, "Capture uses requested portrait resolution: " + name);
        foreach (var text in SessionState.GetBool("MechaAR.Phase4.Baseline", false) ? Array.Empty<TMP_Text>() : UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            string visible = text.text.Replace("\n", "").Replace("\r", "").Replace("\t", "");
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
        for (int attempt = 0; attempt <= 20; ++attempt)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            reachable = hits.Count > 0 && (hits[0].gameObject.transform == button.transform || hits[0].gameObject.transform.IsChildOf(button.transform));
            if (reachable || scroll == null) break;
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1 - attempt / 20f;
        }
        Check(reachable, "Center raycast reaches button without overlay interception: " + button.name);
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
        Results.Add("Executor: MechaARPhase4Tests real LateUpdate PlayerLoop; resolution " + TestWidth + "x" + TestHeight);
        var sourcePaths = new List<string>(Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories));
        sourcePaths.Add("Assets/Scenes/MechaAR_Main.unity");
        sourcePaths.Sort(StringComparer.Ordinal);
        using (var sha = SHA256.Create())
            foreach (string path in sourcePaths)
                Results.Add("SHA256 " + path.Replace('\\', '/') + " " + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""));
        Results.Add("Editor synthetic AR events; no physical device or AR camera verification.");
        if (error != null) Results.Add(error);
        File.WriteAllLines(Evidence + "/playmode-tests.txt", Results);
        if (passed) Debug.Log("[MechaAR Phase4] PLAYMODE TESTS PASSED");
        else Debug.LogError("[MechaAR Phase4] PLAYMODE TESTS FAILED: " + error);
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }
}



