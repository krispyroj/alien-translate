using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AlienTranslate
{
    internal sealed class RightClickHook : IDisposable
    {
        private const int MouseLowLevel = 14;
        private const int LeftDown = 0x0201;
        private const int RightDown = 0x0204;
        private const int RightUp = 0x0205;
        private const int Shift = 0x10;

        private readonly HookProc callback;
        private readonly Control dispatcher;
        private readonly Action<Point, IntPtr> onDown;
        private readonly Action<Point> onUp;
        private readonly Action onCancel;
        private IntPtr hook;

        internal RightClickHook(Control dispatcher, Action<Point, IntPtr> onDown, Action<Point> onUp, Action onCancel)
        {
            this.dispatcher = dispatcher;
            this.onDown = onDown;
            this.onUp = onUp;
            this.onCancel = onCancel;
            callback = MouseEvent;
            hook = SetWindowsHookEx(MouseLowLevel, callback, GetModuleHandle(null), 0);
        }

        internal bool Available { get { return hook != IntPtr.Zero; } }

        private IntPtr MouseEvent(int code, IntPtr message, IntPtr data)
        {
            if (code == 0)
            {
                switch (message.ToInt32())
                {
                    case LeftDown:
                        Dispatch(onCancel);
                        break;
                    case RightDown:
                        // MSLLHOOKSTRUCT starts with the cursor's screen coordinates.
                        var down = new Point(Marshal.ReadInt32(data), Marshal.ReadInt32(data, 4));
                        var target = GetAsyncKeyState(Shift) < 0 ? IntPtr.Zero : WindowFromPoint(down);
                        Dispatch(() => onDown(down, target));
                        break;
                    case RightUp:
                        var up = new Point(Marshal.ReadInt32(data), Marshal.ReadInt32(data, 4));
                        Dispatch(() => onUp(up));
                        break;
                }
            }
            return CallNextHookEx(hook, code, message, data);
        }

        private void Dispatch(Action action)
        {
            if (dispatcher.IsHandleCreated)
                dispatcher.BeginInvoke(action);
        }

        public void Dispose()
        {
            if (hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            }
        }

        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int type, HookProc proc, IntPtr module, uint threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point point);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);
    }
}
