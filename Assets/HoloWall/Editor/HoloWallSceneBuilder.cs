using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.ARFoundation;

namespace HoloWall.Editor
{
    /// <summary>Builds the Main scene (AR rig, hologram screen, reticle and the whole UI) from code.</summary>
    public static class HoloWallSceneBuilder
    {
        // Theme: cyan / violet on navy.
        static readonly Color Navy = new Color(0.035f, 0.055f, 0.15f, 1f);
        static readonly Color Panel = new Color(0.03f, 0.05f, 0.14f, 0.82f);
        static readonly Color Card = new Color(0.07f, 0.1f, 0.26f, 0.96f);
        static readonly Color Cyan = new Color(0.25f, 0.9f, 1f, 1f);
        static readonly Color Violet = new Color(0.62f, 0.45f, 1f, 1f);
        static readonly Color TextMain = new Color(0.9f, 0.96f, 1f, 1f);
        static readonly Color TextDim = new Color(0.65f, 0.75f, 0.92f, 1f);
        static readonly Color ButtonBg = new Color(0.07f, 0.11f, 0.27f, 0.92f);

        public class UiSprites
        {
            public Sprite Rounded;
            public Sprite Logo;
        }

        static TMP_FontAsset font;
        static UiSprites sprites;

        // ---------- UI sprites ----------

        public static UiSprites CreateUiSprites()
        {
            const string path = HoloWallBuild.UiDir + "/Rounded.png";
            if (!File.Exists(path))
            {
                const int size = 64;
                const float radius = 22f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Signed distance to a rounded rectangle, anti-aliased over one pixel.
                        float px = Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius);
                        float py = Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius);
                        float d = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                        float a = Mathf.Clamp01(0.5f - d);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spriteBorder = new Vector4(24, 24, 24, 24);
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }

            return new UiSprites
            {
                Rounded = AssetDatabase.LoadAssetAtPath<Sprite>(path),
                Logo = AssetDatabase.LoadAssetAtPath<Sprite>(HoloWallBuild.IconDir + "/Logo.png")
            };
        }

        // ---------- Scene ----------

        public static void BuildScene(TMP_FontAsset fontAsset, GameObject planePrefab, UiSprites uiSprites)
        {
            font = fontAsset;
            sprites = uiSprites;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- AR session + rig ---
            var sessionGo = new GameObject("AR Session");
            var session = sessionGo.AddComponent<ARSession>();

            var originGo = new GameObject("XR Origin");
            var origin = originGo.AddComponent<XROrigin>();
            var planeManager = originGo.AddComponent<ARPlaneManager>();
            var raycastManager = originGo.AddComponent<ARRaycastManager>();
            planeManager.planePrefab = planePrefab;
            planeManager.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal | UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Vertical;

            var offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(offsetGo.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            camGo.AddComponent<AudioListener>();
            var camManager = camGo.AddComponent<ARCameraManager>();
            // Draw the camera feed first so every virtual object is drawn on top of it.
            camManager.requestedBackgroundRenderingMode = CameraBackgroundRenderingMode.BeforeOpaques;
            camGo.AddComponent<ARCameraBackground>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;

            // Camera follows the phone via the XR device API (see XRCameraPoseDriver for why).
            camGo.AddComponent<XRCameraPoseDriver>();

            origin.Origin = originGo;
            origin.CameraFloorOffsetObject = offsetGo;
            origin.Camera = cam;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;

            // --- Hologram screen ---
            var screenGo = new GameObject("Hologram Screen");
            var mf = screenGo.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mr = screenGo.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            screenGo.AddComponent<VideoPlayer>();
            screenGo.AddComponent<AudioSource>().playOnAwake = false;
            var screen = screenGo.AddComponent<HologramScreen>();
            screen.sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(HoloWallBuild.MaterialsDir + "/HologramScreen.mat");
            screen.quadRenderer = mr;
            screenGo.SetActive(false);

            // --- Reticle ---
            var reticleGo = new GameObject("Placement Reticle");
            reticleGo.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var rmr = reticleGo.AddComponent<MeshRenderer>();
            rmr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(HoloWallBuild.MaterialsDir + "/Reticle.mat");
            rmr.shadowCastingMode = ShadowCastingMode.Off;
            rmr.receiveShadows = false;
            reticleGo.transform.localScale = Vector3.one * 0.16f;
            reticleGo.SetActive(false);

            // --- Logic ---
            var logicGo = new GameObject("HoloWall");
            var placement = logicGo.AddComponent<PlacementController>();
            placement.planeManager = planeManager;
            placement.raycastManager = raycastManager;
            placement.arCamera = cam;
            placement.screen = screen;
            placement.reticle = reticleGo.transform;

            var status = logicGo.AddComponent<ARStatusMonitor>();
            status.session = session;

            var app = logicGo.AddComponent<HoloWallApp>();
            app.placement = placement;
            app.screen = screen;
            app.arStatus = status;

            BuildUi(app);

            EditorSceneManager.SaveScene(scene, HoloWallBuild.ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(HoloWallBuild.ScenePath, true) };
        }

        // ---------- UI ----------

        static void BuildUi(HoloWallApp app)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();

            // ===== HUD =====
            var hud = NewRect("HUD", canvasGo.transform);
            Stretch(hud);
            var hudSafe = NewRect("SafeArea", hud);
            Stretch(hudSafe);
            hudSafe.gameObject.AddComponent<SafeAreaFitter>();
            app.hudRoot = hud.gameObject;

            // Top bar
            var top = NewRect("TopBar", hudSafe);
            SetAnchored(top, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(-32, 140));
            AddImage(top, Panel, sprites.Rounded, true);

            var logo = NewRect("Logo", top);
            SetAnchored(logo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(96, 96));
            var logoImg = AddImage(logo, Color.white, sprites.Logo, false);
            logoImg.type = Image.Type.Simple;
            logoImg.preserveAspect = true;

            var title = NewText("Title", top, "HoloWall", 64, Cyan, TextAlignmentOptions.MidlineLeft);
            SetAnchored(title.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            title.rectTransform.offsetMin = new Vector2(138, 0);
            title.rectTransform.offsetMax = new Vector2(-200, 0);

            app.aboutButton = NewButton("AboutButton", top, "About", 36, 24, 150, 84);
            var aboutRt = (RectTransform)app.aboutButton.transform;
            SetAnchored(aboutRt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-22, 0), new Vector2(150, 84));

            // Clip label
            app.clipLabel = NewText("ClipLabel", hudSafe, "", 48, TextMain, TextAlignmentOptions.Center);
            SetAnchored(app.clipLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(900, 64));

            // Coaching hint
            var hint = NewRect("HintPanel", hudSafe);
            SetAnchored(hint, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(940, 130));
            AddImage(hint, new Color(0.03f, 0.05f, 0.14f, 0.72f), sprites.Rounded, false);
            app.hintPanel = hint.gameObject;
            app.hintText = NewText("Text", hint, "Move your phone slowly to scan a wall", 42, TextMain, TextAlignmentOptions.Center);
            Stretch(app.hintText.rectTransform, 24, 8);

            // Toast
            var toast = NewRect("Toast", hudSafe);
            SetAnchored(toast, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 350), new Vector2(940, 96));
            AddImage(toast, new Color(0.36f, 0.24f, 0.78f, 0.92f), sprites.Rounded, false);
            app.toastText = NewText("Text", toast, "", 38, Color.white, TextAlignmentOptions.Center);
            Stretch(app.toastText.rectTransform, 20, 6);
            toast.gameObject.SetActive(false);

            // Bottom controls
            var bottom = NewRect("BottomBar", hudSafe);
            SetAnchored(bottom, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(-32, 330));
            AddImage(bottom, Panel, sprites.Rounded, true);
            var vlg = bottom.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(22, 22, 22, 22);
            vlg.spacing = 16;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = vlg.childForceExpandHeight = true;

            var row1 = NewRow("TransportRow", bottom);
            app.prevButton = NewButton("PrevButton", row1, "Prev", 40, 24, 0, 0);
            app.playButton = NewButton("PlayButton", row1, "Pause", 40, 24, 0, 0);
            app.nextButton = NewButton("NextButton", row1, "Next", 40, 24, 0, 0);
            app.modeButton = NewButton("ModeButton", row1, "Hologram", 40, 24, 0, 0);

            var row2 = NewRow("ToolRow", bottom);
            app.pickButton = NewButton("PickButton", row2, "Pick video", 34, 20, 0, 0);
            app.muteButton = NewButton("MuteButton", row2, "Sound", 34, 20, 0, 0);
            app.lockButton = NewButton("LockButton", row2, "Lock", 34, 20, 0, 0);
            app.planesButton = NewButton("PlanesButton", row2, "Planes", 34, 20, 0, 0);
            app.resetButton = NewButton("ResetButton", row2, "Reset", 34, 20, 0, 0);

            // ===== About panel =====
            var about = NewOverlay("AboutPanel", canvasGo.transform, out var aboutCard);
            app.aboutPanel = about.gameObject;
            AddCardText(aboutCard, "About HoloWall", 62, Cyan);
            app.aboutVersionText = AddCardText(aboutCard, "Version " + HoloWallBuild.Version, 40, TextDim);
            AddCardText(aboutCard, "AR hologram video player", 42, TextMain);
            AddCardText(aboutCard, "Made by Maheshwaran", 48, Violet);
            AddCardText(aboutCard, "github.com/MAHESH21723", 40, Cyan);
            AddCardText(aboutCard, "Gallery picker: NativeGallery by yasirkula (MIT). Font: Rajdhani (SIL OFL).", 30, TextDim);
            app.aboutCloseButton = AddCardButton(aboutCard, "Close");

            // ===== Message panel =====
            var message = NewOverlay("MessagePanel", canvasGo.transform, out var msgCard);
            app.messagePanel = message.gameObject;
            app.messageTitle = AddCardText(msgCard, "Title", 58, Cyan);
            app.messageBody = AddCardText(msgCard, "Message", 40, TextMain);
            app.messageRetryButton = AddCardButton(msgCard, "Allow camera");
            app.messageCloseButton = AddCardButton(msgCard, "Close");

            // ===== Splash / start panel (topmost) =====
            var splash = NewRect("SplashPanel", canvasGo.transform);
            Stretch(splash);
            AddImage(splash, Navy, null, true);
            app.splashPanel = splash.gameObject;

            var glow = NewRect("GlowTop", splash);
            SetAnchored(glow, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 200), new Vector2(1500, 900));
            AddImage(glow, new Color(0.25f, 0.9f, 1f, 0.07f), sprites.Rounded, false);

            var content = NewRect("Content", splash);
            Stretch(content);
            content.gameObject.AddComponent<SafeAreaFitter>();
            var cv = content.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.padding = new RectOffset(60, 60, 90, 60);
            cv.spacing = 20;
            cv.childAlignment = TextAnchor.UpperCenter;
            cv.childControlWidth = cv.childControlHeight = true;
            cv.childForceExpandWidth = false;
            cv.childForceExpandHeight = false;

            var logoBig = NewRect("Logo", content);
            var lbImg = AddImage(logoBig, Color.white, sprites.Logo, false);
            lbImg.type = Image.Type.Simple;
            lbImg.preserveAspect = true;
            SetLayout(logoBig, 320, 320);

            var t1 = NewText("Title", content, "HoloWall", 124, Cyan, TextAlignmentOptions.Center);
            SetLayout(t1.rectTransform, 940, -1);
            var t2 = NewText("Tagline", content, "AR hologram video player", 50, TextMain, TextAlignmentOptions.Center);
            SetLayout(t2.rectTransform, 940, -1);
            var t3 = NewText("Credit", content, "Made by Maheshwaran", 44, Violet, TextAlignmentOptions.Center);
            SetLayout(t3.rectTransform, 940, -1);

            var how = NewRect("HowTo", content);
            AddImage(how, Card, sprites.Rounded, false);
            var hv = how.gameObject.AddComponent<VerticalLayoutGroup>();
            hv.padding = new RectOffset(40, 40, 32, 32);
            hv.spacing = 14;
            hv.childControlWidth = hv.childControlHeight = true;
            hv.childForceExpandWidth = true;
            hv.childForceExpandHeight = false;
            SetLayout(how, 940, -1);
            NewText("Heading", how, "How to use", 46, Cyan, TextAlignmentOptions.MidlineLeft);
            NewText("Step1", how, "1.  Point your phone at a wall and move it slowly.", 38, TextMain, TextAlignmentOptions.TopLeft);
            NewText("Step2", how, "2.  Tap a detected surface to place the hologram.", 38, TextMain, TextAlignmentOptions.TopLeft);
            NewText("Step3", how, "3.  Pinch to resize, or pick your own video.", 38, TextMain, TextAlignmentOptions.TopLeft);

            var spacer = NewRect("Spacer", content);
            var sp = spacer.gameObject.AddComponent<LayoutElement>();
            sp.flexibleHeight = 1;

            app.startButton = NewButton("StartButton", content, "Start", 62, 30, 600, 150);
            var startImg = (Image)app.startButton.targetGraphic;
            startImg.color = new Color(0.36f, 0.24f, 0.78f, 1f);
            SetLayout((RectTransform)app.startButton.transform, 600, 150);

            // Wire the HUD default colours.
            app.buttonNormal = ButtonBg;
        }

        // ---------- UI helpers ----------

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r, float padX = 0, float padY = 0)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(padX, padY);
            r.offsetMax = new Vector2(-padX, -padY);
        }

        static void SetAnchored(RectTransform r, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static void SetLayout(RectTransform r, float width, float height)
        {
            var le = r.GetComponent<LayoutElement>() ?? r.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) le.preferredWidth = width;
            if (height >= 0) le.preferredHeight = height;
        }

        static Image AddImage(RectTransform r, Color color, Sprite sprite, bool raycast)
        {
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = raycast;
            return img;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Button NewButton(string name, Transform parent, string label, float fontSize, float minFont, float width, float height)
        {
            var rt = NewRect(name, parent);
            var img = AddImage(rt, ButtonBg, sprites.Rounded, true);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.65f, 0.8f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var text = NewText("Label", rt, label, fontSize, TextMain, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 6, 4);
            text.enableAutoSizing = true;
            text.fontSizeMin = minFont;
            text.fontSizeMax = fontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            if (width > 0 && height > 0) SetLayout(rt, width, height);
            return button;
        }

        static RectTransform NewRow(string name, Transform parent)
        {
            var rt = NewRect(name, parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            return rt;
        }

        static RectTransform NewOverlay(string name, Transform parent, out RectTransform card)
        {
            var overlay = NewRect(name, parent);
            Stretch(overlay);
            AddImage(overlay, new Color(0.01f, 0.02f, 0.07f, 0.82f), null, true);

            var safe = NewRect("SafeArea", overlay);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            card = NewRect("Card", safe);
            SetAnchored(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 100));
            AddImage(card, Card, sprites.Rounded, true);
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(48, 48, 44, 44);
            v.spacing = 22;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return overlay;
        }

        static TextMeshProUGUI AddCardText(RectTransform card, string text, float size, Color color)
        {
            return NewText("Text", card, text, size, color, TextAlignmentOptions.Center);
        }

        static Button AddCardButton(RectTransform card, string label)
        {
            var b = NewButton(label + "Button", card, label, 44, 26, 0, 0);
            SetLayout((RectTransform)b.transform, 600, 120);
            return b;
        }
    }
}
