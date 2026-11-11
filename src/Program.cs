using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Alien Translate")]
[assembly: System.Reflection.AssemblyDescription("Thai Kedmanee and English US keyboard-layout recovery for Windows")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace AlienTranslate
{
    internal static class Program
    {
        internal const string DisplayName = "Alien Translate · แปลงแป้นไทย–อังกฤษ";

        [STAThread]
        private static void Main()
        {
            bool firstInstance;
            using (var instance = new Mutex(true, @"Local\AlienTranslate", out firstInstance))
            {
                if (!firstInstance)
                {
                    MessageBox.Show("โปรแกรมเปิดอยู่แล้ว ดูไอคอน Alien Translate ที่มุมขวาล่าง", "Alien Translate");
                    return;
                }

                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                using (var app = new TranslatorApp(icon ?? SystemIcons.Application))
                    Application.Run(app);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }

    internal sealed class TranslatorApp : ApplicationContext
    {
        private const int ClipboardHotkey = 1;
        private const int PointerHotkey = 2;

        private readonly ResultWindow result = new ResultWindow();
        private readonly PreviewWindow preview = new PreviewWindow();
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly ContextMenuStrip directionMenu = new ContextMenuStrip();
        private readonly HotkeyWindow hotkeys;
        private readonly RightClickHook rightClick;
        private readonly LineCopyWatch lineCopy;
        private readonly bool clipboardHotkeyAvailable;
        private readonly bool pointerHotkeyAvailable;
        private readonly int processId;
        private Task<string> pointerRead;
        private Task<string> selectionRead;
        private string menuText;
        private Point rightDownPoint;
        private bool waitingForRightUp;
        private int rightClickVersion;
        private int requestVersion;
        private bool exiting;

        internal TranslatorApp(Icon icon)
        {
            result.Icon = icon;
            using (var process = Process.GetCurrentProcess())
                processId = process.Id;

            hotkeys = new HotkeyWindow(HandleHotkey);
            clipboardHotkeyAvailable = hotkeys.Register(ClipboardHotkey, Keys.J);
            pointerHotkeyAvailable = hotkeys.Register(PointerHotkey, Keys.H);
            lineCopy = new LineCopyWatch(FromLineCopy);
            rightClick = new RightClickHook(result, BeginRightClick, EndRightClick, CancelRightClick);

            directionMenu.Font = new Font("Tahoma", 10);
            directionMenu.Items.Add("อังกฤษ → ไทยเกษมณี", null, (sender, args) =>
                ChooseConversion(text => Kedmanee.Convert(text, true)));
            directionMenu.Items.Add("ไทยเกษมณี → อังกฤษ", null, (sender, args) =>
                ChooseConversion(text => Kedmanee.Convert(text, false)));
            directionMenu.Items.Add(new ToolStripSeparator());
            directionMenu.Items.Add("แก้ Caps Lock · ไทย → ไทย", null, (sender, args) =>
                ChooseConversion(Kedmanee.FixThaiCapsLock));
            directionMenu.Items.Add("แก้ Caps Lock · อังกฤษ → ไทย", null, (sender, args) =>
                ChooseConversion(Kedmanee.FixEnglishCapsLock));
            directionMenu.Closed += (sender, args) =>
            {
                if (args.CloseReason != ToolStripDropDownCloseReason.ItemClicked)
                    menuText = null;
            };

            menu.Items.Add("แปลงข้อความที่คัดลอก", null, (sender, args) => FromClipboard());
            menu.Items.Add("วิธีใช้", null, (sender, args) => ShowHelp());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("ออกจากโปรแกรม", null, (sender, args) => ExitThread());

            tray = new NotifyIcon
            {
                Icon = icon,
                Text = Program.DisplayName,
                ContextMenuStrip = menu,
                Visible = true
            };
            tray.DoubleClick += (sender, args) => ShowHelp();
            ShowHelp();
        }

        private void ShowHelp()
        {
            requestVersion++;
            string copy = clipboardHotkeyAvailable
                ? "คัดลอกข้อความ แล้วกด Ctrl+Alt+J"
                : "คัดลอกข้อความ แล้วเลือก “แปลงข้อความที่คัดลอก” จากไอคอนมุมขวาล่าง";
            string selection = rightClick.Available
                ? "คลุมข้อความแล้วคลิกขวาบนข้อความที่เลือก จากนั้นเลือกทิศทางการแปลง"
                : "เปิดเมนูจากการคลิกขวาไม่ได้ในขณะนี้";
            result.Present("แป้นไทยเกษมณี ↔ อังกฤษ", selection + "\r\n\r\n" + copy
                + "\r\n\r\nใน LINE เลือก “คัดลอกข้อความที่เลือก” เพื่อเปิดเมนูแปลง"
                + "\r\n\r\nShift+คลิกขวา เพื่อเปิดเฉพาะเมนูของแอป", Cursor.Position);
        }

        private void HandleHotkey(int id)
        {
            if (id == ClipboardHotkey)
                FromClipboard();
            if (id == PointerHotkey)
                FromPointer();
        }

        private void FromClipboard()
        {
            requestVersion++;
            CancelRightClick();
            lineCopy.Cancel();
            Point point = Cursor.Position;
            try
            {
                string text = Clipboard.GetText(TextDataFormat.UnicodeText);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ShowDirections(text, point);
                    return;
                }

                result.Present("ยังไม่มีข้อความที่คัดลอก", "คัดลอกข้อความจากโปรแกรมต้นทางแล้วลองอีกครั้ง", point);
            }
            catch (ExternalException)
            {
                result.Present("อ่านคลิปบอร์ดไม่ได้", "ลองกดคีย์ลัดอีกครั้ง", point);
            }
        }

        private void BeginRightClick(Point point, IntPtr target)
        {
            CancelRightClick();
            lineCopy.Cancel();
            if (exiting || target == IntPtr.Zero)
                return;

            int targetProcess;
            GetWindowThreadProcessId(target, out targetProcess);
            if (targetProcess == 0 || targetProcess == processId)
                return;

            if (IsLineProcess(targetProcess))
                lineCopy.Arm(targetProcess);

            if (selectionRead != null && !selectionRead.IsCompleted)
                return;

            waitingForRightUp = true;
            rightDownPoint = point;
            selectionRead = Task.Run(() => ScreenText.ReadSelection(point, target, targetProcess));
        }

        private async void EndRightClick(Point point)
        {
            if (!waitingForRightUp)
                return;
            waitingForRightUp = false;
            if (Math.Abs(point.X - rightDownPoint.X) > 8 || Math.Abs(point.Y - rightDownPoint.Y) > 8)
                return;

            int version = rightClickVersion;
            var read = selectionRead;
            var completed = await Task.WhenAny(read, Task.Delay(450));
            // Give the source app time to handle the native right-click.
            await Task.Delay(45);
            if (exiting || version != rightClickVersion || completed != read || read.Status != TaskStatus.RanToCompletion)
                return;

            var cursor = Cursor.Position;
            if (Math.Abs(cursor.X - point.X) <= 30 && Math.Abs(cursor.Y - point.Y) <= 30
                && !string.IsNullOrWhiteSpace(read.Result))
            {
                requestVersion++;
                ShowDirections(read.Result, point);
            }
        }

        private void CancelRightClick()
        {
            rightClickVersion++;
            waitingForRightUp = false;
        }

        private void ShowDirections(string text, Point point)
        {
            lineCopy.Cancel();
            result.Hide();
            directionMenu.Close();
            menuText = text;
            directionMenu.Show(point, ToolStripDropDownDirection.AboveRight);
        }

        private void FromLineCopy(string text)
        {
            requestVersion++;
            CancelRightClick();
            ShowDirections(text, Cursor.Position);
        }

        private static bool IsLineProcess(int id)
        {
            try
            {
                using (var process = Process.GetProcessById(id))
                    return string.Equals(process.ProcessName, "LINE", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private void ChooseConversion(Func<string, string> convert)
        {
            string text = menuText;
            menuText = null;
            if (!string.IsNullOrWhiteSpace(text))
                preview.Present(convert(text), Cursor.Position);
        }

        private async void FromPointer()
        {
            int version = ++requestVersion;
            Point point = Cursor.Position;
            string copyHint = clipboardHotkeyAvailable
                ? "คัดลอกข้อความแล้วกด Ctrl+Alt+J"
                : "คัดลอกข้อความแล้วเลือก “แปลงข้อความที่คัดลอก” จากไอคอนมุมขวาล่าง";

            if (pointerRead != null && !pointerRead.IsCompleted)
            {
                result.Present("กำลังรอโปรแกรมต้นทาง", copyHint, point);
                return;
            }

            var read = Task.Run(() => ScreenText.ReadAt(point, processId));
            pointerRead = read;
            var completed = await Task.WhenAny(read, Task.Delay(1200));
            if (exiting || version != requestVersion)
                return;

            string text = completed == read && read.Status == TaskStatus.RanToCompletion ? read.Result : null;
            if (string.IsNullOrWhiteSpace(text))
                result.Present("อ่านข้อความตรงที่ชี้ไม่ได้", copyHint, point);
            else
                result.Present("ผลตามแป้นไทยเกษมณี", Kedmanee.Convert(text, true), point, true);
        }

        protected override void ExitThreadCore()
        {
            exiting = true;
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                rightClick.Dispose();
                lineCopy.Dispose();
                hotkeys.Dispose();
                tray.Dispose();
                menu.Dispose();
                directionMenu.Dispose();
                preview.Dispose();
                result.Dispose();
            }
            base.Dispose(disposing);
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);
    }

    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int HotkeyMessage = 0x0312;
        private const uint ControlAltNoRepeat = 0x4003;
        private readonly Action<int> onHotkey;

        internal HotkeyWindow(Action<int> onHotkey)
        {
            this.onHotkey = onHotkey;
            CreateHandle(new CreateParams());
        }

        internal bool Register(int id, Keys key)
        {
            return RegisterHotKey(Handle, id, ControlAltNoRepeat, (uint)key);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == HotkeyMessage)
                onHotkey(message.WParam.ToInt32());
            else
                base.WndProc(ref message);
        }

        public void Dispose()
        {
            // Destroying the window releases its registered hotkeys.
            DestroyHandle();
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    }

    internal sealed class ResultWindow : Form
    {
        private readonly Label heading;
        private readonly TextBox body;
        private readonly Button copy;
        private string resultText;

        internal ResultWindow()
        {
            Text = Program.DisplayName;
            Font = new Font("Tahoma", 10);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(392, 213);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.White;

            heading = new Label
            {
                Location = new Point(14, 12),
                Size = new Size(364, 22),
                Font = new Font(Font, FontStyle.Bold)
            };
            body = new TextBox
            {
                Location = new Point(14, 38),
                Size = new Size(364, 125),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                Font = new Font("Tahoma", 12),
                AccessibleName = "ข้อความ"
            };
            copy = new Button
            {
                Location = new Point(260, 174),
                Size = new Size(118, 27),
                Text = "คัดลอกผล"
            };
            copy.Click += (sender, args) => CopyResult();
            Controls.AddRange(new Control[] { heading, body, copy });
            KeyPreview = true;
            KeyDown += (sender, args) =>
            {
                if (args.KeyCode == Keys.Escape)
                    Hide();
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        internal void Present(string title, string text, Point anchor, bool canCopy = false)
        {
            heading.Text = title;
            body.Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            body.SelectionStart = 0;
            body.SelectionLength = 0;
            body.ScrollToCaret();
            resultText = canCopy ? text : null;
            copy.Text = "คัดลอกผล";
            copy.Visible = canCopy;

            var screen = Screen.FromPoint(anchor).WorkingArea;
            int x = Math.Max(screen.Left, Math.Min(anchor.X + 14, screen.Right - Width));
            int y = anchor.Y + 20;
            if (y + Height > screen.Bottom)
                y = anchor.Y - Height - 12;
            Location = new Point(x, Math.Max(screen.Top, y));
            Show();
        }

        private void CopyResult()
        {
            try
            {
                Clipboard.SetText(resultText, TextDataFormat.UnicodeText);
                copy.Text = "คัดลอกแล้ว";
            }
            catch (ExternalException)
            {
                copy.Text = "ลองอีกครั้ง";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs args)
        {
            base.OnFormClosing(args);
            if (args.CloseReason == CloseReason.UserClosing)
            {
                args.Cancel = true;
                Hide();
            }
        }
    }
}
