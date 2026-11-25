using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace HoloWall
{
    /// <summary>
    /// Moves the AR camera with the phone using the XR device API (UnityEngine.XR.InputDevices).
    /// Used instead of the Input System's TrackedPoseDriver, whose ARCore device layout is not
    /// registered in every Unity version. Updates in Update and again just before rendering
    /// so the virtual camera matches the camera image with no lag.
    /// </summary>
    [DefaultExecutionOrder(-30000)]
    public class XRCameraPoseDriver : MonoBehaviour
    {
        static readonly List<InputDevice> devices = new List<InputDevice>();
        InputDevice device;

        /// <summary>True once a pose has been received from the AR device.</summary>
        public bool HasPose { get; private set; }

        void OnEnable() => Application.onBeforeRender += OnBeforeRender;
        void OnDisable() => Application.onBeforeRender -= OnBeforeRender;

        void Update() => ApplyPose();

        [BeforeRenderOrder(-30000)]
        void OnBeforeRender() => ApplyPose();

        void ApplyPose()
        {
            if (!device.isValid)
            {
                InputDevices.GetDevicesAtXRNode(XRNode.CenterEye, devices);
                if (devices.Count == 0) InputDevices.GetDevices(devices);
                device = default;
                foreach (var d in devices)
                {
                    if (d.TryGetFeatureValue(CommonUsages.devicePosition, out _) ||
                        d.TryGetFeatureValue(CommonUsages.centerEyePosition, out _)) { device = d; break; }
                }
                if (!device.isValid) return;
            }

            bool hasPos = device.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 position)
                       || device.TryGetFeatureValue(CommonUsages.devicePosition, out position);
            bool hasRot = device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation)
                       || device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation);

            if (hasPos) transform.localPosition = position;
            if (hasRot) transform.localRotation = rotation;
            HasPose |= hasPos && hasRot;
        }
    }
}
