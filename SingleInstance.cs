using System.Runtime.InteropServices;

namespace ProxyForward;

internal static class SingleInstance
{
    private const string MutexName = "Global\\ProxyForward.Ouwenwu.SingleInstance";
    private const string ShowMessageName = "ProxyForward.ShowMainWindow.v1";

    public static readonly int ShowMainWindowMessage = NativeMethods.RegisterWindowMessage(ShowMessageName);

    public static bool TryAcquire(out Mutex mutex)
    {
        mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        return createdNew;
    }

    public static void SignalExistingInstance()
    {
        NativeMethods.PostMessage(NativeMethods.HwndBroadcast, ShowMainWindowMessage, IntPtr.Zero, IntPtr.Zero);
    }

    private static class NativeMethods
    {
        public static readonly IntPtr HwndBroadcast = new(0xffff);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
