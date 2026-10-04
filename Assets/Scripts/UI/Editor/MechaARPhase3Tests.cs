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
public static class MechaARPhase3Tests
{
    const string Pending = "MechaAR.Phase3.Tests.Pending";
    const string RestoreScene = "MechaAR.Phase3.Tests.RestoreScene";
    static int TestHeight
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
    static string Evidence => SessionState.GetString("MechaAR.Phase3.Evidence", ".agent-system/tasks/MECHA-PHASE3/evidence/" + TestHeight);
    static readonly List<string> Results = new List<string>();
    static IEnumerator checks;
    static double started;
    static ARImageTracking tracker;
    static EquipmentInfoUI ui;
    static MechaARPhase3TestDispatcher dispatcher;
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("MechaAR/Phase 3/Run integration checks")]
    public static void RunTests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play Mode before running MechaAR integration checks.");
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ResetRun();
        SessionState.SetString("MechaAR.Phase3.Evidence", ".agent-system/tasks/MECHA-PHASE3/evidence/" + TestHeight + "/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
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
        string presetName = "MechaAR Portrait " + TestHeight;
        int index = Array.FindIndex(display, value => value.Contains(presetName));
        if (index < 0)
        {
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, Enum.Parse(modeType, "FixedResolution"), 1080, TestHeight, presetName);
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
                dispatcher = host.AddComponent<MechaARPhase3TestDispatcher>();
                dispatcher.Frame = PlayerFrame;
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    static void PlayerFrame()
    {
        try
        {
            if (checks == null) checks = Verify();
            if (!checks.MoveNext()) Finish(true, null);
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    static MechaARUIManager app;
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, PrivateInstance).GetValue(owner);
    static IEnumerator Verify()
    {
        for (int i = 0; i < 12; ++i) yield return null;
        app = UnityEngine.Object.FindFirstObjectByType<MechaARUIManager>();
        tracker = UnityEngine.Object.FindFirstObjectByType<ARImageTracking>();
        ui = UnityEngine.Object.FindFirstObjectByType<EquipmentInfoUI>(FindObjectsInactive.Include);
        Check(app != null && tracker != null && ui != null, "Installed scene contains navigation, tracker and original equipment UI.");
        var session = Field<ARSession>(app, "arSession");
        var camera = Field<ARCameraManager>(app, "arCameraManager");
        var first = tracker.Database.GetById("EQ001");
        var second = tracker.Database.GetById("EQ002");
        Check(first != null && second != null && first.modelPrefab != second.modelPrefab, "Real database contains EQ001 and EQ002 with distinct prefabs.");
        Check(!app.HasStartedAR && !session.enabled && !camera.enabled, "Home starts without requesting AR session or camera.");
        var diagnostics = UnityEngine.Object.FindFirstObjectByType<ARRuntimeDiagnostics>(FindObjectsInactive.Include);
        Check(diagnostics != null && !diagnostics.enabled, "Existing runtime diagnostics is gated off on cold Home.");
        Page(MechaARUIManager.Page.Home);
        foreach (var _ in Capture("home")) yield return _;
        ClickAction("ShowLibrary");
        yield return null;
        Page(MechaARUIManager.Page.Library);
        foreach (var _ in Capture("library")) yield return _;
        foreach (var data in new[] { first, second })
        {
            ClickCard(data);
            yield return null;
            Page(MechaARUIManager.Page.Detail);
            Check(app.SelectedEquipment == data && Field<TMP_Text>(app, "detailNameText").text == data.equipmentName &&
                Field<TMP_Text>(app, "detailDescriptionText").text == data.description &&
                Field<TMP_Text>(app, "detailPrincipleText").text == data.workingPrinciple,
                "Library details match real asset: " + data.equipmentId);
            foreach (var _ in Capture("detail-" + data.equipmentId)) yield return _;
            ClickAction("GoBack");
            Page(MechaARUIManager.Page.Library);
        }
        app.GoBack(); Page(MechaARUIManager.Page.Home);
        ClickCard(first); Page(MechaARUIManager.Page.Detail);
        app.GoBack(); Page(MechaARUIManager.Page.Home);
        ClickAction("ShowTutorial"); Page(MechaARUIManager.Page.Tutorial);
        foreach (var _ in Capture("tutorial")) yield return _;
        app.GoBack(); Page(MechaARUIManager.Page.Home);
        ClickAction("ShowAbout"); Page(MechaARUIManager.Page.About);
        foreach (var _ in Capture("about")) yield return _;
        app.GoBack(); Page(MechaARUIManager.Page.Home);
        Check(!app.HasStartedAR && !session.enabled && !camera.enabled, "All offline pages remain independent of camera permission and AR startup.");

        var longData = UnityEngine.Object.Instantiate(second);
        longData.description = string.Join("\n", new string[] { second.description, second.description, second.description, second.description, second.description, second.description, second.description, second.description });
        app.ShowEquipment(longData);
        for (int i = 0; i < 4; ++i) yield return null;
        var scroll = Field<ScrollRect>(app, "detailScroll");
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content); Canvas.ForceUpdateCanvases();
        Check(scroll.content.rect.height > scroll.viewport.rect.height + 1, "Long cloned detail content overflows and remains scrollable.");
        scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; Canvas.ForceUpdateCanvases();
        float top = scroll.content.anchoredPosition.y;
        scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
        Check(Mathf.Abs(scroll.content.anchoredPosition.y - top) > 1, "Detail scroll reaches lower content.");
        foreach (var _ in Capture("detail-long-bottom")) yield return _;
        app.ShowHome(); UnityEngine.Object.Destroy(longData);

        var safe = app.GetComponentInChildren<SafeAreaPanel>(true);
        Check(safe != null, "Application UI has a safe-area adapter.");
        var safeRect = (RectTransform)safe.transform;
        var oldMin = safeRect.anchorMin; var oldMax = safeRect.anchorMax;
        safe.enabled = false;
        safeRect.anchorMin = new Vector2(0, 100f / TestHeight);
        safeRect.anchorMax = new Vector2(1, 1 - 120f / TestHeight);
        Canvas.ForceUpdateCanvases();
        foreach (var _ in Capture("home-safe-area")) yield return _;
        ClickAction("ShowLibrary"); app.GoBack();
        Check(app.CurrentPage == MechaARUIManager.Page.Home, "Primary navigation remains reachable with simulated top and bottom safe-area insets.");
        safeRect.anchorMin = oldMin; safeRect.anchorMax = oldMax; safe.enabled = true;

        int sessions = UnityEngine.Object.FindObjectsByType<ARSession>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int origins = UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        ClickAction("StartScan");
        Check(app.HasStartedAR && session.enabled && camera.enabled, "First Scan enables the same existing session and camera manager.");
        Check(diagnostics.enabled, "Scanner enables the existing diagnostics component.");
        // Disable only subsystem components immediately, before yielding a frame. Synthetic
        // AR events below test navigation/tracker behavior, not hardware initialization.
        session.enabled = false; camera.enabled = false;
        diagnostics.enabled = false;
        Page(MechaARUIManager.Page.Scanner);
        var a = Image(101, first.referenceImageName, TrackingState.Tracking);
        var b = Image(102, second.referenceImageName, TrackingState.Tracking);
        Batch(new[] { a }, null, null);
        var model = Models()[a.trackableId];
        Check(tracker.IsTrackingTarget && ui.IsPanelVisible && model.activeSelf && model.name.StartsWith(first.modelPrefab.name), "EQ001 creates correct model and scanner information.");
        for (int i = 0; i < 10; ++i) Batch(null, new[] { a }, null);
        Check(Models().Count == 1 && Models()[a.trackableId] == model, "Repeated tracking updates reuse model instance.");
        foreach (var _ in Capture("scanner-stepper")) yield return _;
        Click(Field<Button>(ui, "closeButton"));
        Batch(null, new[] { a }, null);
        Check(!ui.IsPanelVisible && model.activeSelf, "Closing scanner information preserves model and dismissal.");
        State(a, TrackingState.Limited); Batch(null, new[] { a }, null);
        Check(!ui.IsPanelVisible && !model.activeSelf, "Limited tracking hides both model and information.");
        State(a, TrackingState.Tracking); Batch(null, new[] { a }, null);
        Check(ui.IsPanelVisible && model.activeSelf, "Reacquisition restores model and information.");
        for (int i = 0; i < 3; ++i)
        {
            app.GoBack(); Page(MechaARUIManager.Page.Home);
            Check(!diagnostics.enabled, "Leaving scanner disables diagnostics.");
            Check(!ui.gameObject.activeInHierarchy && !ui.IsPanelVisible && model.activeSelf, "Home hides scanner UI without destroying tracked model.");
            app.StartScan(); Page(MechaARUIManager.Page.Scanner);
            Check(diagnostics.enabled, "Re-entering scanner restores diagnostics.");
            diagnostics.enabled = false;
            Check(!session.enabled && !camera.enabled && Models()[a.trackableId] == model && ui.IsPanelVisible,
                "Re-entry reconciles information, reuses model and does not restart AR managers.");
        }
        Batch(new[] { b }, null, null);
        var arduinoModel = Models()[b.trackableId];
        Check(tracker.TargetImageName == second.referenceImageName && arduinoModel.name.StartsWith(second.modelPrefab.name) &&
            Field<TMP_Text>(ui, "equipmentNameText").text == second.equipmentName, "EQ002 switches to correct distinct prefab and equipment information.");
        foreach (var _ in Capture("scanner-arduino")) yield return _;
        State(b, TrackingState.Limited); Batch(null, new[] { b }, null);
        Check(tracker.TargetImageName == first.referenceImageName && ui.IsPanelVisible, "Loss of Arduino retains still-tracked Stepper information.");
        Batch(null, null, new[] { a.trackableId, b.trackableId });
        Check(!tracker.IsTrackingTarget && !ui.IsPanelVisible && Models().Count == 0, "Removing both IDs clears models and scanner information.");
        Check(sessions == UnityEngine.Object.FindObjectsByType<ARSession>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length &&
            origins == UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length &&
            cameras == UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            "Navigation creates no duplicate session, XR origin or camera.");
        app.GoBack(); Page(MechaARUIManager.Page.Home);
        UnityEngine.Object.Destroy(a.gameObject); UnityEngine.Object.Destroy(b.gameObject);
    }

    static IEnumerable Capture(string name)
    {
        for (int i = 0; i < 4; ++i) yield return null;
        Canvas.ForceUpdateCanvases();
        Check(Screen.width == 1080 && Screen.height == TestHeight, "Capture uses requested portrait resolution: " + name);
        foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
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
        Results.Add("Executor: MechaARPhase3Tests real LateUpdate PlayerLoop; resolution 1080x" + TestHeight);
        var sourcePaths = new List<string>(Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories));
        sourcePaths.Add("Assets/Scenes/MechaAR_Main.unity");
        sourcePaths.Sort(StringComparer.Ordinal);
        using (var sha = SHA256.Create())
            foreach (string path in sourcePaths)
                Results.Add("SHA256 " + path.Replace('\\', '/') + " " + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""));
        Results.Add("Editor synthetic AR events; no physical device or AR camera verification.");
        if (error != null) Results.Add(error);
        File.WriteAllLines(Evidence + "/playmode-tests.txt", Results);
        if (passed) Debug.Log("[MechaAR Phase3] PLAYMODE TESTS PASSED");
        else Debug.LogError("[MechaAR Phase3] PLAYMODE TESTS FAILED: " + error);
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }
}

