using UnityEngine;
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaPanel : MonoBehaviour
{
    Rect lastSafeArea;
    Vector2Int lastScreen;
    void OnEnable() { Apply(); }
    void Update()
    {
        if (Screen.safeArea != lastSafeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height) Apply();
    }
    void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        lastSafeArea = Screen.safeArea;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        var rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
        rect.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
