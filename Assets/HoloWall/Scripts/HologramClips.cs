using UnityEngine;

namespace HoloWall
{
    /// <summary>
    /// The built-in hologram clips shipped in StreamingAssets/Holograms.
    /// All of them are rendered on a pure green (0,255,0) background so the chroma-key shader can remove it.
    /// </summary>
    public static class HologramClips
    {
        public readonly struct Clip
        {
            public readonly string Label;
            public readonly string FileName;

            public Clip(string label, string fileName)
            {
                Label = label;
                FileName = fileName;
            }

            /// <summary>URL usable by VideoPlayer on every platform (on Android it points inside the APK).</summary>
            public string Url => Application.streamingAssetsPath + "/Holograms/" + FileName;
        }

        public static readonly Clip[] All =
        {
            new Clip("Globe", "holo_globe.mp4"),
            new Clip("Logo", "holo_logo.mp4"),
            new Clip("Reactor", "holo_reactor.mp4"),
            new Clip("Radar", "holo_radar.mp4"),
        };
    }
}
