using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace SharkTools
{
    public static class ThumbnailHelper
    {
        public static string GetThumbnailBase64(ISldWorks swApp, string filePath)
        {
            // 1. Try SolidWorks API
            try
            {
                object hBitmapObj = swApp.GetPreviewBitmap(filePath, "");
                if (hBitmapObj != null)
                {
                    long hBitmapVal = 0;
                    try { hBitmapVal = Convert.ToInt64(hBitmapObj); } catch { }

                    if (hBitmapVal != 0)
                    {
                        IntPtr hBitmapPtr = new IntPtr(hBitmapVal);
                        try
                        {
                            using (Bitmap bmp = Image.FromHbitmap(hBitmapPtr))
                            {
                                return BitmapToBase64(bmp);
                            }
                        }
                        finally
                        {
                            DeleteObject(hBitmapPtr);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error if needed
                Console.WriteLine("SW API Thumbnail failed: " + ex.Message);
            }

            // 2. Try Windows Shell API (Fallback)
            try
            {
                using (Bitmap bmp = GetShellThumbnail(filePath))
                {
                    if (bmp != null)
                    {
                        return BitmapToBase64(bmp);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Shell API Thumbnail failed: " + ex.Message);
            }

            return null;
        }

        private static string BitmapToBase64(Bitmap bmp)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                byte[] byteImage = ms.ToArray();
                return "data:image/png;base64," + Convert.ToBase64String(byteImage);
            }
        }

        // --- Windows Shell API P/Invoke ---

        private static Bitmap GetShellThumbnail(string fileName)
        {
            IShellItemImageFactory factory = null;
            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                Guid uuid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"); // IShellItemImageFactory
                SHCreateItemFromParsingName(fileName, IntPtr.Zero, uuid, out factory);

                if (factory != null)
                {
                    SIZE size = new SIZE { cx = 256, cy = 256 };
                    factory.GetImage(size, 0, out hBitmap);

                    if (hBitmap != IntPtr.Zero)
                    {
                        return Image.FromHbitmap(hBitmap);
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            finally
            {
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (factory != null) Marshal.ReleaseComObject(factory);
            }
            return null;
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            [In] IntPtr pbc,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        private interface IShellItemImageFactory
        {
            void GetImage(
                [In, MarshalAs(UnmanagedType.Struct)] SIZE size,
                [In] int flags,
                [Out] out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }
    }
}
