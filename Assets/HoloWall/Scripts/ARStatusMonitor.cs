using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace HoloWall
{
    /// <summary>
    /// Watches the ARCore session and reports problems (unsupported device, ARCore missing, camera permission)
    /// as friendly messages instead of leaving the user with a black screen.
    /// </summary>
    public class ARStatusMonitor : MonoBehaviour
    {
        public ARSession session;

        [Tooltip("Seconds to wait for tracking before checking whether the camera permission was denied.")]
        public float permissionCheckDelay = 6f;

        /// <summary>Raised with (title, message, canRetry). An empty message clears the problem.</summary>
        public event Action<string, string, bool> ProblemChanged;

        bool shownProblem;

        IEnumerator Start()
        {
            yield return ARSession.CheckAvailability();

            switch (ARSession.state)
            {
                case ARSessionState.Unsupported:
                    Report("AR not supported",
                        "This phone does not support ARCore, so HoloWall cannot detect walls here.\n\nTry a phone from the ARCore supported devices list.", false);
                    if (session != null) session.enabled = false;
                    yield break;

                case ARSessionState.NeedsInstall:
                    // Ask Google Play to install or update "Google Play Services for AR".
                    yield return ARSession.Install();
                    if (ARSession.state == ARSessionState.NeedsInstall || ARSession.state == ARSessionState.Unsupported)
                    {
                        Report("ARCore is needed",
                            "HoloWall needs \"Google Play Services for AR\". Install it from Google Play, then reopen the app.", false);
                        yield break;
                    }
                    break;
            }

            ARSession.stateChanged += OnStateChanged;
            StartCoroutine(CheckCameraPermission());
        }

        void OnDestroy() => ARSession.stateChanged -= OnStateChanged;

        void OnStateChanged(ARSessionStateChangedEventArgs args)
        {
            if (args.state == ARSessionState.Unsupported)
            {
                Report("AR not supported", "This phone does not support ARCore, so HoloWall cannot detect walls here.", false);
            }
            else if (args.state == ARSessionState.SessionTracking && shownProblem)
            {
                shownProblem = false;
                ProblemChanged?.Invoke("", "", false);
            }
        }

        IEnumerator CheckCameraPermission()
        {
            yield return new WaitForSeconds(permissionCheckDelay);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (ARSession.state != ARSessionState.SessionTracking && !Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Report("Camera access needed",
                    "HoloWall needs the camera to see your walls. Allow camera access when asked, or enable it in Settings > Apps > HoloWall > Permissions.", true);
            }
#endif
        }

        /// <summary>Asks for the camera permission again (used by the Retry button).</summary>
        public void RetryPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Permission.RequestUserPermission(Permission.Camera);
#endif
            shownProblem = false;
            ProblemChanged?.Invoke("", "", false);
            StartCoroutine(CheckCameraPermission());
        }

        void Report(string title, string message, bool canRetry)
        {
            shownProblem = true;
            ProblemChanged?.Invoke(title, message, canRetry);
        }
    }
}
