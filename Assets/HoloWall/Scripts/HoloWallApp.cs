using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HoloWall
{
    /// <summary>
    /// Glue between the UI and the AR logic: splash/about/message panels, the control bar and the coaching hint.
    /// The UI hierarchy itself is created by the editor script (HoloWallBuild.SetupProject).
    /// </summary>
    public class HoloWallApp : MonoBehaviour
    {
        [Header("Logic")]
        public PlacementController placement;
        public HologramScreen screen;
        public ARStatusMonitor arStatus;

        [Header("Panels")]
        public GameObject splashPanel;
        public GameObject hudRoot;
        public GameObject aboutPanel;
        public GameObject messagePanel;

        [Header("HUD")]
        public TMP_Text clipLabel;
        public GameObject hintPanel;
        public TMP_Text hintText;
        public TMP_Text toastText;
        public Button prevButton;
        public Button playButton;
        public Button nextButton;
        public Button modeButton;
        public Button pickButton;
        public Button muteButton;
        public Button lockButton;
        public Button planesButton;
        public Button resetButton;
        public Button aboutButton;

        [Header("Splash / About / Message")]
        public Button startButton;
        public Button aboutCloseButton;
        public TMP_Text aboutVersionText;
        public TMP_Text messageTitle;
        public TMP_Text messageBody;
        public Button messageRetryButton;
        public Button messageCloseButton;

        [Header("Theme")]
        public Color buttonNormal = new Color(0.07f, 0.11f, 0.27f, 0.92f);
        public Color buttonActive = new Color(0.36f, 0.24f, 0.78f, 0.95f);

        Coroutine toastRoutine;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        void Start()
        {
            splashPanel.SetActive(true);
            hudRoot.SetActive(false);
            aboutPanel.SetActive(false);
            messagePanel.SetActive(false);
            placement.InputEnabled = false;
            if (aboutVersionText != null) aboutVersionText.text = "Version " + Application.version;

            startButton.onClick.AddListener(OnStart);
            aboutButton.onClick.AddListener(() => aboutPanel.SetActive(true));
            aboutCloseButton.onClick.AddListener(() => aboutPanel.SetActive(false));
            messageCloseButton.onClick.AddListener(() => messagePanel.SetActive(false));
            messageRetryButton.onClick.AddListener(() => { arStatus.RetryPermission(); messagePanel.SetActive(false); });

            prevButton.onClick.AddListener(() => PlayerCommand(() => screen.PreviousClip()));
            nextButton.onClick.AddListener(() => PlayerCommand(() => screen.NextClip()));
            playButton.onClick.AddListener(() => PlayerCommand(() => screen.TogglePlay()));
            modeButton.onClick.AddListener(() => PlayerCommand(() => screen.ToggleMode()));
            muteButton.onClick.AddListener(OnMute);
            pickButton.onClick.AddListener(OnPick);
            lockButton.onClick.AddListener(() => placement.ToggleLock());
            planesButton.onClick.AddListener(() => placement.TogglePlanes());
            resetButton.onClick.AddListener(OnReset);

            placement.HintChanged += OnHint;
            placement.LockChanged += locked => SetButton(lockButton, locked ? "Locked" : "Lock", locked);
            placement.PlanesVisibleChanged += v => SetButton(planesButton, v ? "Planes" : "Planes off", !v);
            placement.Placed += () => Toast("Placed. Pinch to resize, tap to move.");
            screen.ClipLabelChanged += label => clipLabel.text = label;
            screen.PlayingChanged += playing => SetButton(playButton, playing ? "Pause" : "Play", false);
            screen.ModeChanged += OnModeChanged;
            screen.Error += Toast;
            arStatus.ProblemChanged += OnARProblem;

            SetButton(lockButton, "Lock", false);
            SetButton(planesButton, "Planes", false);
            SetButton(muteButton, "Sound", false);
            SetButton(playButton, "Pause", false);
            SetButton(modeButton, "Hologram", false);
            clipLabel.text = "";
            OnHint(PlacementController.Hint.Scan);
        }

        void Update()
        {
            // Android back button / Escape closes panels first.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (aboutPanel.activeSelf) aboutPanel.SetActive(false);
                else if (messagePanel.activeSelf) messagePanel.SetActive(false);
                else if (hudRoot.activeSelf) Application.Quit();
            }
        }

        // ---------- Actions ----------

        void OnStart()
        {
            splashPanel.SetActive(false);
            hudRoot.SetActive(true);
            placement.InputEnabled = true;
        }

        void PlayerCommand(System.Action action)
        {
            if (!placement.IsPlaced) { Toast("Tap a wall first to place the screen."); return; }
            action();
        }

        void OnModeChanged(DisplayMode m) => SetButton(modeButton, m == DisplayMode.Hologram ? "Hologram" : "Screen", m == DisplayMode.Screen);

        void OnMute()
        {
            bool muted = !screen.IsMuted;
            screen.SetMuted(muted);
            SetButton(muteButton, muted ? "Muted" : "Sound", muted);
        }

        void OnPick()
        {
            if (!placement.IsPlaced) { Toast("Tap a wall first to place the screen."); return; }
            GalleryPicker.PickVideo(
                path => screen.PlayFile(path),
                message => Toast(message));
        }

        void OnReset()
        {
            placement.ResetScreen();
            clipLabel.text = "";
            Toast("Screen removed. Tap a wall to place it again.");
        }

        // ---------- Feedback ----------

        void OnHint(PlacementController.Hint hint)
        {
            string text = null;
            switch (hint)
            {
                case PlacementController.Hint.Scan: text = "Move your phone slowly to scan a wall"; break;
                case PlacementController.Hint.TapToPlace: text = "Aim at a wall and tap to place the hologram"; break;
                case PlacementController.Hint.TrackingLost: text = "Tracking lost. Move slowly and point at a textured surface"; break;
            }
            hintPanel.SetActive(text != null);
            if (text != null) hintText.text = text;
        }

        void OnARProblem(string title, string message, bool canRetry)
        {
            if (string.IsNullOrEmpty(message)) { messagePanel.SetActive(false); return; }
            messageTitle.text = title;
            messageBody.text = message;
            messageRetryButton.gameObject.SetActive(canRetry);
            messageCloseButton.gameObject.SetActive(canRetry);
            messagePanel.SetActive(true);
        }

        void SetButton(Button button, string label, bool active)
        {
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = label;
            var image = button.targetGraphic as Image;
            if (image != null) image.color = active ? buttonActive : buttonNormal;
        }

        void Toast(string message)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        IEnumerator ToastRoutine(string message)
        {
            toastText.text = message;
            toastText.transform.parent.gameObject.SetActive(true);
            yield return new WaitForSeconds(2.6f);
            toastText.transform.parent.gameObject.SetActive(false);
            toastRoutine = null;
        }
    }
}
