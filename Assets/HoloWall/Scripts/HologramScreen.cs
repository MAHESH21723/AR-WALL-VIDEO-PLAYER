using System;
using UnityEngine;
using UnityEngine.Video;

namespace HoloWall
{
    /// <summary>How the video is shown on the screen quad.</summary>
    public enum DisplayMode
    {
        /// <summary>Green background removed, glowing hologram look.</summary>
        Hologram,
        /// <summary>Normal video with a thin glowing frame.</summary>
        Screen
    }

    /// <summary>
    /// The placed "hologram screen": a quad that shows a video through a chroma-key material.
    /// Owns the VideoPlayer, the RenderTexture and the clip/mode state. Placement is done by <see cref="PlacementController"/>.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class HologramScreen : MonoBehaviour
    {
        const int MaxRenderTextureSide = 1920;

        [Tooltip("Material using the HoloWall/HoloChromaKey shader. A runtime copy is made.")]
        public Material sourceMaterial;
        [Tooltip("Renderer of the quad.")]
        public MeshRenderer quadRenderer;
        [Tooltip("Height of the quad in metres at scale 1.")]
        public float baseHeight = 0.7f;
        public float minScale = 0.3f;
        public float maxScale = 3.5f;

        public event Action<string> ClipLabelChanged;
        public event Action<bool> PlayingChanged;
        public event Action<DisplayMode> ModeChanged;
        public event Action<string> Error;

        VideoPlayer player;
        AudioSource audioSource;
        Material material;
        RenderTexture renderTexture;
        float aspect = 16f / 9f;
        float userScale = 1f;
        bool muted;

        int builtinIndex;
        bool usingGalleryFile;
        string currentLabel = "";
        DisplayMode mode = DisplayMode.Hologram;
        bool preparing;

        public DisplayMode Mode => mode;
        public bool IsMuted => muted;
        public bool IsPlaying => player != null && player.isPlaying;
        public string CurrentLabel => currentLabel;
        public float UserScale => userScale;

        void Awake()
        {
            player = GetComponent<VideoPlayer>();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            player.playOnAwake = false;
            player.isLooping = true;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount = 1;
            player.SetTargetAudioSource(0, audioSource);
            player.prepareCompleted += OnPrepared;
            player.errorReceived += OnVideoError;

            material = new Material(sourceMaterial);
            quadRenderer.sharedMaterial = material;
            ApplyMode(DisplayMode.Hologram, notify: false);
            ApplyScale();
        }

        void OnDestroy()
        {
            if (player != null)
            {
                player.prepareCompleted -= OnPrepared;
                player.errorReceived -= OnVideoError;
            }
            ReleaseTexture();
            if (material != null) Destroy(material);
        }

        // ---------- Clips ----------

        /// <summary>Plays one of the built-in hologram clips (shown with the green removed by default).</summary>
        public void PlayBuiltin(int index, bool keepMode = false)
        {
            int count = HologramClips.All.Length;
            builtinIndex = ((index % count) + count) % count;
            usingGalleryFile = false;
            var clip = HologramClips.All[builtinIndex];
            if (!keepMode) ApplyMode(DisplayMode.Hologram);
            Load(clip.Url, clip.Label);
        }

        /// <summary>Plays a video file picked from the gallery (shown as a normal screen by default).</summary>
        public void PlayFile(string path)
        {
            usingGalleryFile = true;
            string label = System.IO.Path.GetFileNameWithoutExtension(path);
            ApplyMode(DisplayMode.Screen);
            string url = path.StartsWith("file://", StringComparison.Ordinal) || path.Contains("://") ? path : "file://" + path;
            Load(url, label);
        }

        public void NextClip() => StepClip(+1);
        public void PreviousClip() => StepClip(-1);

        void StepClip(int delta)
        {
            // From a gallery video "next/previous" returns to the built-in list.
            int start = usingGalleryFile ? (delta > 0 ? -1 : HologramClips.All.Length) : builtinIndex;
            PlayBuiltin(start + delta);
        }

        void Load(string url, string label)
        {
            currentLabel = label;
            ClipLabelChanged?.Invoke(label);
            preparing = true;
            player.Stop();
            player.source = VideoSource.Url;
            player.url = url;
            player.Prepare();
        }

        void OnPrepared(VideoPlayer vp)
        {
            preparing = false;
            EnsureRenderTexture((int)vp.width, (int)vp.height);
            vp.Play();
            audioSource.mute = muted;
            PlayingChanged?.Invoke(true);
        }

        void OnVideoError(VideoPlayer vp, string message)
        {
            preparing = false;
            Debug.LogWarning("HoloWall video error: " + message);
            Error?.Invoke("Could not play this video.");
        }

        void EnsureRenderTexture(int w, int h)
        {
            if (w <= 0 || h <= 0) { w = 1280; h = 720; }
            float f = Mathf.Min(1f, MaxRenderTextureSide / (float)Mathf.Max(w, h));
            w = Mathf.Max(16, Mathf.RoundToInt(w * f));
            h = Mathf.Max(16, Mathf.RoundToInt(h * f));

            if (renderTexture == null || renderTexture.width != w || renderTexture.height != h)
            {
                ReleaseTexture();
                renderTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "HoloWall Video RT" };
                renderTexture.Create();
            }
            player.targetTexture = renderTexture;
            material.SetTexture("_MainTex", renderTexture);
            aspect = w / (float)h;
            ApplyScale();
        }

        void ReleaseTexture()
        {
            if (renderTexture == null) return;
            if (player != null) player.targetTexture = null;
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }

        // ---------- Playback / state ----------

        public void TogglePlay()
        {
            if (preparing) return;
            if (player.isPlaying) player.Pause(); else player.Play();
            PlayingChanged?.Invoke(player.isPlaying);
        }

        public void SetMuted(bool value)
        {
            muted = value;
            if (audioSource != null) audioSource.mute = value;
        }

        public void ToggleMode() => ApplyMode(mode == DisplayMode.Hologram ? DisplayMode.Screen : DisplayMode.Hologram);

        public void SetCyanTint(bool on) => material.SetFloat("_CyanTint", on ? 1f : 0f);

        void ApplyMode(DisplayMode m, bool notify = true)
        {
            mode = m;
            material.SetFloat("_Hologram", m == DisplayMode.Hologram ? 1f : 0f);
            if (notify) ModeChanged?.Invoke(m);
        }

        // ---------- Size ----------

        public void SetUserScale(float s)
        {
            userScale = Mathf.Clamp(s, minScale, maxScale);
            ApplyScale();
        }

        void ApplyScale()
        {
            float h = baseHeight * userScale;
            transform.localScale = new Vector3(h * aspect, h, 1f);
        }

        public void ResetState()
        {
            userScale = 1f;
            ApplyScale();
        }
    }
}
