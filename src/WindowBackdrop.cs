using System;
using System.Runtime.InteropServices;

namespace PromptBar
{
    internal static class WindowBackdrop
    {
        private const int DwmCornerRound = 2;
        private const int DwmBackdropMica = 2;
        private const int DwmBackdropAcrylic = 3;
        private const int AccentEnableBlurBehind = 3;
        private const int AccentEnableAcrylicBlurBehind = 4;

        public static void ApplyMicaAero(IntPtr handle, bool acrylic)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            TrySetDwmAttribute(handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, 1);
            TrySetDwmAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, DwmCornerRound);
            TrySetDwmAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, acrylic ? DwmBackdropAcrylic : DwmBackdropMica);
            TrySetAccent(handle, acrylic ? AccentEnableAcrylicBlurBehind : AccentEnableBlurBehind);
        }

        private static void TrySetDwmAttribute(IntPtr handle, int attribute, int value)
        {
            try
            {
                NativeMethods.DwmSetWindowAttribute(handle, attribute, ref value, Marshal.SizeOf(typeof(int)));
            }
            catch
            {
            }
        }

        private static void TrySetAccent(IntPtr handle, int accentState)
        {
            IntPtr policyPointer = IntPtr.Zero;
            try
            {
                NativeMethods.AccentPolicy policy = new NativeMethods.AccentPolicy();
                policy.AccentState = accentState;
                policy.AccentFlags = 2;
                policy.GradientColor = unchecked((int)0xB0181818);

                int policySize = Marshal.SizeOf(policy);
                policyPointer = Marshal.AllocHGlobal(policySize);
                Marshal.StructureToPtr(policy, policyPointer, false);

                NativeMethods.WindowCompositionAttributeData data = new NativeMethods.WindowCompositionAttributeData();
                data.Attribute = NativeMethods.WCA_ACCENT_POLICY;
                data.Data = policyPointer;
                data.SizeOfData = policySize;

                NativeMethods.SetWindowCompositionAttribute(handle, ref data);
            }
            catch
            {
            }
            finally
            {
                if (policyPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(policyPointer);
                }
            }
        }
    }
}
