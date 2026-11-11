using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AlienTranslate
{
    internal sealed class LineCopyWatch : NativeWindow, IDisposable
    {
        private const int ClipboardUpdate = 0x031D;
        private readonly Timer timeout = new Timer { Interval = 15000 };
        private readonly Action<string> onCopy;
        private int sourceProcess;
        private uint startingSequence;

        internal LineCopyWatch(Action<string> onCopy)
        {
            this.onCopy = onCopy;
            CreateHandle(new CreateParams());
            timeout.Tick += (sender, args) => Cancel();
        }

        internal void Arm(int processId)
        {
            Cancel();
            startingSequence = GetClipboardSequenceNumber();
            if (!AddClipboardFormatListener(Handle))
                return;

            sourceProcess = processId;
            timeout.Start();
        }

        internal void Cancel()
        {
            if (sourceProcess == 0)
                return;

            sourceProcess = 0;
            timeout.Stop();
            RemoveClipboardFormatListener(Handle);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == ClipboardUpdate)
                ReadNewCopy();
            else
                base.WndProc(ref message);
        }

        private void ReadNewCopy()
        {
            if (sourceProcess == 0 || GetClipboardSequenceNumber() == startingSequence)
                return;
            if (!IsFromSource())
            {
                Cancel();
                return;
            }

            string text;
            try
            {
                text = Clipboard.GetText(TextDataFormat.UnicodeText);
            }
            catch (ExternalException)
            {
                return;
            }

            if (!IsFromSource() || string.IsNullOrWhiteSpace(text))
                return;

            Cancel();
            onCopy(text);
        }

        private bool IsFromSource()
        {
            int ownerProcess;
            GetWindowThreadProcessId(GetClipboardOwner(), out ownerProcess);
            return ownerProcess == sourceProcess;
        }

        public void Dispose()
        {
            Cancel();
            timeout.Dispose();
            DestroyHandle();
        }

        [DllImport("user32.dll")]
        private static extern bool AddClipboardFormatListener(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool RemoveClipboardFormatListener(IntPtr window);

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardOwner();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);
    }
}
