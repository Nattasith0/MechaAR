using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>Explicit, repeatable editor setup; never modifies a scene automatically on import.</summary>
public static class MechaARPhase2Setup
{
    public const string ScenePath = "Assets/Scenes/MechaAR_Main.unity";
    const string Evidence = ".agent-system/tasks/MECHA-PHASE2/evidence";
    const string FontPath = "Assets/UI/Fonts/MechaARThai SDF.asset";
    const string DataPath = "Assets/EquipmentData/StepperMotor.asset";

    [MenuItem("MechaAR/Phase 2/Install equipment UI")]
    public static void Install()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        Directory.CreateDirectory(Evidence);
        // Preserve the original once, including when invoked again after a failed setup.
        string backup = ".agent-system/tasks/MECHA-PHASE2/backups/" + ScenePath;
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if (!File.Exists(backup)) File.Copy(ScenePath, backup);
        AssetDatabase.Refresh();
        Require(TMP_Settings.instance != null, "Import the bundled TMP Essential Resources before running setup (Window > TextMeshPro > Import TMP Essential Resources).");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NotoSansThai.ttf");
            Require(source != null, "Noto Sans Thai source font is missing.");
            font = TMP_FontAsset.CreateFontAsset(source, 64, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            Require(font != null, "Could not create Thai TMP font.");
            font.name = "MechaARThai SDF";
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
        }
        var codepoints = Enumerable.Range(32, 95).Concat(Enumerable.Range(0x0e01, 0x3a))
            .Concat(Enumerable.Range(0x0e3f, 0x1d)).Concat(new[] { 0x2022 }).Select(v => (uint)v).ToArray();
        // Keep the verified Thai/Latin atlas in the player; allow future catalog glyphs too.
        var serializedFont = new SerializedObject(font);
        serializedFont.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
        serializedFont.ApplyModifiedPropertiesWithoutUndo();
        font.TryAddCharacters(codepoints, out uint[] missing, true);
        Require(missing == null || missing.Length == 0, "Thai/ASCII glyphs missing: " + string.Join(",", missing ?? Array.Empty<uint>()));
        TMP_Settings.defaultFontAsset = font;
        EditorUtility.SetDirty(TMP_Settings.instance);
        EditorUtility.SetDirty(font);
        Directory.CreateDirectory("Assets/EquipmentData");
        AssetDatabase.Refresh();
        var data = AssetDatabase.LoadAssetAtPath<EquipmentData>(DataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<EquipmentData>();
            data.equipmentId = "EQ001";
            data.referenceImageName = "StepperMotor";
            data.equipmentName = "Stepper Motor";
            data.category = "Electric Motor";
            data.thaiName = "มอเตอร์สเต็ป";
            data.description = "มอเตอร์ไฟฟ้าที่สามารถควบคุมการหมุนเป็นขั้นตามสัญญาณควบคุม เหมาะสำหรับงานที่ต้องการควบคุมตำแหน่งและมุมหมุน";
            data.workingPrinciple = "เมื่อจ่ายกระแสไฟฟ้าให้ขดลวดตามลำดับ จะเกิดสนามแม่เหล็กทำให้โรเตอร์หมุนไปทีละขั้นตามสัญญาณควบคุม";
            data.applications = new[] { "เครื่องพิมพ์ 3D", "เครื่อง CNC", "แขนกล", "ระบบควบคุมตำแหน่ง" };
            AssetDatabase.CreateAsset(data, DataPath);
        }
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var tracker = UnityEngine.Object.FindFirstObjectByType<ARImageTracking>();
        Require(tracker != null, "ARImageTracking missing.");
        var ui = UnityEngine.Object.FindFirstObjectByType<EquipmentInfoUI>(FindObjectsInactive.Include);
        if (ui == null)
        {
            var go = new GameObject("MechaAR Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            ui = go.AddComponent<EquipmentInfoUI>();
            ui.BuildUI(tracker, font, new[] { data });
        }
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        AssetDatabase.SaveAssets();
        Require(EditorSceneManager.SaveScene(scene), "Scene save failed.");
        Validate();
        Debug.Log("[MechaAR Phase2] INSTALL PASSED");
    }

    [MenuItem("MechaAR/Phase 2/Validate scene")]
    public static void Validate()
    {
        Directory.CreateDirectory(Evidence);
        var scene = EditorSceneManager.GetActiveScene();
        Require(scene.path == ScenePath, "Open MechaAR_Main before validation.");
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing script: " + transform.name);
        var ui = UnityEngine.Object.FindFirstObjectByType<EquipmentInfoUI>(FindObjectsInactive.Include);
        Require(ui != null, "Equipment UI missing.");
        var serializedUI = new SerializedObject(ui);
        foreach (string field in new[] { "imageTracking", "equipmentInfoPanel", "arStatusText", "scanStatusText", "equipmentNameText", "equipmentTypeText", "equipmentDescriptionText", "equipmentPrincipleText", "equipmentApplicationsText", "closeButton", "detailsScroll" })
            Require(serializedUI.FindProperty(field)?.objectReferenceValue != null, "UI reference missing: " + field);
        var catalog = serializedUI.FindProperty("equipmentCatalog");
        Require(catalog != null && catalog.arraySize > 0 && catalog.GetArrayElementAtIndex(0).objectReferenceValue != null, "Equipment catalog reference missing.");
        var canvas = ui.GetComponent<Canvas>();
        Require(canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay, "Overlay Canvas missing.");
        var scaler = ui.GetComponent<CanvasScaler>();
        Require(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && scaler.referenceResolution == new Vector2(1080, 1920) && Mathf.Approximately(scaler.matchWidthOrHeight, .5f), "Canvas scaler incorrect.");
        Require(ui.GetComponentsInChildren<TMP_Text>(true).Length >= 7, "TMP fields missing.");
        foreach (var text in ui.GetComponentsInChildren<TMP_Text>(true))
        {
            Require(text.font != null && text.font.material != null, "Font missing: " + text.name);
            string visible = System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]+>", "");
            Require(text.font.HasCharacters(visible, out uint[] missing, true, true), "Glyph missing in " + text.name + ": " + string.Join(",", missing ?? Array.Empty<uint>()));
        }
        var data = AssetDatabase.LoadAssetAtPath<EquipmentData>(DataPath);
        Require(data != null && data.equipmentId == "EQ001" && data.referenceImageName == "StepperMotor", "EQ001 catalog not configured.");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        string dataText = data.equipmentName + data.category + data.thaiName + data.description + data.workingPrinciple + string.Join("•", data.applications);
        Require(font.HasCharacters(dataText, out uint[] missingData, false, true), "Equipment glyphs missing: " + string.Join(",", missingData ?? Array.Empty<uint>()));
        var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        Require(apis.Length == 1 && apis[0] == GraphicsDeviceType.OpenGLES3, "Android must remain GLES3 only.");
        Require(EditorBuildSettings.scenes.First(s => s.enabled).path == ScenePath, "Main scene is not first enabled.");
        Require(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StepperMotor_AR.prefab") != null, "Stepper prefab missing.");
        File.WriteAllText(Evidence + "/scene-validation.txt", "PASSED: no missing scripts; Canvas/TMP/font/catalog; GLES3; first scene; prefab.\n" + DateTime.UtcNow.ToString("O"));
        Debug.Log("[MechaAR Phase2] SCENE VALIDATION PASSED");
    }

    public static void BuildAndroid()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Validate();
        Directory.CreateDirectory("Builds");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = "Builds/MechaAR-Phase2.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        });
        File.WriteAllText(Evidence + "/android-build.txt", $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nSize: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
        Require(report.summary.result == BuildResult.Succeeded, "Android build failed; see build log.");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
