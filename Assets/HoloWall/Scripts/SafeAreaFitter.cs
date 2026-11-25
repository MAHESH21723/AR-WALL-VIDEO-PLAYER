using UnityEngine;

namespace HoloWall
{
    /// <summary>Keeps a RectTransform inside the screen's safe area (notches, rounded corners, gesture bars).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rect;
        Rect lastSafeArea;
        Vector2Int lastScreenSize;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != lastSafeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
                Apply();
        }

        void Apply()
        {
            if (rect == null) rect = (RectTransform)transform;
            Rect safe = Screen.safeArea;
            lastSafeArea = safe;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
