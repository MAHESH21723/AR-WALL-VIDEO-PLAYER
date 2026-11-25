using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace HoloWall
{
    /// <summary>
    /// Handles plane detection, the placement reticle and all touch input in the AR view:
    /// tap to place/move the hologram screen, pinch to scale, twist to rotate, lock and reset.
    /// </summary>
    public class PlacementController : MonoBehaviour
    {
        /// <summary>What the coaching hint should currently say.</summary>
        public enum Hint { None, Scan, TapToPlace, Placed, TrackingLost }

        [Header("AR")]
        public ARPlaneManager planeManager;
        public ARRaycastManager raycastManager;
        public Camera arCamera;

        [Header("Scene objects")]
        public HologramScreen screen;
        public Transform reticle;

        [Header("Tuning")]
        [Tooltip("Gap between the wall and the screen quad, to avoid z-fighting.")]
        public float wallOffset = 0.01f;
        [Tooltip("A touch that moves less than this (inches) and is shorter than maxTapSeconds counts as a tap.")]
        public float tapMaxInches = 0.15f;
        public float maxTapSeconds = 0.35f;

        public event Action<Hint> HintChanged;
        public event Action<bool> LockChanged;
        public event Action<bool> PlanesVisibleChanged;
        public event Action Placed;

        /// <summary>Input is ignored until the user pressed Start on the splash screen.</summary>
        public bool InputEnabled { get; set; }

        public bool IsLocked { get; private set; }
        public bool PlanesVisible { get; private set; } = true;
        public bool IsPlaced { get; private set; }

        readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        Hint currentHint = Hint.None;
        bool reticleValid;
        Pose reticlePose;
        bool reticleOnFloor;
        float reticleNormalY;

        // Touch bookkeeping.
        int trackedTouchId = -1;
        Vector2 touchStart;
        double touchStartTime;
        bool touchMoved;
        bool touchStartedOverUi;

        // Two-finger gesture.
        float lastPinchDistance;
        float lastTwistAngle;
        bool twoFingerActive;

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            if (planeManager != null)
            {
                planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
                planeManager.trackablesChanged.AddListener(OnPlanesChanged);
            }
        }

        void OnDisable()
        {
            if (planeManager != null) planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
            EnhancedTouchSupport.Disable();
        }

        void Start()
        {
            if (reticle != null) reticle.gameObject.SetActive(false);
            SetHint(Hint.Scan);
        }

        void Update()
        {
            UpdateHint();
            UpdateReticle();
            if (!InputEnabled) return;
            HandleTouches();
        }

        // ---------- Hint ----------

        void UpdateHint()
        {
            Hint h;
            if (!InputEnabled) h = Hint.None;
            else if (IsPlaced) h = Hint.Placed;
            else if (planeManager != null && planeManager.trackables.count > 0) h = Hint.TapToPlace;
            else if (ARSession.state == ARSessionState.SessionInitializing && ARSession.notTrackingReason != NotTrackingReason.None && ARSession.notTrackingReason != NotTrackingReason.Initializing) h = Hint.TrackingLost;
            else h = Hint.Scan;
            SetHint(h);
        }

        void SetHint(Hint h)
        {
            if (h == currentHint) return;
            currentHint = h;
            HintChanged?.Invoke(h);
        }

        // ---------- Planes ----------

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            // New planes inherit the current visibility setting.
            foreach (var plane in args.added) plane.gameObject.SetActive(PlanesVisible);
        }

        public void SetPlanesVisible(bool visible)
        {
            PlanesVisible = visible;
            if (planeManager != null) planeManager.SetTrackablesActive(visible);
            PlanesVisibleChanged?.Invoke(visible);
        }

        public void TogglePlanes() => SetPlanesVisible(!PlanesVisible);

        // ---------- Reticle ----------

        void UpdateReticle()
        {
            reticleValid = InputEnabled && TryRaycast(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out reticlePose, out reticleOnFloor, out reticleNormalY);
            if (reticle == null) return;

            bool show = reticleValid && !IsLocked;
            if (reticle.gameObject.activeSelf != show) reticle.gameObject.SetActive(show);
            if (!show) return;

            // Lay the reticle flat on the plane, lifted a hair to avoid z-fighting.
            reticle.SetPositionAndRotation(
                reticlePose.position + reticlePose.up * 0.005f,
                Quaternion.LookRotation(-reticlePose.up, arCamera.transform.up));
        }

        /// <summary>
        /// Raycasts against detected planes. Vertical planes (walls) win over horizontal ones when both are hit.
        /// The returned pose has up = plane normal, facing the camera.
        /// </summary>
        bool TryRaycast(Vector2 screenPoint, out Pose pose, out bool onFloor, out float normalY)
        {
            pose = default;
            onFloor = false;
            normalY = 0f;
            if (raycastManager == null || !raycastManager.Raycast(screenPoint, hits, TrackableType.PlaneWithinPolygon)) return false;

            ARRaycastHit best = hits[0];
            ARPlane bestPlane = planeManager.GetPlane(best.trackableId);
            if (bestPlane == null || bestPlane.alignment != PlaneAlignment.Vertical)
            {
                foreach (var h in hits)
                {
                    var p = planeManager.GetPlane(h.trackableId);
                    if (p != null && p.alignment == PlaneAlignment.Vertical) { best = h; bestPlane = p; break; }
                }
            }

            Vector3 normal = bestPlane != null ? bestPlane.normal : best.pose.up;
            Vector3 toCamera = arCamera.transform.position - best.pose.position;
            if (Vector3.Dot(normal, toCamera) < 0f) normal = -normal;

            normalY = normal.y;
            onFloor = Mathf.Abs(normal.y) > 0.7f;   // mostly horizontal plane (floor, table, ceiling)
            pose = new Pose(best.pose.position, Quaternion.FromToRotation(Vector3.up, normal));
            return true;
        }

        // ---------- Placing ----------

        void PlaceAt(Pose pose, bool onFloor)
        {
            Vector3 normal = pose.up;
            Transform t = screen.transform;

            if (onFloor)
            {
                // Stand the screen up and make it face the camera.
                Vector3 toCam = arCamera.transform.position - pose.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude < 0.0001f) toCam = -arCamera.transform.forward;
                toCam.Normalize();
                // Quad is visible from its -Z side, so its forward points away from the viewer.
                t.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
                t.position = pose.position + Vector3.up * (screen.baseHeight * screen.UserScale * 0.5f);
            }
            else
            {
                // On a wall: face out of the wall, upright using world up.
                Vector3 up = Vector3.up;
                if (Mathf.Abs(Vector3.Dot(normal, up)) > 0.95f) up = arCamera.transform.up;
                t.rotation = Quaternion.LookRotation(-normal, up);
                t.position = pose.position + normal * wallOffset;
            }

            bool first = !IsPlaced;
            IsPlaced = true;
            if (!screen.gameObject.activeSelf)
            {
                screen.gameObject.SetActive(true);
                screen.PlayBuiltin(0);
            }
            if (first) Placed?.Invoke();
        }

        public void SetLocked(bool locked)
        {
            IsLocked = locked;
            LockChanged?.Invoke(locked);
        }

        public void ToggleLock() => SetLocked(!IsLocked);

        /// <summary>Removes the hologram screen so the user can place it again.</summary>
        public void ResetScreen()
        {
            IsPlaced = false;
            screen.ResetState();
            screen.gameObject.SetActive(false);
            SetLocked(false);
        }

        // ---------- Touch ----------

        bool IsOverUi(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = position };
            uiHits.Clear();
            EventSystem.current.RaycastAll(data, uiHits);
            return uiHits.Count > 0;
        }

        void HandleTouches()
        {
            var touches = Touch.activeTouches;

            if (touches.Count >= 2)
            {
                HandleTwoFingers(touches[0], touches[1]);
                return;
            }
            twoFingerActive = false;

            if (touches.Count == 1) HandleSingleTouch(touches[0]);
            else trackedTouchId = -1;
        }

        void HandleSingleTouch(Touch t)
        {
            if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                trackedTouchId = t.touchId;
                touchStart = t.screenPosition;
                touchStartTime = t.startTime;
                touchMoved = false;
                touchStartedOverUi = IsOverUi(t.screenPosition);
                return;
            }
            if (t.touchId != trackedTouchId) return;

            float movedInches = Vector2.Distance(t.screenPosition, touchStart) / Mathf.Max(Screen.dpi, 96f);
            if (movedInches > tapMaxInches) touchMoved = true;

            if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                bool isTap = !touchMoved && (Time.timeAsDouble - touchStartTime) < maxTapSeconds;
                trackedTouchId = -1;
                if (!isTap || touchStartedOverUi || IsLocked) return;

                if (TryRaycast(t.screenPosition, out Pose pose, out bool onFloor, out _)) PlaceAt(pose, onFloor);
            }
            else if (t.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                trackedTouchId = -1;
            }
        }

        void HandleTwoFingers(Touch a, Touch b)
        {
            Vector2 delta = b.screenPosition - a.screenPosition;
            float dist = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            if (!twoFingerActive)
            {
                twoFingerActive = true;
                lastPinchDistance = dist;
                lastTwistAngle = angle;
                trackedTouchId = -1;
                return;
            }

            if (IsPlaced && !IsLocked && screen != null && !IsOverUi(a.screenPosition) && !IsOverUi(b.screenPosition) && lastPinchDistance > 1f)
            {
                screen.SetUserScale(screen.UserScale * (dist / lastPinchDistance));

                // Twist rotates the screen around its own normal. Screen X on the display is clockwise-negative.
                float dAngle = Mathf.DeltaAngle(lastTwistAngle, angle);
                if (Mathf.Abs(dAngle) < 45f)
                    screen.transform.Rotate(0f, 0f, -dAngle, Space.Self);
            }
            lastPinchDistance = dist;
            lastTwistAngle = angle;
        }
    }
}
