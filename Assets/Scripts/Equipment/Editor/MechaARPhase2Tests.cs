using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
public static class MechaARPhase2Tests
{
    const string Pending = "MechaAR.Phase2.Tests.Pending";
    const string RestoreScene = "MechaAR.Phase2.Tests.RestoreScene";
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
    static string Evidence => ".agent-system/tasks/MECHA-PHASE2/evidence" + (TestHeight == 1920 ? "" : "/tall");
    static readonly List<string> Results = new List<string>();
    static IEnumerator checks;
    static double started;
    static ARImageTracking tracker;
    static EquipmentInfoUI ui;
    static MechaARPhase2TestDispatcher dispatcher;
    static readonly List<bool> transitions = new List<bool>();
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("MechaAR/Phase 2/Run integration checks")]
    public static void RunTests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play Mode before running MechaAR integration checks.");
            return;
        }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ResetRun();
        Directory.CreateDirectory(Evidence);
        EditorSceneManager.OpenScene(MechaARPhase2Setup.ScenePath);
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
        EditorSceneManager.OpenScene(MechaARPhase2Setup.ScenePath);
    }

    static void ResetRun()
    {
        checks = null;
        Results.Clear();
        transitions.Clear();
        tracker = null;
        ui = null;
        dispatcher = null;
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
            if (EditorApplication.timeSinceStartup - started > 90)
                throw new TimeoutException("Play Mode integration checks timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (dispatcher == null)
            {
                var host = new GameObject("MechaAR editor integration checks");
                host.hideFlags = HideFlags.DontSave;
                dispatcher = host.AddComponent<MechaARPhase2TestDispatcher>();
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

    static IEnumerator Verify()
    {
        for (int i = 0; i < 12; ++i) yield return null;
        foreach (var behaviour in UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (behaviour.GetType().Name == "ARRuntimeDiagnostics") behaviour.enabled = false;
        tracker = UnityEngine.Object.FindFirstObjectByType<ARImageTracking>();
        ui = UnityEngine.Object.FindFirstObjectByType<EquipmentInfoUI>();
        Check(tracker != null && ui != null, "Installed scene contains tracker and UI.");
        tracker.EquipmentTrackingChanged += RecordTransition;
        Check(!tracker.IsTrackingTarget && !ui.IsPanelVisible, "Initial scan state hides panel.");

        var a = Image(1, "StepperMotor", TrackingState.Tracking);
        var b = Image(2, "StepperMotor", TrackingState.Tracking);
        var other = Image(3, "Unrelated", TrackingState.Tracking);
        Batch(new[] { other }, null, null);
        Check(!tracker.IsTrackingTarget && transitions.Count == 0, "Unrelated images do not trigger equipment.");
        Batch(new[] { a }, null, null);
        Check(tracker.IsTrackingTarget && ui.IsPanelVisible && transitions.Count == 1, "First target reveals equipment exactly once.");
        var model = Models()[a.trackableId];
        Check(model != null && model.activeSelf, "Tracking creates and activates model.");
        for (int i = 0; i < 10; ++i) Batch(null, new[] { a }, null);
        Check(Models().Count == 1 && Models()[a.trackableId] == model && transitions.Count == 1,
            "Repeated updates reuse model and do not repeat transitions.");
        for (int i = 0; i < 4; ++i) yield return null;
        Canvas.ForceUpdateCanvases();
        foreach (var text in ui.GetComponentsInChildren<TMP_Text>(true))
        {
            // Newlines and tabs are layout controls, not visible glyphs.
            string visible = text.text.Replace("\n", "").Replace("\r", "").Replace("\t", "");
            Check(text.font != null && text.font.HasCharacters(visible, out uint[] missing, true, true),
                "Font covers all displayed characters: " + text.name);
        }
        var scroll = (ScrollRect)typeof(EquipmentInfoUI).GetField("detailsScroll", PrivateInstance).GetValue(ui);
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        Canvas.ForceUpdateCanvases();
        LogLayout(scroll, "Installed equipment");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Evidence + "/ui-tracked.png"));
        for (int i = 0; i < 6; ++i) yield return null;
        Check(scroll.viewport.rect.height > 0 && scroll.content.rect.height > 0,
            "Installed details have nonzero viewport and content dimensions.");
        foreach (var text in scroll.content.GetComponentsInChildren<TMP_Text>())
            Check(text.rectTransform.rect.height + 1 >= text.preferredHeight,
                "Layout reserves preferred text height: " + text.name);
        var applications = (TMP_Text)typeof(EquipmentInfoUI).GetField("equipmentApplicationsText", PrivateInstance).GetValue(ui);
        Check(applications.text.Contains("\u2022") && applications.font.HasCharacter('\u2022', true, true),
            "Application list includes a supported bullet glyph.");
        // The supplied short description may fit. A cloned long equipment entry
        // exercises scrolling without modifying the catalog asset or saved scene.
        var catalog = (EquipmentData[])typeof(EquipmentInfoUI).GetField("equipmentCatalog", PrivateInstance).GetValue(ui);
        Check(catalog != null && catalog.Length > 0 && catalog[0] != null, "Equipment catalog supplies scroll fixture source.");
        var longData = UnityEngine.Object.Instantiate(catalog[0]);
        var paragraphs = new string[12];
        for (int i = 0; i < paragraphs.Length; ++i) paragraphs[i] = catalog[0].description;
        longData.description = string.Join("\n", paragraphs);
        ui.ShowEquipment(longData);
        for (int i = 0; i < 4; ++i) yield return null;
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        Canvas.ForceUpdateCanvases();
        LogLayout(scroll, "Long equipment fixture");
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Evidence + "/ui-long-details.png"));
        for (int i = 0; i < 6; ++i) yield return null;
        Check(scroll.content.rect.height > scroll.viewport.rect.height + 1,
            "Long equipment details overflow viewport.");
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
        Canvas.ForceUpdateCanvases();
        float topY = scroll.content.anchoredPosition.y;
        scroll.verticalNormalizedPosition = 0;
        Canvas.ForceUpdateCanvases();
        for (int i = 0; i < 3; ++i) yield return null;
        Results.Add("Scroll movement: topY=" + topY + ", bottomY=" + scroll.content.anchoredPosition.y +
            ", normalized=" + scroll.verticalNormalizedPosition);
        Check(Mathf.Abs(scroll.content.anchoredPosition.y - topY) > 1 && scroll.verticalNormalizedPosition < .01f,
            "Scrolling to bottom moves content and reaches normalized position zero.");
        ui.ShowEquipment(catalog[0]);
        UnityEngine.Object.Destroy(longData);
        for (int i = 0; i < 3; ++i) yield return null;

        var closeButton = (Button)typeof(EquipmentInfoUI).GetField("closeButton", PrivateInstance).GetValue(ui);
        Check(closeButton != null && closeButton.interactable, "Serialized close button is connected and interactable.");
        Check(EventSystem.current != null && EventSystem.current.currentInputModule != null,
            "Scene has an active EventSystem input module.");
        Canvas.ForceUpdateCanvases();
        var closeRect = (RectTransform)closeButton.transform;
        var closeCanvas = closeButton.GetComponentInParent<Canvas>();
        var raycastCamera = closeCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : closeCanvas.worldCamera;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(raycastCamera, closeRect.TransformPoint(closeRect.rect.center))
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Results.Add("Close raycast PlayerLoop Screen=" + Screen.width + "x" + Screen.height +
            ", pointer=" + pointer.position + ", hits=" + hits.Count +
            ", first=" + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
        Check(hits.Count > 0 && (hits[0].gameObject.transform == closeButton.transform ||
            hits[0].gameObject.transform.IsChildOf(closeButton.transform)),
            "Raycast at close button center reaches its hierarchy without an overlay intercepting input.");
        closeButton.onClick.Invoke();
        Batch(null, new[] { a }, null);
        Check(!ui.IsPanelVisible && model.activeSelf, "Close keeps AR model visible and stays closed during tracking.");
        State(a, TrackingState.Limited);
        Batch(null, new[] { a }, null);
        Check(!tracker.IsTrackingTarget && !ui.IsPanelVisible && !model.activeSelf && transitions.Count == 2,
            "Limited tracking hides model and panel.");
        State(a, TrackingState.Tracking);
        Batch(null, new[] { a }, null);
        Check(ui.IsPanelVisible && model.activeSelf && transitions.Count == 3, "Reacquisition resets dismissal and restores panel.");

        Batch(new[] { b }, null, new[] { a.trackableId });
        Check(tracker.IsTrackingTarget && ui.IsPanelVisible && transitions.Count == 3 && Models().Count == 1,
            "Atomic target replacement does not flicker or publish false.");
        State(a, TrackingState.Tracking);
        Batch(new[] { a }, null, null);
        State(b, TrackingState.Limited);
        Batch(null, new[] { b }, null);
        Check(tracker.IsTrackingTarget && ui.IsPanelVisible && transitions.Count == 3,
            "One limited image cannot hide another tracking target.");
        Batch(null, null, new[] { a.trackableId });
        Check(!tracker.IsTrackingTarget && !ui.IsPanelVisible && transitions.Count == 4,
            "Removal with null Unity object clears last active target by ID.");
        State(b, TrackingState.Tracking);
        Batch(null, new[] { b }, null);
        Check(ui.IsPanelVisible && transitions.Count == 5, "Remaining image reacquires after removals.");
        tracker.enabled = false;
        Check(!tracker.IsTrackingTarget && !ui.IsPanelVisible && !Models()[b.trackableId].activeSelf && transitions.Count == 6,
            "Disabling tracker clears UI and hides retained models.");
        tracker.enabled = true;
        Check(!ui.IsPanelVisible && transitions.Count == 6, "Re-enable with disabled manager cannot restore stale tracking.");
        Batch(null, new[] { b }, null);
        Check(ui.IsPanelVisible && Models().Count == 1 && transitions.Count == 7,
            "New tracking after re-enable reuses retained model.");
        var removedId = b.trackableId;
        UnityEngine.Object.Destroy(b.gameObject);
        yield return null;
        Batch(null, null, new[] { removedId });
        Check(!tracker.IsTrackingTarget && !ui.IsPanelVisible && Models().Count == 0,
            "Destroyed tracked image and child model are safely removed.");
        tracker.EquipmentTrackingChanged -= RecordTransition;
    }

    static void RecordTransition(string name, bool value)
    {
        Check(name == "StepperMotor", "Event retains reference image name.");
        transitions.Add(value);
    }

    static void LogLayout(ScrollRect scroll, string label)
    {
        var canvas = ui.GetComponent<Canvas>();
        Results.Add(label + ": Screen=" + Screen.width + "x" + Screen.height +
            ", canvas=" + ((RectTransform)canvas.transform).rect + ", scale=" + canvas.scaleFactor +
            ", content=" + scroll.content.rect + ", viewport=" + scroll.viewport.rect);
        foreach (var text in scroll.content.GetComponentsInChildren<TMP_Text>())
            Results.Add(text.name + ": actual=" + text.rectTransform.rect + ", preferred=" +
                text.preferredWidth + "x" + text.preferredHeight + ", characters=" + text.text.Length);
        File.WriteAllLines(Evidence + "/playmode-layout.txt", Results);
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
        Results.Add("Editor synthetic AR events; no physical device or AR camera verification.");
        if (error != null) Results.Add(error);
        File.WriteAllLines(Evidence + "/playmode-tests.txt", Results);
        if (passed) Debug.Log("[MechaAR Phase2] PLAYMODE TESTS PASSED");
        else Debug.LogError("[MechaAR Phase2] PLAYMODE TESTS FAILED: " + error);
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        else EditorApplication.isPlaying = false;
    }
}
