using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PromptBar
{
    internal static class WindowShaping
    {
        public static void ApplyRoundedRegion(Window window, IntPtr handle, double radiusDip)
        {
            int width;
            int height;
            double scaleX;
            double scaleY;
            GetWindowPixelSize(window, out width, out height, out scaleX, out scaleY);

            int radius = Math.Max(0, (int)Math.Round(radiusDip * Math.Max(scaleX, scaleY)));
            int diameter = Math.Max(1, radius * 2);
            IntPtr region = NativeMethods.CreateRoundRectRgn(0, 0, width + 1, height + 1, diameter, diameter);
            ApplyRegion(handle, region);
        }

        public static void ApplyBottomRoundedRegion(Window window, IntPtr handle, double radiusDip)
        {
            int width;
            int height;
            double scaleX;
            double scaleY;
            GetWindowPixelSize(window, out width, out height, out scaleX, out scaleY);

            int radius = Math.Max(0, (int)Math.Round(radiusDip * Math.Max(scaleX, scaleY)));
            if (radius <= 0)
            {
                ApplyRegion(handle, NativeMethods.CreateRectRgn(0, 0, width, height));
                return;
            }

            int diameter = radius * 2;
            int roundTop = Math.Max(0, height - diameter);
            int fillBottom = Math.Max(0, height - radius);

            IntPtr top = IntPtr.Zero;
            IntPtr bottom = IntPtr.Zero;
            IntPtr combined = IntPtr.Zero;

            try
            {
                top = NativeMethods.CreateRectRgn(0, 0, width, fillBottom);
                bottom = NativeMethods.CreateRoundRectRgn(0, roundTop, width + 1, height + 1, diameter, diameter);
                combined = NativeMethods.CreateRectRgn(0, 0, 0, 0);

                if (top == IntPtr.Zero || bottom == IntPtr.Zero || combined == IntPtr.Zero)
                {
                    return;
                }

                NativeMethods.CombineRgn(combined, top, bottom, NativeMethods.RGN_OR);
                IntPtr regionForWindow = combined;
                combined = IntPtr.Zero;
                ApplyRegion(handle, regionForWindow);
            }
            finally
            {
                DeleteRegion(top);
                DeleteRegion(bottom);
                DeleteRegion(combined);
            }
        }

        private static void GetWindowPixelSize(Window window, out int width, out int height, out double scaleX, out double scaleY)
        {
            scaleX = 1.0;
            scaleY = 1.0;

            PresentationSource source = PresentationSource.FromVisual(window);
            if (source != null && source.CompositionTarget != null)
            {
                Matrix transform = source.CompositionTarget.TransformToDevice;
                scaleX = transform.M11;
                scaleY = transform.M22;
            }

            double widthDip = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
            double heightDip = window.ActualHeight > 0 ? window.ActualHeight : window.Height;

            if (Double.IsNaN(widthDip) || widthDip <= 0)
            {
                widthDip = 1;
            }

            if (Double.IsNaN(heightDip) || heightDip <= 0)
            {
                heightDip = 1;
            }

            width = Math.Max(1, (int)Math.Ceiling(widthDip * scaleX));
            height = Math.Max(1, (int)Math.Ceiling(heightDip * scaleY));
        }

        private static void ApplyRegion(IntPtr handle, IntPtr region)
        {
            if (handle == IntPtr.Zero || region == IntPtr.Zero)
            {
                DeleteRegion(region);
                return;
            }

            if (NativeMethods.SetWindowRgn(handle, region, true) == 0)
            {
                DeleteRegion(region);
            }
        }

        private static void DeleteRegion(IntPtr region)
        {
            if (region != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(region);
            }
        }
    }
}
