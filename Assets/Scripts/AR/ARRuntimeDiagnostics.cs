using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

/// <summary>
/// Scene-local diagnostics. ARSession/ARCore remain the sole owners of permission,
/// availability and installation requests; this component never requests camera images.
/// </summary>
public sealed class ARRuntimeDiagnostics : MonoBehaviour
{
    private const string MainScenePath = "Assets/Scenes/MechaAR_Main.unity";
    private ARSession session;
    private ARCameraManager cameraManager;
    private float startedAt;
    private float nextLogAt;
    private float lastFrameAt = -1f;
    private int frames;
    private string status = "Starting AR...";
    private GUIStyle messageStyle;
    private GUIStyle buttonStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        // Avoid duplicate subscriptions when entering Play Mode without domain reload.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.path != MainScenePath)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<ARRuntimeDiagnostics>(true) != null)
                return;
        }

        var host = new GameObject("AR Runtime Diagnostics");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<ARRuntimeDiagnostics>();
    }

    private void Start()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (session == null)
                session = root.GetComponentInChildren<ARSession>();
            if (cameraManager == null)
                cameraManager = root.GetComponentInChildren<ARCameraManager>();
        }

        if (cameraManager != null)
            cameraManager.frameReceived += OnCameraFrame;
        startedAt = Time.unscaledTime;
        nextLogAt = startedAt;
    }

    private void OnDestroy()
    {
        if (cameraManager != null)
            cameraManager.frameReceived -= OnCameraFrame;
    }

    private void OnCameraFrame(ARCameraFrameEventArgs args)
    {
        ++frames;
        lastFrameAt = Time.unscaledTime;
    }

    private static bool HasCameraPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Permission.HasUserAuthorizedPermission(Permission.Camera);
#else
        return true;
#endif
    }

    private void Update()
    {
        // Poll status at a bounded rate, including frame counts but never camera pixels.
        if (Time.unscaledTime < nextLogAt)
            return;

        nextLogAt = Time.unscaledTime + 5f;
        var loader = XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null
            ? XRGeneralSettings.Instance.Manager.activeLoader : null;
        status = $"AR: {ARSession.state} | Reason: {ARSession.notTrackingReason}\n" +
                 $"Camera frames / 5s: {frames} | Permission: {HasCameraPermission()}";
        Debug.Log($"[MechaAR AR] {status.Replace('\n', ' ')} | " +
                  $"Loader: {(loader != null ? loader.name : "none")} | " +
                  $"Session running: {session != null && session.subsystem != null && session.subsystem.running} | " +
                  $"Camera running: {cameraManager != null && cameraManager.subsystem != null && cameraManager.subsystem.running} | " +
                  $"Facing: {(cameraManager != null ? cameraManager.currentFacingDirection.ToString() : "missing")} | " +
                  $"Pipeline: {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name ?? "none"}", this);
        frames = 0;
    }

    private string GetGuidance()
    {
        if (session == null || cameraManager == null)
            return "AR setup is incomplete. AR Session or AR Camera Manager is missing.";
        if (!HasCameraPermission())
            return "Camera access is required to scan images. Allow the camera permission. " +
                   "If you denied it, open Settings > Permissions > Camera, allow access, then return here.";
        switch (ARSession.state)
        {
            case ARSessionState.Unsupported:
                return "AR is unavailable. Check device support and Google Play Services for AR. " +
                       "Update it in Google Play, then reopen MechaAR.";
            case ARSessionState.NeedsInstall:
                return "Google Play Services for AR must be installed or updated. " +
                       "Complete the Android prompt, or tap Retry AR if you dismissed it.";
            case ARSessionState.Installing:
                return "Installing Google Play Services for AR. Complete the Android prompt.";
            case ARSessionState.CheckingAvailability:
                return "Checking whether this device supports AR...";
        }

        if (Time.unscaledTime - startedAt > 15f &&
            (lastFrameAt < 0f || Time.unscaledTime - lastFrameAt > 15f))
            return "No recent AR camera frames. Check camera permission, Google Play Services for AR " +
                   "and whether another app is using the camera. See the [MechaAR AR] device logs.";
        return null;
    }

    private void OnGUI()
    {
        // IMGUI keeps failure guidance available even when XR rendering is unavailable.
        string guidance = GetGuidance();
        if (guidance == null && !Debug.isDebugBuild)
            return;

        float scale = Mathf.Max(1f, Screen.width / 480f);
        messageStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
        buttonStyle ??= new GUIStyle(GUI.skin.button);
        messageStyle.fontSize = Mathf.RoundToInt(15f * scale);
        buttonStyle.fontSize = Mathf.RoundToInt(15f * scale);
        float width = Screen.safeArea.width - 24f * scale;
        string text = guidance ?? status;
        if (guidance != null && Debug.isDebugBuild)
            text += "\n" + status;
        float textHeight = messageStyle.CalcHeight(new GUIContent(text), width - 20f * scale);
        float buttonHeight = 44f * scale;
        Rect panel = new Rect(Screen.safeArea.x + 12f * scale,
            Screen.height - Screen.safeArea.yMax + 12f * scale,
            width, textHeight + buttonHeight + 24f * scale);
        GUI.Box(panel, GUIContent.none);
        GUILayout.BeginArea(new Rect(panel.x + 10f * scale, panel.y + 8f * scale,
            panel.width - 20f * scale, panel.height - 16f * scale));
        GUILayout.Label(text, messageStyle);
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!HasCameraPermission() && GUILayout.Button("Open app settings", buttonStyle, GUILayout.Height(buttonHeight)))
            OpenAppSettings();
#endif
        // Do not interrupt ARSession's availability/install coroutine or running session.
        if (session != null && HasCameraPermission() && !session.enabled &&
            (ARSession.state == ARSessionState.NeedsInstall || ARSession.state == ARSessionState.Ready) &&
            GUILayout.Button("Retry AR", buttonStyle, GUILayout.Height(buttonHeight)))
        {
            startedAt = Time.unscaledTime;
            session.enabled = true;
        }
        GUILayout.EndArea();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void OpenAppSettings()
    {
        try
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS");
            using var uriClass = new AndroidJavaClass("android.net.Uri");
            using var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "package:" + Application.identifier);
            intent.Call<AndroidJavaObject>("setData", uri).Dispose();
            activity.Call("startActivity", intent);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"[MechaAR AR] Could not open app settings ({exception.GetType().Name}). " +
                             "Open Android Settings > Apps > MechaAR > Permissions > Camera manually.");
        }
    }
#endif
}
