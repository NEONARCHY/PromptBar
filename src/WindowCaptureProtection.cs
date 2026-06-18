using System;

namespace PromptBar
{
    internal static class WindowCaptureProtection
    {
        public static bool Apply(IntPtr handle, bool enabled)
        {
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            if (!enabled)
            {
                return NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WDA_NONE);
            }

            if (NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE))
            {
                return true;
            }

            return NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WDA_MONITOR);
        }
    }
}
