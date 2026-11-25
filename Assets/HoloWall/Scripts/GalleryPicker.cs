using System;
using UnityEngine;

namespace HoloWall
{
    /// <summary>
    /// Opens the phone's video gallery (via the NativeGallery plugin by yasirkula, MIT) and reports the chosen file.
    /// </summary>
    public static class GalleryPicker
    {
        /// <param name="onPicked">Called with a local file path when the user chose a video.</param>
        /// <param name="onProblem">Called with a user-facing message when nothing could be picked because of permissions or errors.</param>
        public static void PickVideo(Action<string> onPicked, Action<string> onProblem)
        {
#if UNITY_EDITOR
            onProblem?.Invoke("The gallery picker only works on a phone.");
#else
            if (NativeGallery.IsMediaPickerBusy()) return;

            NativeGallery.GetVideoFromGallery(path =>
            {
                if (!string.IsNullOrEmpty(path))
                {
                    onPicked?.Invoke(path);
                    return;
                }

                // null = the user cancelled, or the permission was denied.
                if (!NativeGallery.CheckPermission(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Video))
                    onProblem?.Invoke("Gallery access was denied. Allow videos access in Settings > Apps > HoloWall > Permissions.");
            }, "Choose a video", "video/*");
#endif
        }
    }
}
