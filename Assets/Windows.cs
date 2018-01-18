using UnityEngine;
using System;
using System.Runtime.InteropServices;

public class Windows : MonoBehaviour{

    #region Variable Field

    public static IntPtr hWnd;

    #endregion

    #region DLLImport Field

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

	[DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string className, string windowName);

    #endregion

    #region Window Handler Field

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO{
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }
 
    private const uint FLASHW_ALL = 3;
    private const uint FLASHW_TIMERNOFG = 12;

    private static FLASHWINFO Create_FLASHWINFO(IntPtr handle, uint flags, uint count, uint timeout){
        FLASHWINFO fi = new FLASHWINFO();
        fi.cbSize = Convert.ToUInt32(Marshal.SizeOf(fi));
        fi.hwnd = handle;
        fi.dwFlags = flags;
        fi.uCount = count;
        fi.dwTimeout = timeout;
        return fi;
    }

    #endregion

    public static void FlashWindow() {
#if UNITY_EDITOR
#else
        if(hWnd == new IntPtr()){
            hWnd = FindWindow(null, Application.productName);
        }
        FLASHWINFO fi = Create_FLASHWINFO(hWnd, FLASHW_ALL | FLASHW_TIMERNOFG, uint.MaxValue, 0);
        FlashWindowEx(ref fi);
        #endif
    }
}