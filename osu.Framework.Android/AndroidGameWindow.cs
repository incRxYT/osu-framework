// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
//
// High refresh rate (90fps) modifications by incRX
// osu-framework fork: https://github.com/incRxYT/osu-framework

using System;
using System.Linq;
using Android.Views;
using osu.Framework.Platform;
using osu.Framework.Platform.SDL3;

namespace osu.Framework.Android
{
    internal class AndroidGameWindow : SDL3MobileWindow
    {
        public override IntPtr SurfaceHandle => AndroidGameActivity.Surface.Holder?.Surface?.Handle ?? IntPtr.Zero;

        public AndroidGameWindow(GraphicsSurfaceType surfaceType, string appName)
            : base(surfaceType, appName)
        {
        }

        public override void Create()
        {
            base.Create();

            SafeAreaPadding.BindTo(AndroidGameActivity.Surface.SafeAreaPadding);

            requestHighRefreshRate();
        }

        /// <summary>
        /// Requests a high refresh rate display mode from the OS via WindowManager.
        /// This is complementary to <see cref="AndroidGameSurface.HandleResume"/> which sets
        /// the frame rate hint on the Surface itself. Together they cover both the compositor
        /// side (WindowManager display mode) and the render side (Surface frame rate hint).
        /// </summary>
        /// <remarks>
        /// On API 30+ we use <see cref="WindowManager"/> preferred display mode to select
        /// the highest available refresh rate that matches the current resolution.
        /// On API 23-29 we fall back to setting <see cref="WindowManagerLayoutParams.PreferredRefreshRate"/>
        /// which is a hint only — the OS may ignore it — but it's the best available on older devices.
        /// Below API 23 there is no mechanism to request a specific refresh rate.
        /// </remarks>
        private void requestHighRefreshRate()
        {
            var activity = AndroidGameActivity.Surface.Context as AndroidGameActivity;
            if (activity?.Window == null) return;

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                // API 30+: enumerate all supported display modes and pick the one with the
                // highest refresh rate that matches the current display resolution.
                // This covers devices where 90/120hz is a separate display mode rather than
                // just a compositor setting (common on older 90hz panels like OnePlus, Pixel 4).
                var display = activity.WindowManager?.DefaultDisplay;
                if (display == null) return;

                var currentMode = display.Mode;
                if (currentMode == null) return;

                var supportedModes = display.SupportedModes;
                if (supportedModes == null) return;

                // Find the highest refresh rate mode that matches current resolution exactly.
                // We match resolution to avoid accidentally switching to a lower-res high-fps mode.
                var bestMode = supportedModes
                    .Where(m => m.PhysicalWidth == currentMode.PhysicalWidth
                             && m.PhysicalHeight == currentMode.PhysicalHeight)
                    .OrderByDescending(m => m.RefreshRate)
                    .FirstOrDefault();

                if (bestMode != null && bestMode.ModeId != currentMode.ModeId)
                {
                    var layoutParams = activity.Window.Attributes;
                    if (layoutParams != null)
                    {
                        layoutParams.PreferredDisplayModeId = bestMode.ModeId;
                        activity.Window.Attributes = layoutParams;
                    }
                }
            }
            else if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                // API 23-29: no mode switching, but we can hint at a preferred refresh rate.
                // 90f is a hint — the display will honour it if capable, ignore it otherwise.
                var layoutParams = activity.Window.Attributes;
                if (layoutParams != null)
                {
                    layoutParams.PreferredRefreshRate = 90f;
                    activity.Window.Attributes = layoutParams;
                }
            }
        }
    }
}
